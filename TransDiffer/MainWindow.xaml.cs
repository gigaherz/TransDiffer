using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TransDiffer.Annotations;

namespace TransDiffer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private string _templateName = "ByIdTemplate";

        public DirectoryInfo Root;
        public ObservableCollection<LangFolder> Folders { get; } = new ObservableCollection<LangFolder>();

        public ToolTip MissingLangs { get; } = new ToolTip();

        public RelayCommand ShowInExplorerCommand { get; }
        public RelayCommand OpenLangFileCommand { get; }
        public RelayCommand ByFileCommand { get; }
        public RelayCommand ByIdCommand { get; }

        public string TemplateName
        {
            get => _templateName;
            set
            {
                if (value == _templateName) return;
                _templateName = value;
                FoldersTree.ItemTemplate = (DataTemplate)FindResource(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsByFile));
                OnPropertyChanged(nameof(IsById));
            }
        }
        public bool IsByFile => TemplateName == "ByFileTemplate";
        public bool IsById => TemplateName == "ByIdTemplate";

        public MainWindow()
        {
            ShowInExplorerCommand = new RelayCommand(ShowInExplorer_OnClick);
            OpenLangFileCommand = new RelayCommand(OpenLangFile_OnClick);
            ByFileCommand = new RelayCommand(_ =>
            {
                TemplateName = "ByFileTemplate";
            });
            ByIdCommand = new RelayCommand(_ =>
            {
                TemplateName = "ByIdTemplate";
            });

            InitializeComponent();

            ByFileCommand.Execute(null);

            RunInWorker(progress => ScanFolder(@"F:\Reactos\sources", progress), () =>
            {
                var c = Folders.Count(f => f.HasErrors);
                if (c > 0)
                    StatusLabel.Text = $"Done. {c} folders have langauges with missing or obsolete strings.";
                else
                    StatusLabel.Text = "Done. No missing or obsolete strings found.";
            });
        }

        public void RunInWorker(Action<Action<int>> task, Action completion)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => task(worker.ReportProgress);
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += (sender, args) => LoadingProgress.Value = args.ProgressPercentage;
            worker.RunWorkerCompleted += (sender, args) => completion();
            worker.RunWorkerAsync();
        }

        public void ScanFolder(string path, Action<int> progress)
        {
            var dir = new DirectoryInfo(path);

            Root = dir;

            var dirs = dir.GetDirectories();
            for (var i = 0; i < dirs.Length; i++)
            {
                var subdir = dirs[i];
                var minPercent = i * 100 / (dirs.Length - 1);
                var maxPercent = (i + 1) * 100 / (dirs.Length - 1);
                ScanSubfolder(subdir, progress, minPercent, maxPercent);
            }
        }

        private void ScanSubfolder(DirectoryInfo dir, Action<int> progress, int minPercent, int maxPercent)
        {
            progress(minPercent);

            var enus = dir.GetFiles("en-US.rc");
            if (enus.Length > 0)
            {
                if (!dir.FullName.Replace("\\","/").Contains("/getuname/"))
                {
                    LoadLangs(dir);
                }
            }
            else
            {
                var dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; i++)
                {
                    var subdir = dirs[i];
                    var minPercent1 = minPercent + i * (maxPercent - minPercent) / dirs.Length;
                    var maxPercent1 = minPercent + (i + 1) * (maxPercent - minPercent) / dirs.Length;
                    ScanSubfolder(subdir, progress, minPercent1, maxPercent1);
                }
            }
        }

        private void LoadLangs(DirectoryInfo dir)
        {
            LangFolder lf = new LangFolder() { Root = Root, Directory = dir };
            foreach (var lang in dir.GetFiles("*.rc"))
            {
                if (string.Compare(lang.Name, "en-US.rc", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    lf.EnUs.Files.Add(new LangFile() { Folder = lf, File = lang });
                }
                else
                {
                    lf.OtherFiles.Files.Add(new LangFile() { Folder = lf, File = lang });
                }
            }

            lf.ScanContents();

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                Folders.Add(lf);
            }));
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is LangFile f)
            {
#if false
                string xmlDoc = null;
                StatusLabel.Text = "Preparing document...";
                RunInWorker(
                    (progress) => {
                        var doc = f.BuildDocument(MissingLangs, progress);
                        xmlDoc = XamlWriter.Save(doc);
                    },
                    () =>
                    {
                        StatusLabel.Text = "Displaying document...";
                        FileContents.Document = (FlowDocument)XamlReader.Parse(xmlDoc);
                        StatusLabel.Text = "Done.";
                    });
#else
                FileContents.Document = f.BuildDocument(MissingLangs, _ => { });
#endif
            }
        }

        private void ShowInExplorer_OnClick(object parameter)
        {
            if (parameter is FileInfo file)
            {
                Process.Start("explorer.exe", "/select," + file.FullName);
            }
        }

        private void OpenLangFile_OnClick(object parameter)
        {
            if (parameter is FileInfo file)
            {
                Process.Start(new ProcessStartInfo(file.FullName) { Verb = "open", UseShellExecute = true });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    enum ParseState
    {
        Root,
        Menu,
        Popup,
        Dialog,
        StringTable,
        Unhandled
    }

    public class LangFolder
    {
        public DirectoryInfo Root { get; set; }
        public DirectoryInfo Directory { get; set; }

        public string Name => Directory.FullName.Substring(Root.FullName.Length);
        public bool HasErrors => EnUs.HasErrors || OtherFiles.HasErrors;
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }

        public FileList EnUs { get; } = new FileList() { Name = "Primary" };
        public FileList OtherFiles { get; } = new FileList() { Name = "Other Languages" };
        public ObservableCollection<FileList> FileLists { get; }

        public Dictionary<string, SubLang> SubLangs { get; } = new Dictionary<string, SubLang>();

        public ObservableCollection<NamedString> NamedStrings { get; } = new ObservableCollection<NamedString>();
        public Dictionary<string, NamedString> NamedStringsByName { get; } = new Dictionary<string, NamedString>();

        public LangFolder()
        {
            FileLists = new ObservableCollection<FileList>(new[] { EnUs, OtherFiles });
        }

        public void ScanContents()
        {
            Debug.WriteLine($"Parsing {Name}...");
            var enus = EnUs.Files.First();
            ParseLang(enus);
            foreach (var lang in OtherFiles.Files)
            {
                ParseLang(lang);
            }

            ProcessComparisons();

            IsExpanded = HasErrors;
        }

        private void ProcessComparisons()
        {
            var lname = SubLangs.ContainsKey("ENGLISH_US") ? "ENGLISH_US" : "ENGLISH";
            if (SubLangs.ContainsKey(lname))
            {
                var langenus = SubLangs[lname];
                foreach (var clang in SubLangs.Keys)
                {
                    if (clang == lname)
                        continue;
                    var lang = SubLangs[clang];
                    ProcessComparisons(langenus, lang);
                }
            }

            EnUs.FinishLoading();
            OtherFiles.FinishLoading();
        }

        private static readonly Regex rxLanguage = new Regex("^[ \t]*LANGUAGE[, \t]+LANG_(?<lang>[a-zA-Z0-9_]+)(?:[, \t]+SUBLANG_(?<slang>[a-zA-Z0-9_]+))?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxBegin = new Regex("^[ \t]*(?:BEGIN|{)[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxEnd = new Regex("^[ \t]*(?:END|})[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxMenuEx = new Regex("^[ \t]*(?<id>[a-zA-Z0-9_]+)[, \t]+MENU(?:EX)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxMenuItem = new Regex("^[ \t]*MENUITEM[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+(?<id>[a-zA-Z0-9_]+)(?:[, \t]+.*)?)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex rxPopup = new Regex("^[ \t]*POPUP[, \t]+(?<text>\"(?:[^\\\\\"]|\\\\(?:[^\\\r]|\\\r\\\n)|\"\")*\")(?:[, \t]+(?<id>[a-zA-Z0-9_]+)(?:[, \t]+.*)?)?[ \t]*$", RegexOptions.Compiled | RegexOptions.Singleline);
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
            ParseState state = ParseState.Root;
            ParseState nextState = ParseState.Unhandled;
            Stack<ParseState> stateStack = new Stack<ParseState>();
            Stack<int> unnamedStack = new Stack<int>();

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
                        }
                        else if ((match = rxCaption.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang, contextMatch);
                        }
                        break;
                    case ParseState.StringTable:
                        if ((match = rxStringTableEntry.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang);
                        }
                        break;
                    case ParseState.Menu:
                        if ((match = rxMenuItem.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang);
                        }
                        else if ((match = rxPopup.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang);
                            nextState = ParseState.Menu;
                        }
                        break;
                    case ParseState.Dialog:
                        if ((match = rxDialogControl.Match(line)).Success)
                        {
                            AddNamedString(match, file, i, ref unnamedCount, clang, nlang);
                        }
                        break;
                    }
                }
            }
        }

        private void AddNamedString(Match match, LangFile file, int lineNumber, ref int unnamedCount, string clang, string nlang, Match contextMatch = null)
        {
            SubLang sl;

            if (!SubLangs.TryGetValue(clang, out sl))
            {
                sl = new SubLang() { Source = file, Name = clang, Neutral = nlang };
                SubLangs.Add(clang, sl);
            }

            var stl = sl.AddNamedString(match, clang, file, lineNumber, ref unnamedCount, contextMatch);

            if (!NamedStringsByName.TryGetValue(stl.Id, out var ns))
            {
                ns = new NamedString { Name = stl.Id };
                NamedStrings.Add(ns);
                NamedStringsByName.Add(stl.Id, ns);
            }

            ns.Lines.Add(stl);
            ns.Translations.Add(stl.Language, stl);
        }

        private void ProcessComparisons(SubLang enUs, SubLang lang)
        {
            SubLang en = enUs.Neutral != enUs.Name ? (SubLangs.TryGetValue(enUs.Neutral, out var t1) ? t1 : null) : null;
            SubLang neutral = lang.Neutral != lang.Name ? (SubLangs.TryGetValue(lang.Neutral, out var t2) ? t2 : null) : null;

            string[] MissingInLang = enUs.NamedStrings.Keys.Where(l => !lang.NamedStrings.ContainsKey(l) && (neutral == null || !neutral.NamedStrings.ContainsKey(l))).ToArray();
            string[] MissingInEnUs = lang.NamedStrings.Keys.Where(l => !enUs.NamedStrings.ContainsKey(l) && (en == null || !en.NamedStrings.ContainsKey(l))).ToArray();
            if (MissingInEnUs.Length > 0)
            {
                Debug.WriteLine($"Detected strings from {lang.Name}({lang.Source.Name}) missing in {enUs.Name}({enUs.Source.Name}):  {string.Join(", ", MissingInEnUs)}");
                foreach (var s in MissingInEnUs)
                    lang.NamedStrings[s].MissingInOtherLanguage.Add(enUs);
            }
            if (MissingInLang.Length > 0)
            {
                Debug.WriteLine($"Detected strings from {enUs.Name}({enUs.Source.Name}) missing in {lang.Name}({lang.Source.Name}): {string.Join(", ", MissingInLang)}");
                foreach (var s in MissingInLang)
                    enUs.NamedStrings[s].MissingInOtherLanguage.Add(lang);
            }
        }

    }

    public class NamedString
    {
        public string Name { get; set; }
        public ObservableCollection<StringLine> Lines { get; } = new ObservableCollection<StringLine>();

        public Dictionary<string, StringLine> Translations { get; } = new Dictionary<string, StringLine>();

        public bool HasErrors { get; set; }
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }
    }

    public class FileList
    {
        public string Name { get; set; }
        public ObservableCollection<LangFile> Files { get; } = new ObservableCollection<LangFile>();
        public bool HasErrors => Files.Any(lf => lf.HasErrors);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }

        public void FinishLoading()
        {
            IsExpanded = HasErrors;
            foreach (var file in Files)
            {
                file.FinishLoading();
            }
        }
    }

    public class LangFile
    {
        public LangFolder Folder { get; set; }
        public FileInfo File { get; set; }

        public string Name => File.Name;
        public bool HasErrors => NamedLines.Values.Any(nl => nl.MissingInOtherLanguage.Count > 0);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }

        public string[] Content;
        private FlowDocument cachedDocument;

        public void FinishLoading()
        {
            IsExpanded = HasErrors;
        }

        public Dictionary<int, StringLine> NamedLines { get; } = new Dictionary<int, StringLine>();

        public FlowDocument BuildDocument(ToolTip tt, Action<int> progress)
        {
            if (cachedDocument != null)
                return cachedDocument;

            var block = new Section();

            progress(0);
            for (var i = 0; i < Content.Length; i++)
            {
                var line = Content[i];
                Paragraph para;
                var ll = NamedLines.TryGetValue(i, out var sl) ? sl : null;
                if (ll != null)
                {
                    para = ll.GetFormattedParagraph(tt);
                }
                else
                {
                    para = new Paragraph(new Run(line));
                }
                para.Margin = new Thickness();
                block.Blocks.Add(para);
                progress(i * 100 / (Content.Length - 1));
            }

            cachedDocument = new FlowDocument(block) { FontFamily = new FontFamily("Courier New") };
            return cachedDocument;
        }

        public void InvalidateDocument() { cachedDocument = null; }
    }

    public class SubLang
    {
        public string Neutral { get; set; }
        public LangFile Source { get; set; }
        public string Name { get; set; }

        public Dictionary<string, StringLine> NamedStrings { get; } = new Dictionary<string, StringLine>();

        public StringLine AddNamedString(Match match, String lang, LangFile file, int lineNumber, ref int unnamedCount, Match contextMatch = null)
        {
            var sl = CreateNamedString(match, lang, file, lineNumber, ref unnamedCount, contextMatch);
            NamedStrings.Add(sl.Id, sl);
            file.NamedLines.Add(lineNumber, sl);
            return sl;
        }

        public StringLine CreateNamedString(Match match, String lang, LangFile file, int lineNumber, ref int unnamedCount, Match contextMatch = null)
        {
            var id = match.Groups["id"].Value;
            var idNumbered = id;

            if (id == "-1" || id == "IDC_STATIC" || !match.Groups["id"].Success)
            {
                if (contextMatch == null)
                {
                    id = $"UNNAMED_{id}_{unnamedCount++}";
                    idNumbered = $"{id}#0";
                }
                else
                {
                    id = contextMatch.Groups["id"].Value;
                    idNumbered = id;

                    if (id == "-1" || id == "IDC_STATIC" || !contextMatch.Groups["id"].Success)
                    {
                        id = $"UNNAMED_{id}_{unnamedCount++}";
                        idNumbered = $"{id}#0";
                    }
                }
            }

            int number = 0;
            while (NamedStrings.ContainsKey(idNumbered))
            {
                number++;
                idNumbered = $"{id}#{number}";
            }

            return new StringLine() { Id = idNumbered, Language = lang, Source = file, LineNumber = lineNumber, RegexMatch = match };
        }
    }

    public class StringLine
    {
        public LangFile Source { get; set; }
        public int LineNumber { get; set; }
        public Match RegexMatch { get; set; }

        public string Id { get; set; }
        public string Language { get; set; }

        // Results of the comparison
        public bool HasAllTranslations { get; set; } = true;
        public StringLine Original { get; set; }
        public ObservableCollection<StringLine> Translations { get; } = new ObservableCollection<StringLine>();
        public ObservableCollection<SubLang> MissingInOtherLanguage { get; } = new ObservableCollection<SubLang>();

        public override string ToString()
        {
            return $"{{{RegexMatch.Groups["text"].Value}}}";
        }

        public Paragraph GetFormattedParagraph(ToolTip tt)
        {
            var LineBrush = new SolidColorBrush(Color.FromRgb(245, 255, 245));
            var IdBrush = new SolidColorBrush(Color.FromRgb(160, 255, 160));
            var TextBrush = new SolidColorBrush(Color.FromRgb(200, 255, 200));
            var IdBrushMissing = new SolidColorBrush(Color.FromRgb(255, 140, 140));

            var whole = RegexMatch.Value;
            var id = RegexMatch.Groups["id"];
            var text = RegexMatch.Groups["text"];

            var para = new Paragraph { Background = LineBrush };

            if (!id.Success)
            {
                para.Inlines.Add(new Run(whole.Substring(0, text.Index)));
                para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length)));
            }
            else
            {
                if (id.Index < text.Index)
                {
                    para.Inlines.Add(new Run(whole.Substring(0, id.Index)));
                    para.Inlines.Add(new Run(id.Value) { Background = IdBrush });
                    para.Inlines.Add(new Run(whole.Substring(id.Index + id.Length, text.Index - id.Index - id.Length)));
                    para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                    para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length)));
                }
                else
                {
                    para.Inlines.Add(new Run(whole.Substring(0, text.Index)));
                    para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                    para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length, id.Index - text.Index - text.Length)));
                    para.Inlines.Add(new Run(id.Value) { Background = IdBrush });
                    para.Inlines.Add(new Run(whole.Substring(id.Index + id.Length)));
                }
            }

            if (MissingInOtherLanguage.Count > 0)
            {
                para.Background = IdBrushMissing;
                para.MouseEnter += (sender, args) =>
                {
                    if (args.LeftButton == MouseButtonState.Pressed
                        || args.RightButton == MouseButtonState.Pressed
                        || args.MiddleButton == MouseButtonState.Pressed
                        || args.XButton1 == MouseButtonState.Pressed
                        || args.XButton2 == MouseButtonState.Pressed)
                        return;
                    tt.Content = $"Detected missing strings for {id.Value}: {string.Join(", ", MissingInOtherLanguage.Select(o => $"{o.Name}({o.Source.Name})"))}";
                    tt.IsOpen = true;
                };
                para.MouseLeave += (sender, args) =>
                {
                    tt.IsOpen = false;
                };
            }

            return para;
        }
    }
}
