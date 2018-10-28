namespace TransDiffer.Parser.Structure
{
    class DialogControl : ITranslationEntry
    {
        public ParsingContext Context { get; set; }
        public Token EntryType { get; set; }

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }

        public string GenericControlType { get; set; } = string.Empty;
        public System.Windows.Rect Dimensions;
        public ExpressionValue Style { get; set; }
    }
}