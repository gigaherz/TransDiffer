using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    class DialogDefinition : ResourceStatement, ITranslationEntry
    {
        public Token EntryType { get; set; }

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }

        public List<DialogControl> Entries { get; } = new List<DialogControl>();
    }
}