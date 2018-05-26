using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TransDiffer
{
    public class SubLang
    {
        public string Neutral { get; set; }
        public LangFile Source { get; set; }
        public string Name { get; set; }

        public Dictionary<string, TranslationStringReference> NamedStrings { get; } = new Dictionary<string, TranslationStringReference>();

        public TranslationStringReference AddNamedString(Match match, string lang, LangFile file, int lineNumber, ref int unnamedCount, string idPrefix, Match contextMatch = null)
        {
            var sl = CreateNamedString(match, lang, file, lineNumber, ref unnamedCount, idPrefix, contextMatch);
            NamedStrings.Add(sl.Id, sl);
            file.NamedLines.Add(lineNumber, sl);
            return sl;
        }

        public TranslationStringReference CreateNamedString(Match match, string lang, LangFile file, int lineNumber, ref int unnamedCount, string idPrefix, Match contextMatch = null)
        {
            var prefix = idPrefix.Length > 0 ? idPrefix + "_" : "";
            var id = match.Groups["id"].Value;
            var idNumbered = id;

            if (string.IsNullOrEmpty(id) || id == "-1" || id == "IDC_STATIC" || !match.Groups["id"].Success)
            {
                if (contextMatch == null)
                {
                    id = $"UNNAMED_{id}_{unnamedCount++}";
                    idNumbered = $"{prefix}{id}#0";
                }
                else
                {
                    id = contextMatch.Groups["id"].Value;
                    idNumbered = $"{id}";

                    if (string.IsNullOrEmpty(id) || id == "-1" || id == "IDC_STATIC" || !contextMatch.Groups["id"].Success)
                    {
                        id = $"UNNAMED_{id}_{unnamedCount++}";
                        idNumbered = $"{prefix}{id}#0";
                    }
                }
            }

            int number = 0;
            while (NamedStrings.ContainsKey(idNumbered))
            {
                number++;
                idNumbered = $"{prefix}{id}#{number}";
            }

            return new TranslationStringReference() { Id = idNumbered, Language = lang, Source = file, LineNumber = lineNumber, RegexMatch = match };
        }
    }
}