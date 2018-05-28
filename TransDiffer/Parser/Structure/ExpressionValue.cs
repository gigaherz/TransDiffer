using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransDiffer.Parser.Structure
{
    public class ExpressionValue
    {
        public ParsingContext Context { get; set; }
        public ParsingContext ContextEnd { get; set; }
        public List<Token> Tokens { get; } = new List<Token>();

        public string Process()
        {
            return string.Join("", Tokens.Select(t => t.Text));
        }

        public int CompareTo(Token token)
        {
            if (token == null)
                return -1;
            int line = Math.Sign(Context.Line - token.Context.Line);
            if (line != 0)
                return line;
            return Math.Sign(Context.Column - token.Context.Column);
        }
    }
}
