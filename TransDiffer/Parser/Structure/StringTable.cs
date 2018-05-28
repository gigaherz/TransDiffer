using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDDL;
using GDDL.Structure;

namespace TransDiffer.Parser.Structure
{
    class StringTable : ResourceStatement
    {
        public List<StringTableEntry> Entries { get; } = new List<StringTableEntry>();
    }
}
