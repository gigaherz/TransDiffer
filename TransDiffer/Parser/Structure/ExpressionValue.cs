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
            return Context.CompareTo(token?.Context);
        }
    }
}
