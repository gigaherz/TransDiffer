using System.Collections.Generic;
using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class SubLang
    {
        public string Neutral { get; set; }
        public LangFile Source { get; set; }
        public string Name { get; set; }

        public Dictionary<string, TranslationStringReference> NamedStrings { get; } = new Dictionary<string, TranslationStringReference>();

        public TranslationStringReference AddNamedString(string prefix, LangFile file, ExpressionValue identifier, Token valueToken, ParsingContext context, ref int unnamedCount, string clang)
        {
            var sl = CreateNamedString(prefix, file, identifier, valueToken, context, ref unnamedCount, clang);
            NamedStrings.Add(sl.Id, sl);
            if(!file.ContainedLangs.Contains(this))
                file.ContainedLangs.Add(this);
            file.NamedLines.Add(context.Line, sl);
            return sl;
        }

        public TranslationStringReference CreateNamedString(string _prefix, LangFile file, ExpressionValue identifier, Token valueToken, ParsingContext context, ref int unnamedCount, string clang)
        {
            var prefix = _prefix.Length > 0 ? _prefix + "_" : "";
            var id = identifier?.Process();
            var idNumbered = id;

            if (string.IsNullOrEmpty(id) || id == "-1" || id == "IDC_STATIC")
            {
                id = $"UNNAMED_{id}_{unnamedCount++}";
                idNumbered = $"{prefix}{id}#0";
            }

            int number = 0;
            while (NamedStrings.ContainsKey(idNumbered))
            {
                number++;
                idNumbered = $"{prefix}{id}#{number}";
            }

            return new TranslationStringReference() { Id = idNumbered, Language = clang, Source = file, Context = context, IdentifierToken = identifier, TextValueToken = valueToken };
        }
    }
}