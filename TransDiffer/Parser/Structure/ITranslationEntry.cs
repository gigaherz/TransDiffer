namespace TransDiffer.Parser.Structure
{
    public interface ITranslationEntry
    {
        ParsingContext Context { get; }
        Token EntryType { get; }

        ExpressionValue Identifier { get; }
        Token TextValue { get; }
    }
}