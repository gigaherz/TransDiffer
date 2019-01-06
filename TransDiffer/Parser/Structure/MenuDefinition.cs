﻿using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    internal class MenuDefinition : ResourceStatement
    {
        public ExpressionValue Identifier { get; set; }

        public List<MenuItemDefinition> Entries { get; } = new List<MenuItemDefinition>();
    }

    internal class MenuItemDefinition : ITranslationEntry
    {
        public ParsingContext Context { get; set; }
        public Token EntryType { get; set; }

        public ExpressionValue Identifier { get; set; }
        public Token TextValue { get; set; }

        public List<MenuItemDefinition> Entries { get; } = new List<MenuItemDefinition>();
    }
}