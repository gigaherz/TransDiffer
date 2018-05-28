namespace TransDiffer.Parser.Structure
{
    class StringTableEntry
    {
        public ParsingContext Context { get; set; }

        public ExpressionValue IdentifierToken { get; set; }
        public Token ValueToken { get; set; }
    }
}