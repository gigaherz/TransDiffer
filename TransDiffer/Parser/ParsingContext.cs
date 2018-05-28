
using System;

namespace TransDiffer.Parser
{
    public class ParsingContext : IComparable<ParsingContext>
    {
        public readonly string Filename;
        public readonly int Offset;
        public readonly int Line;
        public readonly int Column;

        public ParsingContext(string f, int of, int l, int c)
        {
            Filename = f;
            Offset = of;
            Line = l;
            Column = c;
        }

        public override string ToString()
        {
            return $"{Filename}({Line},{Column})";
        }

        public int CompareTo(ParsingContext other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Offset.CompareTo(other.Offset);
        }
    }
}
