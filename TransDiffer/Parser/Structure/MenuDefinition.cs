using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    class MenuDefinition : ResourceStatement
    {
        public Token Identifier { get; set; }
        public List<MenuItemDefinition> Entries { get; } = new List<MenuItemDefinition>();
        // Style, Font etc not needed for our purposes
    }
    class MenuItemDefinition
    {
        public ParsingContext Context { get; set; }

        public ExpressionValue IdentifierToken { get; set; }
        public Token ValueToken { get; set; }

        public List<MenuItemDefinition> Entries { get; } = new List<MenuItemDefinition>();
    }
}