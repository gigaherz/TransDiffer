using System.Collections.Generic;
using System.Windows.Documents;
using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class TranslationStringReference
    {
        public LangFile Source { get; set; }
        public ITranslationEntry Entry { get; set; }

        public ParsingContext Context => Entry.Context;
        public Token EntryType => Entry.EntryType;
        public ExpressionValue Identifier => Entry.Identifier;
        public Token TextValue => Entry.TextValue;

        public string Id { get; set; }
        public string Language { get; set; }

        // Results of the comparison
        public TranslationString String { get; set; }

        public List<FileLineItem> Paragraphs { get; } = new List<FileLineItem>();

        public TranslationStringReference Previous { get; set; }
        public TranslationStringReference Next { get; set; }

        public override string ToString()
        {
            return $"{{{Identifier.Process()}={Lexer.UnescapeString(TextValue)}}}";
        }
    }
}