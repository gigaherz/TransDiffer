using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    class Font
    {
        public string Name;
        public float Size;
    }

    class DialogDefinition : ResourceStatement, ITranslationEntry
    {
        public Token EntryType { get; set; }

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }

        public System.Windows.Rect Dimensions { get; set; }
        public Font Font { get; set; } = new Font() { Name = "Segoe UI", Size = 12 };
        public ExpressionValue Style { get; set; }
        public ExpressionValue ExStyle { get; set; }

        public List<DialogControl> Entries { get; } = new List<DialogControl>();
    }
}