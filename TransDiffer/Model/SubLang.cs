using System.Collections.Generic;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class SubLang
    {
        public string Neutral { get; set; }
        public LangFile Source { get; set; }
        public string Name { get; set; }

        public Dictionary<string, TranslationStringReference> NamedStrings { get; } = new Dictionary<string, TranslationStringReference>();
        public List<TranslationStringReference> References { get; } = new List<TranslationStringReference>();

        public TranslationStringReference AddNamedString(string prefix, LangFile file, ITranslationEntry entry, ref Dictionary<string,int> unnamedCount, string clang)
        {
            var sl = CreateNamedString(prefix, file, entry, ref unnamedCount, clang);
            NamedStrings.Add(sl.Id, sl);
            References.Add(sl);
            if(!file.ContainedLangs.Contains(this))
                file.ContainedLangs.Add(this);
            file.NamedLines.Add(entry.Context.Line, sl);
            return sl;
        }

        public TranslationStringReference CreateNamedString(string _prefix, LangFile file, ITranslationEntry entry, ref Dictionary<string, int> unnamedCount, string clang)
        {
            var prefix = _prefix.Length > 0 ? _prefix + "_" : "";
            var id = entry.Identifier?.Process();
            var idNumbered = id;
            var type = string.IsNullOrWhiteSpace(entry.EntryType?.Text) ? "UNNAMED" : entry.EntryType.Text;

            if (string.IsNullOrEmpty(id) || id == "-1" || id == "IDC_STATIC")
            {
                var type_id = $"{type}_{id}";
                int count;
                unnamedCount.TryGetValue(type_id, out count);
                id = $"{type}_{id}_{count++}";
                idNumbered = $"{prefix}{id}#0";
                unnamedCount[type] = count + 1;
            }

            int number = 0;
            while (NamedStrings.ContainsKey(idNumbered))
            {
                number++;
                idNumbered = $"{prefix}{id}#{number}";
            }

            return new TranslationStringReference { Id = idNumbered, Language = clang, Source = file, Entry = entry };
        }

        public void FinishLoading()
        {
            for (var i = 0; i < References.Count; i++)
            {
                var ns = References[i];
                if (i > 0)
                    ns.Previous = References[i - 1];
                if (i+1 < References.Count)
                    ns.Next = References[i + 1];
            }
        }
    }
}