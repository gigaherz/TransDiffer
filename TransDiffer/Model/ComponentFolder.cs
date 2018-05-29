using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TransDiffer.Annotations;
using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class ComponentFolder : IExpandable
    {
        private bool _isExpanded;

        public DirectoryInfo Root { get; set; }
        public DirectoryInfo Directory { get; set; }

        public string Path => Directory.FullName;
        public string Name => Directory.FullName.Substring(Root.FullName.Length);
        public bool HasErrors => Files.Any(f => f.HasErrors);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LangFile> Files { get; } = new ObservableCollection<LangFile>();

        public Dictionary<string, SubLang> SubLangs { get; } = new Dictionary<string, SubLang>();

        public ObservableCollection<TranslationString> NamedStrings { get; } = new ObservableCollection<TranslationString>();
        public Dictionary<string, TranslationString> NamedStringsByName { get; } = new Dictionary<string, TranslationString>();

        public void ScanContents()
        {
            Debug.WriteLine($"Parsing {Name}...");

            foreach (var lang in Files)
            {
                ParseLang(lang);
            }

            ProcessComparisons();

            IsExpanded = HasErrors;
        }

        private void ProcessComparisons()
        {
            var sl = SubLangs.Keys.ToList();
            foreach(var ns in NamedStrings)
            {
                var st = new HashSet<string>();
                foreach(var str in ns.Lines)
                {
                    st.Add(str.Language);
                }
                foreach(var lang in sl.Except(st))
                {
                    var sll = SubLangs[lang];
                    if (sll.Neutral == sll.Name || !ns.Translations.ContainsKey(sll.Neutral))
                    {
                        ns.MissingInLanguages.Add(sll);
                    }
                }
            }

            foreach (var file in Files)
            {
                file.FinishLoading();
            }
        }
        
        private void ParseLang(LangFile file)
        {
            var parser = Parser.Parser.FromFile(file.File.FullName);
            var rc = parser.Parse();
            var currentLanguage = "NEUTRAL";
            var currentNeutralLanguage = "NEUTRAL";
            var topLevelUnnamed = 0;
            foreach (var def in rc.Definition.Where(s => !(s is ParseErrorRecovery)))
            {
                if (def is LanguageStatement s)
                {
                    var lang = s.Lang.Text.Substring("LANG_".Length);
                    var sublang = s.SubLang?.Text.Substring("SUBLANG_".Length) ?? currentLanguage;
                    switch (sublang)
                    {
                    case "NEUTRAL":
                        currentLanguage = lang;
                        break;
                    case "DEFAULT":
                        currentLanguage = $"{lang}_DEFAULT";
                        break;
                    default:
                        currentLanguage = sublang;
                        break;
                    }
                    currentNeutralLanguage = lang;
                }
                else if(def is MenuDefinition md)
                {
                    var unnamedCount = 0;
                    var prefix = md.Identifier.Process();
                    foreach(var entry in md.Entries)
                    {
                        ProcessMenuItem(prefix, file, entry, ref unnamedCount, currentLanguage, currentNeutralLanguage, null);
                    }
                }
                else if (def is DialogDefinition dd)
                {
                    var parent = AddNamedString("", file, dd.Identifier, dd.Caption, dd.Context, ref topLevelUnnamed, currentLanguage, currentNeutralLanguage, null);

                    var unnamedCount = 0;
                    var prefix = dd.Identifier.Process();
                    foreach (var entry in dd.Entries)
                    {
                        ProcessDialogControl(prefix, file, entry, ref unnamedCount, currentLanguage, currentNeutralLanguage, parent);
                    }
                }
                else if (def is StringTable st)
                {
                    var unnamedCount = 0;
                    var prefix = "";
                    foreach (var entry in st.Entries)
                    {
                        ProcessStringTableEntry(prefix, file, entry, ref unnamedCount, currentLanguage, currentNeutralLanguage);
                    }
                }
            }
        }

        private void ProcessDialogControl(string prefix, LangFile file, DialogControl dc, ref int unnamedCount, string clang, string nlang, TranslationStringReference parent)
        {
            AddNamedString(prefix, file, dc.IdentifierToken, dc.ValueToken, dc.Context, ref unnamedCount, clang, nlang, parent);
        }

        private void ProcessStringTableEntry(string prefix, LangFile file, StringTableEntry dc, ref int unnamedCount, string clang, string nlang)
        {
            AddNamedString(prefix, file, dc.IdentifierToken, dc.ValueToken, dc.Context, ref unnamedCount, clang, nlang, null);
        }

        private void ProcessMenuItem(string prefix, LangFile file, MenuItemDefinition mi, ref int unnamedCount, string clang, string nlang, TranslationStringReference parent)
        {
            var ns = AddNamedString(prefix, file, mi.IdentifierToken, mi.ValueToken, mi.Context, ref unnamedCount, clang, nlang, parent);

            var unnamedCount2 = 0;
            prefix = $"{prefix}_{mi.IdentifierToken?.Process()}";
            foreach (var entry in mi.Entries)
            {
                ProcessMenuItem(prefix, file, entry, ref unnamedCount2, clang, nlang, ns);
            }
        }

        private TranslationStringReference AddNamedString(string prefix, LangFile file, ExpressionValue identifier, Token valueToken, ParsingContext context, ref int unnamedCount, string clang, string nlang, TranslationStringReference parent)
        {
            SubLang sl;
            if (!SubLangs.TryGetValue(clang, out sl))
            {
                sl = new SubLang() { Source = file, Name = clang, Neutral = nlang };
                SubLangs.Add(clang, sl);
            }

            var stl = sl.AddNamedString(prefix, file, identifier, valueToken, context, ref unnamedCount, clang);

            if (!NamedStringsByName.TryGetValue(stl.Id, out var ns))
            {
                ns = new TranslationString { Name = stl.Id, Parent = parent?.String };
                NamedStrings.Add(ns);
                NamedStringsByName.Add(stl.Id, ns);
            }

            ns.Lines.Add(stl);
            ns.Translations.Add(stl.Language, stl);
            stl.String = ns;

            return stl;
        }

        public void Scan(DirectoryInfo dir)
        {
            foreach (var lang in dir.GetFiles("*.rc"))
            {
                Files.Add(new LangFile() { Folder = this, File = lang });
            }

            ScanContents();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}