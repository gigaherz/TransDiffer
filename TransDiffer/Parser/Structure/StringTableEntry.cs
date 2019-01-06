namespace TransDiffer.Parser.Structure
{
    internal class StringTableEntry : ITranslationEntry
    {
        public ParsingContext Context { get; set; }
        public Token EntryType => null;

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }
    }
}