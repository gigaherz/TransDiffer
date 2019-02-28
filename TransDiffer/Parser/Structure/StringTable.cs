using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    internal class StringTable : ResourceStatement
    {
        public List<StringTableEntry> Entries { get; } = new List<StringTableEntry>();
    }
}
