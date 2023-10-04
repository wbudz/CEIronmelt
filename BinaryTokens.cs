using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{

    internal class BinaryTokens
    {
        Dictionary<ushort, BinaryToken> codes = new Dictionary<ushort, BinaryToken>();

        internal BinaryTokens(string path)
        {
            string[] src = File.ReadAllLines(path);
            for (int i = 1; i < src.Length; i++)
            {
                if (src[i].Length <= 14) continue; // empty code
                string hexCode = src[i].Substring(2, 4);
                ushort intCode;
                UInt16.TryParse(hexCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out intCode);
                if (intCode != 0 && !codes.ContainsKey(intCode))
                    codes.Add(intCode, new BinaryToken(src[i]));
            }
        }

        public BinaryToken TryGetCode(byte b1, byte b2)
        {
            return TryGetCode((ushort)(b1 << 8 | b2));
        }

        public BinaryToken TryGetCode(ushort code)
        {
            BinaryToken output;
            codes.TryGetValue(code, out output);
            return output;
        }

        public bool IsACode(ushort code)
        {
            return codes.ContainsKey(code);
        }
    }

    public class BinaryToken
    {
        public string Text = "";
        public SpecialCode DataType = SpecialCode.None;
        public bool Quoted = false;
        public bool InheritType = false;
        public bool? List = null;

        public BinaryToken(string input)
        {
            string[] fields = input.Split(';');
            if (fields.Length < 6) return;
            Text = fields[1];
            DataType = GetDataType(fields[2].ToLowerInvariant());
            if (fields[3].Trim() == "1") Quoted = true;
            if (fields[4].Trim() == "1") InheritType = true;
            if (fields[5].Trim() == "-1") List = false; else if (fields[5].Trim() == "1") List = true; else List = null;
        }

        public override string ToString()
        {
            return Text;
        }

        public SpecialCode GetDataType(string input)
        {

            switch (input)
            {
                case "string": return SpecialCode.String;
                case "integer": return SpecialCode.Integer;
                case "float": return SpecialCode.Float;
                case "float5": return SpecialCode.Float5;
                case "date": return SpecialCode.Date;
                case "boolean": return SpecialCode.Boolean;
                case "variable": return SpecialCode.Variable;
                default: return SpecialCode.None;
            }
        }
    }
}
