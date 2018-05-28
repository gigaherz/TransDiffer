namespace TransDiffer.Parser.Structure
{
    public class ParseErrorRecovery : ResourceStatement
    {
        public ParseErrorRecovery(Token token)
        {
            Context = token.Context;
        }
    }
}