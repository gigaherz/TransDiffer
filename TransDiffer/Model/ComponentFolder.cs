using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace TransDiffer
{
    public class ComponentFolder
    {
        enum ParseState
        {
            Root,
            Menu,
            Popup,
            Dialog,
            StringTable,
            Unhandled
        }

        public DirectoryInfo Root { get; set; }
        public DirectoryInfo Directory { get; set; }

        public string Path => Directory.FullName;
        public string Name => Directory.FullName.Substring(Root.FullName.Length);
        public bool HasErrors => Files.Any(f => f.HasErrors);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }

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

        private static readonly Regex rxLanguage = new Regex("^[ \t]*LANGUAGE[, \t]+LANG_(?<lang>[a-zA-Z0-9_]+)(?:[, \t]+SUBLANG_(?<slang>[a-zA-Z0-9_]+))?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxBegin = new Regex("^[ \t]*(?:BEGIN|{)[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxEnd = new Regex("^[ \t]*(?:END|})[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxMenuEx = new Regex("^[ \t]*(?<id>[a-zA-Z0-9_]+)[, \t]+MENU(?:EX)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxMenuItem = new Regex("^[ \t]*MENUITEM[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+(?<id>[a-zA-Z0-9_]+)(?:[, \t]+.*)?)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxPopup = new Regex("^[ \t]*POPUP[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+(?:BEGIN|(?<id>[a-zA-Z0-9_]+))(?:[, \t]+.*)?)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxStringTable = new Regex("^[ \t]*STRINGTABLE[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxStringTableEntry = new Regex("^[ \t]*(?<id>[a-zA-Z0-9_]+)[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxCaption = new Regex("^[ \t]*CAPTION[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+.*)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxDialogEx = new Regex("^[ \t]*(?<id>[a-zA-Z0-9_]+)[, \t]+DIALOG(?:EX)?(?:[, \t]+.*)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxDialogControl = new Regex("^[ \t]*[a-zA-Z0-9_]+[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+(?<id>[a-zA-Z0-9_]+)(?:[, \t]+.*)?)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private void ParseLang(LangFile file)
        {
            string allText = File.ReadAllText(file.File.FullName);

            List<string> lines = new List<string>();

            string tmpLine = "";
            int st = 0;
            for (int i = 0; i < allText.Length; i++)
            {
                int c = allText[i];
                int c1 = (i + 1) < allText.Length ? allText[i + 1] : -1;
                int c2 = (i + 2) < allText.Length ? allText[i + 2] : -1;
                if (st == 0) // normal
                {
                    if (c == '/')
                    {
                        if (c1 == '/') { st = 1; }
                        else if (c1 == '*') { st = 2; i++; }
                    }
                    else if (c == '\\' && c1 == '\n') { tmpLine = tmpLine + (char)c + (char)c1; i++; }
                    else if (c == '\\' && c1 == '\r' && c2 == '\n') { tmpLine = tmpLine + (char)c + (char)c1 + (char)c2; i += 2; }
                    else if (c == '"') { st = 3; tmpLine += (char)c; }
                    else if (c == '\n') { lines.Add(tmpLine); tmpLine = ""; }
                    else if (c == '\r' && c1 == '\n') { lines.Add(tmpLine); tmpLine = ""; i++; }
                    else { tmpLine += (char)c; }
                }
                else if (st == 1) // single-line comment
                {
                    if (c == '\n') { st = 0; lines.Add(tmpLine); tmpLine = ""; }
                    else if (c == '\r') { st = 0; if (c1 == '\n') i++; lines.Add(tmpLine); tmpLine = ""; }
                }
                else if (st == 2) // multi-line comment
                {
                    if (c == '*' && c1 == '/') { st = 0; }
                }
                else if (st == 3)
                {
                    if (c == '"' && c1 == '"') { tmpLine += "\"\""; i++; }
                    else if (c == '"' && c1 != '"') { tmpLine += '"'; st = 0; }
                    else if (c == '\\') { tmpLine = tmpLine + (char)c + (char)c1; i++; }
                    else { tmpLine += (char)c; }
                }
            }

            file.Content = lines.ToArray();

            int unnamedCount = 0;
            string idPrefix = "";
            string idPrefixTemp = "";
            ParseState state = ParseState.Root;
            ParseState nextState = ParseState.Unhandled;
            Stack<ParseState> stateStack = new Stack<ParseState>();
            Stack<int> unnamedStack = new Stack<int>();
            Stack<string> idPrefixStack = new Stack<string>();

            string clang = "ENGLISH_US";
            string nlang = "ENGLISH";

            Match match;
            Match contextMatch = null;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if ((match = rxLanguage.Match(line)).Success)
                {
                    var sub = match.Groups["slang"];
                    nlang = match.Groups["lang"].Value;
                    if (sub.Success && sub.Value != "DEFAULT" && sub.Value != "NEUTRAL")
                    {
                        clang = sub.Value;
                    }
                    else
                    {
                        clang = nlang;
                    }
                    if (clang == "DEFAULT")
                        clang = "ENGLISH_US";
                }
                else if ((match = rxBegin.Match(line)).Success)
                {
                    stateStack.Push(state);
                    state = nextState;
                    nextState = ParseState.Unhandled;

                    unnamedStack.Push(unnamedCount);
                    unnamedCount = 0;

                    idPrefixStack.Push(idPrefix);
                    idPrefix = idPrefixTemp;
                }
                else if ((match = rxEnd.Match(line)).Success)
                {
                    if (stateStack.Count == 0)
                    {
                        Debug.WriteLine("ERROR! Unbalanced END");
                    }
                    else
                    {
                        state = stateStack.Pop();
                        unnamedCount = unnamedStack.Pop();
                        idPrefixTemp = idPrefix;
                        idPrefix = idPrefixStack.Pop();
                    }
                    nextState = ParseState.Unhandled;
                }
                else
                {
                    switch (state)
                    {
                    case ParseState.Root:
                        if ((match = rxMenuEx.Match(line)).Success)
                        {
                            nextState = ParseState.Menu;
                        }
                        else if ((match = rxStringTable.Match(line)).Success)
                        {
                            nextState = ParseState.StringTable;
                        }
                        else if ((match = rxDialogEx.Match(line)).Success)
                        {
                            contextMatch = match; // for CAPTION
                            nextState = ParseState.Dialog;
                            idPrefixTemp = idPrefix.Length > 0 
                                ? $"{idPrefix}_{contextMatch.Groups["id"].Value}" 
                                : $"{contextMatch.Groups["id"].Value}";
                        }
                        else if ((match = rxCaption.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, idPrefixTemp, contextMatch);
                        }
                        break;
                    case ParseState.StringTable:
                        if ((match = rxStringTableEntry.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, idPrefixTemp);
                        }
                        break;
                    case ParseState.Menu:
                        if ((match = rxMenuItem.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, idPrefixTemp);
                        }
                        else if ((match = rxPopup.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, idPrefixTemp);
                            nextState = ParseState.Menu;
                        }
                        break;
                    case ParseState.Dialog:
                        if ((match = rxDialogControl.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, idPrefixTemp);
                        }
                        break;
                    }
                }
            }
        }

        private void AddNamedString(Match match, LangFile file, int lineNumber, ref int unnamedCount, string clang, string nlang, string idPrefix, Match contextMatch = null)
        {
            SubLang sl;

            if (!SubLangs.TryGetValue(clang, out sl))
            {
                sl = new SubLang() { Source = file, Name = clang, Neutral = nlang };
                SubLangs.Add(clang, sl);
            }

            var stl = sl.AddNamedString(match, clang, file, lineNumber, ref unnamedCount, idPrefix, contextMatch);

            if (!NamedStringsByName.TryGetValue(stl.Id, out var ns))
            {
                ns = new TranslationString { Name = stl.Id };
                NamedStrings.Add(ns);
                NamedStringsByName.Add(stl.Id, ns);
            }

            ns.Lines.Add(stl);
            ns.Translations.Add(stl.Language, stl);
            stl.String = ns;
        }

        public void Scan(DirectoryInfo dir)
        {
            foreach (var lang in dir.GetFiles("*.rc"))
            {
                Files.Add(new LangFile() { Folder = this, File = lang });
            }

            ScanContents();
        }
    }
}