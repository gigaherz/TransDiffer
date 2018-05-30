
namespace TransDiffer.Parser
{
    public class Token : IContextProvider
    {
        public readonly Tokens Name;
        public readonly string Text;
        public readonly ParsingContext Context;
        public readonly ParsingContext ContextEnd;

        public Token(Tokens name, ParsingContext context, string text, ParsingContext contextEnd)
        {
            Name = name;
            Text = text;
            Context = context;
            ContextEnd = contextEnd;
        }

        public Token(Tokens newName, Token copyOf)
        {
            Name = newName;
            Text = copyOf.Text;
            Context = copyOf.Context;
            ContextEnd = copyOf.ContextEnd;
        }

        public override string ToString()
        {
            if (Text == null)
                return $"({Name} @ {Context.Line}:{Context.Column})";

            if (Text.Length > 22)
                return $"({Name} @ {Context.Line}:{Context.Column}: {Text.Substring(0, 20)}...)";

            return $"({Name} @ {Context.Line}:{Context.Column}: {Text})";
        }

        public ParsingContext GetParsingContext()
        {
            return Context;
        }
    }
}
