using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    public static class Ironmelt
    {
        static BinaryTokens tokens;
        static BinaryMap map;
        static byte[] raw;

        static Encoding ANSI = Encoding.GetEncoding(1250);

        public static BinaryMap Decode(string gameToken, byte[] bytes)
        {
            tokens = new BinaryTokens("eu4bin.csv");
            map = new BinaryMap(bytes.Length, tokens);
            raw = bytes;
            map.AddString(ANSI.GetString(raw, 0, 6), false);
            int i = 6;

            SpecialCode specialcode = SpecialCode.None;

            SpecialCode type = SpecialCode.None;
            string handler = "";
            bool quoted = false;
            bool? list = null;

            bool oneLineInheritance = false;

            BinaryToken code = null;
            BinaryToken inheritedCode = null;

            while (i < raw.Length - 1)
            {

                // 1. Check for a special code first
                specialcode = GetSpecialCode(i);

                if (specialcode != 0)
                {
                    if ((int)specialcode < 4) // brace or equals sign
                    {
                        ReadSpecial(ref i);
                        // Right-brace sign clears current inheritance.
                        if (specialcode == SpecialCode.RightBrace)
                        {
                            inheritedCode = null;
                            type = SpecialCode.None;
                            handler = "";
                            quoted = false;
                            list = null;
                        }
                        continue;
                    }
                    else
                    {
                        // If there is attempt to redefine inherited properties, ignore it and read regardless.
                        if (inheritedCode == null)
                        {
                            type = specialcode;
                        }

                        // Now interpret the special code.
                        i += 2;
                        switch (type)
                        {
                            case SpecialCode.String: ReadString(ref i, quoted); continue;
                            case SpecialCode.Integer: ReadInteger(ref i); continue;
                            case SpecialCode.Float: ReadFloat(ref i); continue;
                            case SpecialCode.Float5: ReadFloat5(ref i); continue;
                            case SpecialCode.Boolean: ReadBoolean(ref i); continue;
                            case SpecialCode.Date: ReadDate(ref i, false); continue; // never quoted in practice
                            default: break;
                        }
                        continue;
                    }

                }

                // 2. No special code encountered, we are looking for a token now

                code = tokens.TryGetCode(raw[i], raw[i + 1]); // from little-endian code

                // If the token does not match anything, try to continue to read data in the same format as previously.
                if (code == null)
                {
                    // Maybe it is an unknown token
                    SpecialCode sc1 = GetSpecialCode(i - 2);
                    SpecialCode sc2 = GetSpecialCode(i + 2);
                    if (sc1 == SpecialCode.Equals || sc2 == SpecialCode.Equals)
                    {
                        map.AddToken("UNKNOWN_" + GetHexString(i));
                        i += 2;
                        if (TryAddSpecials(ref i)) continue;
                        TryReadTypeDefinition(ref i, ref type);
                    }

                    switch (type)
                    {
                        case SpecialCode.String: ReadString(ref i, quoted); continue;
                        case SpecialCode.Integer: ReadInteger(ref i); continue;
                        case SpecialCode.Float: ReadFloat(ref i); continue;
                        case SpecialCode.Float5: ReadFloat5(ref i); continue;
                        case SpecialCode.Boolean: ReadBoolean(ref i); continue;
                        case SpecialCode.Date: ReadDate(ref i, quoted); continue;
                        default: break;
                    }
                    i += 2;
                    continue;
                }

                // 3. We have found the token

                // Tackle inheritance: if it is on for a given token, enforce properties until the next right-brace sign.
                if (code.InheritType)
                {
                    inheritedCode = code;
                    type = inheritedCode.DataType;
                    handler = inheritedCode.Text;
                    quoted = inheritedCode.Quoted;
                    list = inheritedCode.List;
                    oneLineInheritance = true;
                }
                // If no inheritance, read properties from the current token.
                else if (inheritedCode == null)
                {
                    type = code.DataType;
                    handler = code.Text;
                    quoted = code.Quoted;
                    list = code.List;
                }

                if (type == SpecialCode.Variable) // variable
                {
                    // Get parent token           
                    string[] parents = map.GetParents();
                    InterpretCode(gameToken, handler, ref map, ref i, ref type, ref quoted, parents);
                    continue;
                }
                else
                {
                    map.AddToken(code.Text);
                    i += 2;
                    if (TryAddSpecials(ref i))
                    {
                        oneLineInheritance = false;
                        continue;
                    }
                    if (oneLineInheritance) inheritedCode = null;
                    TryReadTypeDefinition(ref i, ref type);
                    switch (type)
                    {
                        case SpecialCode.String: ReadString(ref i, quoted); continue;
                        case SpecialCode.Integer: ReadInteger(ref i); continue;
                        case SpecialCode.Float: ReadFloat(ref i); continue;
                        case SpecialCode.Float5: ReadFloat5(ref i); continue;
                        case SpecialCode.Boolean: ReadBoolean(ref i); continue;
                        case SpecialCode.Date: ReadDate(ref i, quoted); continue;
                        default: continue; // map.AddAttribToken(i); break;
                    }
                }
            }

            map.Finish();
            return map;
        }

        private static string GetHexString(int i)
        {
            return raw[i].ToString("X").PadLeft(2, '0') + raw[i + 1].ToString("X").PadLeft(2, '0');
        }

        private static SpecialCode GetSpecialCode(int i)
        {
            if (raw[i] == 1 && raw[i + 1] == 0) return SpecialCode.Equals; //0x0100
            if (raw[i] == 3 && raw[i + 1] == 0) return SpecialCode.LeftBrace; //0x0300
            if (raw[i] == 4 && raw[i + 1] == 0) return SpecialCode.RightBrace; //0x0400
            if (raw[i] == 12 && raw[i + 1] == 0) return SpecialCode.Integer; //0x0C00
            if (raw[i] == 13 && raw[i + 1] == 0) return SpecialCode.Float; //0x0D00
            if (raw[i] == 14 && raw[i + 1] == 0) return SpecialCode.Boolean; //0x0E00
            if (raw[i] == 15 && raw[i + 1] == 0) return SpecialCode.String; //0x0F00
            if (raw[i] == 20 && raw[i + 1] == 0) return SpecialCode.Integer; //0x1400
            if (raw[i] == 23 && raw[i + 1] == 0) return SpecialCode.String; //0x1700
            if (raw[i] == 103 && raw[i + 1] == 1) return SpecialCode.Float5; //0x6701
            return SpecialCode.None;
        }

        private static string GetTokenText(int i)
        {
            BinaryToken code = tokens.TryGetCode(raw[i], raw[i + 1]); // from little-endian code
            if (code == null)
                return "UNKNOWN_" + GetHexString(i);
            else
                return code.Text;

        }

        private static void InterpretCode(string gameToken, string text, ref BinaryMap map, ref int i, ref SpecialCode type, ref bool quoted, string[] parents)
        {
            if (gameToken == "eu4")
            {
                switch (text)
                {
                    case "type":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 1, "id") ||
                            IsParent(parents, 1, "leader") ||
                            (IsParent(parents, 1, "rebel_faction") && IsParent(parents, -1, "provinces")) ||
                            (IsParent(parents, 1, "advisor") && IsParent(parents, -1, "active_advisors")) ||
                            IsParent(parents, 1, "monarch"))
                        {
                            ReadInteger(ref i);
                        }
                        else if ((IsParent(parents, 1, "advisor") && IsParent(parents, 3, "history")) ||
                                 (IsParent(parents, 1, "advisor") && IsParent(parents, 2, "history")))
                        {
                            ReadString(ref i, false);
                        }
                        else if (IsParent(parents, 1, "advisor"))
                        {
                            ReadInteger(ref i);
                        }
                        else if (IsParent(parents, 1, "war_goal"))
                        {
                            ReadString(ref i, true);
                        }
                        else if (IsParent(parents, 1, "general"))
                        {
                            map.AddToken(GetTokenText(i));
                            i += 2;
                        }
                        else if (IsParent(parents, 1, "rebel_faction") ||
                            IsParent(parents, 1, "revolt") ||
                            IsParent(parents, 1, "mercenary") ||
                            IsParent(parents, 2, "previous_war"))
                        {
                            ReadString(ref i, true);
                        }
                        else if ((IsParent(parents, 1, "regiment") && !IsParent(parents, 2, "military_construction")) ||
                            IsParent(parents, 1, "ship") ||
                            IsParent(parents, 1, "faction") ||
                            IsParent(parents, 1, "military_construction") ||
                            IsParent(parents, 1, "possible_mercenary") ||
                            IsParent(parents, 1, "active_major_mission") ||
                            IsParent(parents, 1, "casus_belli") ||
                            IsParent(parents, 1, "take_province") ||
                            IsParent(parents, 1, "take_core") ||
                            IsParent(parents, 2, "active_war"))
                        {
                            ReadString(ref i, false);
                        }
                        else
                        {
                            ReadInteger(ref i);
                        }
                        break;
                    case "discovered_by":
                        map.AddToken(text);
                        i += 2;
                        if (parents.Length > 0 && parents[parents.Length - 1].StartsWith("-"))
                        {
                            quoted = false;
                        }
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        ReadString(ref i, true);
                        break;

                    case "action":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 1, "diplomacy_construction"))
                        {
                            ReadString(ref i, true);
                        }
                        else if (IsParent(parents, 1, "previous_war"))
                        {
                            ReadDate(ref i, false);
                        }
                        else
                        {
                            ReadInteger(ref i);
                        }
                        break;

                    case "steer_power":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 1, "node"))
                            ReadFloat(ref i);
                        else
                            ReadInteger(ref i);
                        break;

                    case "value":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 1, "improve_relation") ||
                            IsParent(parents, 1, "warningaction"))
                            ReadBoolean(ref i);
                        else
                        {
                            ReadFloat(ref i);
                        }
                        break;

                    case "unit_type":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (parents.Length > 0 && parents[parents.Length - 1].StartsWith("O0"))
                        {
                            map.AddToken(GetTokenText(i));
                            i += 2;
                        }
                        else
                        {
                            ReadString(ref i, false);
                        }
                        break;

                    case "active":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 1, "rebel_faction") ||
                            IsParent(parents, 1, "siege_combat") ||
                            IsParent(parents, 1, "combat"))
                        {
                            map.AddToken(GetTokenText(i));
                            i += 2;
                        }
                        else
                        {
                            ReadBoolean(ref i);
                        }
                        break;

                    case "revolution_target":
                        map.AddToken(text);
                        i += 2;
                        if (TryAddSpecials(ref i)) break;
                        TryReadTypeDefinition(ref i, ref type);

                        if (IsParent(parents, 2, "history"))
                        {
                            map.AddToken(GetTokenText(i));
                            i += 2;
                        }
                        else
                        {
                            ReadString(ref i, true);
                        }
                        break;


                    default:
                        i += 2;
                        break;
                }
            }
            else
            {
                i += 2;
            }
        }

        private static bool IsParent(string[] parents, int levelsUp, string text)
        {
            if (parents.Length < 1) return false;
            if (levelsUp > parents.Length || levelsUp == 0) return false;
            if (levelsUp < 0) return text == parents[0];
            return parents[parents.Length - levelsUp] == text;
        }

        private static bool TryAddSpecials(ref int i)
        {
            SpecialCode handler = GetSpecialCode(i);
            if (handler == SpecialCode.RightBrace)
            {
                map.AddContainerEnd();
                i += 2;
                return false;
            }
            else if (handler == SpecialCode.LeftBrace)
            {
                map.AddContainerStart(true);
                i += 2;
                return true;
            }
            else if (handler == SpecialCode.Equals)
            {
                // Peek for incoming leftbrace
                SpecialCode next = GetSpecialCode(i + 2);
                if (next == SpecialCode.LeftBrace)
                {
                    map.AddContainerStart(false);
                    i += 4;
                    return true;
                }
                else
                {
                    map.AddAssignment();
                    i += 2;
                    return false;
                }
            }
            return false;
        }

        private static void TryReadTypeDefinition(ref int i, ref SpecialCode type)
        {
            SpecialCode specialcode = GetSpecialCode(i);
            if ((int)specialcode < 4) return;

            // ignore this particular switch
            if (!(specialcode == SpecialCode.Integer && type == SpecialCode.Date))
            {
                type = specialcode;
            }
            i += 2;
        }

        private static void ReadSpecial(ref int i)
        {
            SpecialCode handler = GetSpecialCode(i);
            if (handler == SpecialCode.RightBrace)
            {
                map.AddContainerEnd();
                i += 2;
            }
            else if (handler == SpecialCode.LeftBrace)
            {
                map.AddContainerStart(true);
                i += 2;
            }
            else if (handler == SpecialCode.Equals)
            {
                // Peek
                SpecialCode next = GetSpecialCode(i + 2);
                if (next == SpecialCode.LeftBrace)
                {
                    map.AddContainerStart(false);
                    i += 4;
                }
                else
                {
                    map.AddAssignment();
                    i += 2;
                }
            }
        }

        private static void ReadString(ref int i, bool quoted)
        {
            if (i > raw.Length - 2) return;
            int length = (ushort)(raw[i + 1] << 8 | raw[i]);
            string text = i + 2 + length > raw.Length ? "#ERROR: String exceeds end of file" : ANSI.GetString(raw, i + 2, length);
            map.AddString(text.Trim(), quoted);
            i += 2 + length;
        }

        private static void ReadInteger(ref int i)
        {
            map.AddInteger(BitConverter.ToInt32(raw, i).ToString());
            i += 4;
        }

        private static void ReadFloat(ref int i)
        {
            map.AddFloat(String.Format("{0:0.000}", BitConverter.ToInt32(raw, i) / 1000f));
            i += 4;
        }

        private static void ReadFloat5(ref int i)
        {
            map.AddFloat5(String.Format("{0:0.00000}", BitConverter.ToInt32(raw, i) * 2 / 256f / 256f));
            i += 8;
        }

        private static void ReadBoolean(ref int i)
        {
            map.AddBoolean(BitConverter.ToBoolean(raw, i) ? "yes" : "no");
            i += 1;
        }

        private static void ReadDate(ref int i, bool quoted)
        {
            map.AddDate(DecodeDate(BitConverter.ToInt32(raw, i)), quoted);
            i += 4;
        }

        private static string DecodeDate(int input)
        {
            int[] monthLength = new int[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

            int hour = input % 24;
            int year = -5000 + input / 24 / 365;
            int day = 1 + input / 24 % 365;
            int month = 1;

            for (int i = 0; i < monthLength.Length; i++)
            {
                if (day > monthLength[i])
                {
                    day -= monthLength[i];
                    month++;
                }
                else
                {
                    break;
                }
            }

            return year + "." + month + "." + day;
        }
    }

    public enum SpecialCode
    {
        None, Equals, LeftBrace, RightBrace, Integer, Float, Boolean, String, Float5, Date, Variable
    }
}
