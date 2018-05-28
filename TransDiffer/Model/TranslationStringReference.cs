using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class TranslationStringReference
    {
        public LangFile Source { get; set; }
        public ParsingContext Context { get; set; }
        public ExpressionValue IdentifierToken { get; set; }
        public Token TextValueToken { get; set; }

        public string Id { get; set; }
        public string Language { get; set; }

        // Results of the comparison
        public TranslationString String { get; set; }

        public override string ToString()
        {
            return $"{{{IdentifierToken.Process()}={Lexer.UnescapeString(TextValueToken)}}}";
        }
    }
}