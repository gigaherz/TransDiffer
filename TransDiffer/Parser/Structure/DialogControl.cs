namespace TransDiffer.Parser.Structure
{
    class DialogControl
    {
        public ParsingContext Context { get; set; }

        public ExpressionValue IdentifierToken { get; set; }
        public Token ValueToken { get; set; }
    }
}