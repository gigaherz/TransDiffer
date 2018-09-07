namespace TransDiffer.Parser.Structure
{
    class DialogControl : ITranslationEntry
    {
        public ParsingContext Context { get; set; }
        public Token EntryType { get; set; }

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }
    }
}