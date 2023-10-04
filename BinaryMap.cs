using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEParser
{
    public class BinaryMap
    {
        BinaryTokens tokens;
        List<BinaryBlockType> BlockMap;
        public List<string> TextMap { get; private set; }

        Dictionary<int, int> delimiters = new Dictionary<int, int>();
        Stack<int> delimiterStack = new Stack<int>();
        List<string> parents = new List<string>();

        internal BinaryMap(int length, BinaryTokens tokens)
        {
            BlockMap = new List<BinaryBlockType>(); // (int)(length / 1000f)
            TextMap = new List<string>();
            BlockMap.Add(BinaryBlockType.Start);
            TextMap.Add("");
            this.tokens = tokens;
        }

        internal void AddIntroduction(string text)
        {
            BlockMap.Add(BinaryBlockType.UnquotedString);
            TextMap.Add(text);
        }

        internal void AddContainerStart(bool nameless)
        {
            if (nameless)
            {
                BlockMap.Add(BinaryBlockType.NamelessContainerStart);
                TextMap.Add("{");
                delimiterStack.Push(BlockMap.Count - 1);
                parents.Add("");
            }
            else
            {
                BlockMap.Add(BinaryBlockType.ContainerStart);
                TextMap.Add("={");
                delimiterStack.Push(BlockMap.Count - 1);
                parents.Add(TextMap[TextMap.Count - 2] ?? "");
            }
        }

        internal void AddContainerEnd()
        {
            BlockMap.Add(BinaryBlockType.ContainerEnd);
            TextMap.Add("}");
            if (delimiterStack.Count > 0)
            {
                int startDelimiter = delimiterStack.Pop();
                delimiters.Add(startDelimiter, BlockMap.Count - 1);
            }
            if (parents.Count > 0) parents.RemoveAt(parents.Count - 1);
        }

        internal void AddAssignment()
        {
            BlockMap.Add(BinaryBlockType.Assignment);
            TextMap.Add("=");
        }

        internal void AddString(string text, bool quoted)
        {
            BlockMap.Add(quoted ? BinaryBlockType.QuotedString : BinaryBlockType.UnquotedString);
            TextMap.Add(text);
        }

        internal void AddDate(string text, bool quoted)
        {
            BlockMap.Add(quoted ? BinaryBlockType.QuotedDate : BinaryBlockType.UnquotedDate);
            TextMap.Add(text);
        }

        internal void AddInteger(string text)
        {
            BlockMap.Add(BinaryBlockType.Integer);
            TextMap.Add(text);
        }

        internal void AddFloat(string text)
        {
            BlockMap.Add(BinaryBlockType.Float);
            TextMap.Add(text);
        }

        internal void AddFloat5(string text)
        {
            BlockMap.Add(BinaryBlockType.Float5);
            TextMap.Add(text);
        }

        internal void AddBoolean(string text)
        {
            BlockMap.Add(BinaryBlockType.Boolean);
            TextMap.Add(text);
        }

        internal void AddToken(string text)
        {
            BlockMap.Add(BinaryBlockType.Token);
            TextMap.Add(text);
        }

        internal void Finish()
        {
            BlockMap.Add(BinaryBlockType.End);
            TextMap.Add("");
        }

        public Dictionary<int, int> GetDelimiters()
        {
            return delimiters;
        }

        public string GetText(int i)
        {
            return TextMap[i];
        }

        public string[] GetParents()
        {
            return parents.ToArray();
        }

        public string ExportToText()
        {
            Encoding ANSI = Encoding.GetEncoding(1250);
            StringBuilder sb = new StringBuilder();

            int depth = 0;
            bool isFirstInsideContainer = false;

            sb.AppendLine(TextMap[1]);

            for (int i = 2; i < BlockMap.Count - 1; i++) // first and last blocks are not printable
            {
                if (BlockMap[i] == BinaryBlockType.Assignment)
                {
                    sb.Append(TextMap[i]);
                }
                else if (BlockMap[i] == BinaryBlockType.ContainerStart)
                {
                    depth++;
                    sb.Append(TextMap[i]);
                    isFirstInsideContainer = true;
                }
                else if (BlockMap[i] == BinaryBlockType.NamelessContainerStart)
                {
                    sb.AppendLine();
                    CreateDepth(sb, ++depth);
                    sb.Append(TextMap[i]);
                    isFirstInsideContainer = true;
                }
                else if (BlockMap[i] == BinaryBlockType.ContainerEnd)
                {
                    if (!isFirstInsideContainer)
                    {
                        sb.AppendLine();
                    }
                    CreateDepth(sb, --depth);
                    sb.Append(TextMap[i]);
                }
                else
                {
                    PrepareNewBlock(sb, depth, BlockMap[i - 1], BlockMap[i + 1], isFirstInsideContainer, false);
                    if (BlockMap[i] == BinaryBlockType.QuotedString || BlockMap[i] == BinaryBlockType.QuotedDate)
                    {
                        sb.Append("\"");
                        sb.Append(TextMap[i]);
                        sb.Append("\"");
                    }
                    else
                    {
                        sb.Append(TextMap[i]);
                    }
                    isFirstInsideContainer = false;
                }
            }

            return sb.ToString();
        }

        private void PrepareNewBlock(StringBuilder sb, int depth, BinaryBlockType prev, BinaryBlockType next, bool isFirstInsideContainer, bool isInsideListContainer)
        {
            if ((!isFirstInsideContainer && (next == BinaryBlockType.Assignment || isInsideListContainer)) ||
                next == BinaryBlockType.ContainerStart)
            {
                sb.AppendLine();
                CreateDepth(sb, depth);
            }
            else if (isFirstInsideContainer)
            {
                sb.AppendLine();
                CreateDepth(sb, depth);
            }
            else
            {
                if (prev == BinaryBlockType.QuotedDate || prev == BinaryBlockType.QuotedString ||
                    prev == BinaryBlockType.ContainerStart || prev == BinaryBlockType.NamelessContainerStart)
                {
                    sb.AppendLine();
                    CreateDepth(sb, depth);
                }
                else if (prev != BinaryBlockType.Assignment)
                {
                    sb.Append(" ");
                }
            }
        }

        private void CreateDepth(StringBuilder sb, int depth)
        {
            for (int j = 0; j < depth && j < 20; j++) sb.Append("\t");
        }

        public int Count
        {
            get
            {
                return BlockMap.Count;
            }
        }

    }

    public enum BinaryBlockType
    {
        Unspecified, Start, End,
        Assignment, NamelessContainerStart, ContainerStart, ContainerEnd,
        UnquotedString, QuotedString, Integer, Float, Float5, UnquotedDate, QuotedDate, Boolean,
        Token
    }

}
