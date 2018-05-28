using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    class DialogDefinition : ResourceStatement
    {
        public Token Identifier { get; set; }
        public Token Caption { get; set; }
        public List<DialogControl> Entries { get; } = new List<DialogControl>();
        // Style, Font etc not needed for our purposes
    }
}