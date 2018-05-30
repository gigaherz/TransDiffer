using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TransDiffer.Annotations;
using TransDiffer.Model;
using TransDiffer.Parser.Structure;
using TransDiffer.Properties;

namespace TransDiffer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        public static Brush SemiRed = new SolidColorBrush(Color.FromArgb(96, 255, 60, 0));

        private string _templateName = "ByFileTemplate";
        private bool _isScanningAllowed;
        private bool _canCancel;
        private string _treeSearchTerm = "";
        private string _externalEditorPath;
        private string _externalEditorCommandLinePattern;
        private string _fileSearchTerm;

        public Action CancelScanning = null;
        public DirectoryInfo Root;
        private ObservableCollection<FileLineItem> _currentFileLines;
        private ObservableCollection<FileLineItem> _currentDetails;
        private LangFile _currentFile;
        public ObservableCollection<ComponentFolder> Folders { get; } = new ObservableCollection<ComponentFolder>();

        public RelayCommand ShowInExplorerCommand { get; }
        public RelayCommand OpenLangFileCommand { get; }
        public RelayCommand ByFileCommand { get; }
        public RelayCommand ByIdCommand { get; }

        public string TreeSearchTerm
        {
            get { return _treeSearchTerm; }
            set
            {
                if (value == _treeSearchTerm) return;
                _treeSearchTerm = value;
                OnPropertyChanged();
            }
        }

        public string FileSearchTerm
        {
            get { return _fileSearchTerm; }
            set
            {
                if (value == _fileSearchTerm) return;
                _fileSearchTerm = value;
                OnPropertyChanged();
            }
        }

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

        public bool IsScanningAllowed
        {
            get { return _isScanningAllowed; }
            set
            {
                if (value == _isScanningAllowed) return;
                _isScanningAllowed = value;
                OnPropertyChanged();
            }
        }


        public bool CanCancel
        {
            get { return _canCancel; }
            set
            {
                if (value == _canCancel) return;
                _canCancel = value;
                OnPropertyChanged();
            }
        }

        public LangFile CurrentFile
        {
            get { return _currentFile; }
            set
            {
                if (Equals(value, _currentFile)) return;
                _currentFile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTitleBar));
            }
        }

        public string CurrentTitleBar
        {
            get
            {
                if (CurrentFile != null)
                {
                    return $"TransDiffer - {CurrentFile.File.FullName}";
                }

                return "TransDiffer";
            }
        }

        public ObservableCollection<FileLineItem> CurrentFileLines
        {
            get { return _currentFileLines; }
            set
            {
                if (Equals(value, _currentFileLines)) return;
                _currentFileLines = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileLineItem> CurrentDetails
        {
            get { return _currentDetails; }
            set
            {
                if (Equals(value, _currentDetails)) return;
                _currentDetails = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowDetailsPane));
            }
        }

        public bool ShowDetailsPane => CurrentDetails != null && CurrentDetails.Count > 0;

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


            var cfg = new Settings();
            _externalEditorPath = cfg.ExternalEditorPath;
            _externalEditorCommandLinePattern = ExternalEditorDialog.NameToPattern(cfg.ExternalEditorCommandLineStyle, cfg.ExternalEditorCommandLinePattern);

            InitializeComponent();
        }

        public void Show(string browsePath)
        {
            Show();
            ScanFolder(browsePath);
        }

        public void ScanFolder(string browsePath)
        {
            IsScanningAllowed = false;
            Folders.Clear();
            CancelScanning = RunInWorker((progress, cancellationPending, setCancelled) => ScanFolder(browsePath, progress, cancellationPending, setCancelled), (cancelled) =>
            {
                if (cancelled)
                {
                    StatusLabel.Text = "Cancelled.";
                    LoadingProgress.Value = 0;
                }
                else
                {
                    var c = Folders.Count(f => f.HasErrors);
                    if (c > 0)
                        StatusLabel.Text = $"Done. {c} folders have langauges with missing or obsolete strings.";
                    else
                        StatusLabel.Text = "Done. No missing or obsolete strings found.";
                }
                IsScanningAllowed = true;
                CanCancel = false;
                CancelScanning = null;
            });
            CanCancel = true;
        }

        public Action RunInWorker(Action<Action<int>, Func<bool>, Action> task, Action<bool> completion)
        {
            bool wasCancelled = false;
            var setCancelled = new Action(() => { wasCancelled = true; });

            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => task(worker.ReportProgress, () => worker.CancellationPending, setCancelled);
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += (sender, args) => LoadingProgress.Value = args.ProgressPercentage;
            worker.WorkerSupportsCancellation = true;
            worker.RunWorkerCompleted += (sender, args) =>
            {
                completion(wasCancelled);
            };
            worker.RunWorkerAsync();
            return () => worker.CancelAsync();
        }

        public void ScanFolder(string path, Action<int> progress, Func<bool> cancellationPending, Action setCancelled)
        {
            var dir = new DirectoryInfo(path);

            Root = dir;

            var dirs = dir.GetDirectories();
            for (var i = 0; i < dirs.Length; i++)
            {
                if (cancellationPending())
                {
                    setCancelled();
                    return;
                }
                var subdir = dirs[i];
                var minPercent = i * 100 / (dirs.Length - 1);
                var maxPercent = (i + 1) * 100 / (dirs.Length - 1);
                ScanSubfolder(subdir, progress, cancellationPending, setCancelled, minPercent, maxPercent);
            }
        }

        private void ScanSubfolder(DirectoryInfo dir, Action<int> progress, Func<bool> cancellationPending, Action setCancelled, int minPercent, int maxPercent)
        {
            progress(minPercent);

            var enus = dir.GetFiles("en-US.rc");
            if (enus.Length > 0)
            {
                if (!dir.FullName.Replace("\\", "/").Contains("/getuname/"))
                {
                    LoadLangs(dir);
                }
            }
            else
            {
                var dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; i++)
                {
                    if (cancellationPending())
                    {
                        setCancelled();
                        return;
                    }
                    var subdir = dirs[i];
                    var minPercent1 = minPercent + i * (maxPercent - minPercent) / dirs.Length;
                    var maxPercent1 = minPercent + (i + 1) * (maxPercent - minPercent) / dirs.Length;
                    ScanSubfolder(subdir, progress, cancellationPending, setCancelled, minPercent1, maxPercent1);
                }
            }
        }

        private void LoadLangs(DirectoryInfo dir)
        {
            ComponentFolder lf = new ComponentFolder() { Root = Root, Directory = dir };
            lf.Scan(dir);

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                Folders.Add(lf);
            }));
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is LangFile f)
            {
                TranslationString str = null;

                if (FileContents.SelectedItems.Count > 0)
                {
                    var cp = FileContents.SelectedItem as FileLineItem;
                    if (cp.Tag.Strings.Count > 0)
                    {
                        var first = cp.Tag.Strings.First();
                        if (first.Source.Folder == f.Folder)
                        {
                            str = first.String;
                        }
                    }
                }

                CurrentFileLines = f.BuildDocument();

                FileContents_OnSelectionChanged(FileContents, new RoutedEventArgs(e.RoutedEvent));

                if (str != null)
                {
                    foreach (var sl in f.ContainedLangs)
                    {
                        if (str.Translations.TryGetValue(sl.Name, out var ns))
                        {
                            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            {
                                NavigateToTranslation(ns);
                            }));
                            break;
                        }
                    }
                }

                CurrentFile = f;
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

        private void ChangeWorkspace_Click(object sender, RoutedEventArgs e)
        {
            var set = new Settings();
            var dialog = new SelectFolderDialog
            {
                WorkspaceFolder = Root.FullName,
                Owner = this
            };
            if (dialog.ShowDialog() == true)
            {
                var browsePath = dialog.WorkspaceFolder;
                set.WorkspaceFolder = browsePath;
                set.Save();

                ScanFolder(browsePath);
            }
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            if (CanCancel)
                CancelScanning?.Invoke();
        }

        private void FileContents_OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var s = FileContents.SelectedItems.Cast<FileLineItem>().SelectMany(i => i.Tag.Strings).Distinct().ToList();
            if (s.Count == 1)
            {
                CurrentDetails = s.First().String.CreateDetailsDocument(NavigateToTranslation, NavigateToFile);
            }
            else if (FoldersTree.SelectedItem is LangFile f)
            {
                CurrentDetails = f.CreateDetailsDocument(NavigateToTranslation);
            }
            else
            {
                CurrentDetails = new ObservableCollection<FileLineItem>();
            }
        }

        private void NavigateToFile(LangFile obj)
        {

            if (FoldersTree.ItemContainerGenerator.ContainerFromItem(obj.Folder) is TreeViewItem tvi0)
            {
                if (tvi0.ItemContainerGenerator.ContainerFromItem(obj) is TreeViewItem tvi)
                {
                    tvi.IsSelected = true;
                }
            }
        }

        private void NavigateToTranslation(TranslationStringReference obj)
        {

            if (FoldersTree.ItemContainerGenerator.ContainerFromItem(obj.Source.Folder) is TreeViewItem tvi0)
            {
                if (tvi0.ItemContainerGenerator.ContainerFromItem(obj.Source) is TreeViewItem tvi)
                {
                    tvi.IsSelected = true;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (obj.Paragraphs.Count > 0)
                        {
                            SetSelection(obj.Paragraphs);
                            ScrollAndFocus();
                        }
                    }));
                }
            }
        }

        private void FileContents_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileContents.SelectedItems.Count != 1)
                return;

            var p = (FileLineItem) FileContents.SelectedItem;
            var r = p.Tag;
            if (!TryLaunchExternalEditor(r))
            {
                if (OpenExternalEditorDialog())
                {
                    if (!TryLaunchExternalEditor(r))
                    {
                        MessageBox.Show("Could not launch external editor", "Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private bool TryLaunchExternalEditor(SourceInfo r)
        {
            if (File.Exists(_externalEditorPath))
            {
                var cmdline = _externalEditorCommandLinePattern
                    .Replace("$file$", r.File.FullName)
                    .Replace("$line$", r.Line.ToString());
                var p = new Process { StartInfo = new ProcessStartInfo(_externalEditorPath, cmdline) };
                return p.Start();
            }

            return false;
        }

        private void ExternalEditorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalEditorDialog();
        }

        private bool OpenExternalEditorDialog()
        {
            var cfg = new Settings();
            var dialog = new ExternalEditorDialog
            {
                Owner = this,
                ExternalEditorPath = cfg.ExternalEditorPath,
                CommandLineStyle = cfg.ExternalEditorCommandLineStyle
            };
            if (dialog.CommandLineStyle == "Custom")
                dialog.CommandLinePattern = cfg.ExternalEditorCommandLinePattern;
            if (dialog.ShowDialog() == true)
            {
                cfg.ExternalEditorPath = dialog.ExternalEditorPath;
                cfg.ExternalEditorCommandLineStyle = dialog.CommandLineStyle;
                cfg.ExternalEditorCommandLinePattern = dialog.CommandLinePattern;
                cfg.Save();

                _externalEditorPath = cfg.ExternalEditorPath;
                _externalEditorCommandLinePattern = ExternalEditorDialog.NameToPattern(cfg.ExternalEditorCommandLineStyle,
                    cfg.ExternalEditorCommandLinePattern);

                return true;
            }

            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileContents.SelectedItems.Count != 1)
                    return;

                var p = (FileLineItem)FileContents.SelectedItem;
                var r = p.Tag;
                {
                    bool hadToLoop = r.Strings.Count == 0;
                    while (r.Strings.Count == 0)
                    {
                        if (r.Next == null)
                            return;

                        r = r.Next;
                    }
                    var str = r.Strings.Last();
                    if (!hadToLoop) str = str.Next;
                    if (str == null)
                        return;
                    SetSelection(str.Paragraphs);
                }
            }
            finally
            {
                ScrollAndFocus();
            }
        }

        private void SetSelection(IEnumerable<FileLineItem> items = null)
        {
            if (items != null)
            {
                FileContents.SelectedItems.Clear();
                foreach (var item in items)
                    FileContents.SelectedItems.Add(item);
            }
        }
        private void ScrollAndFocus()
        {
            FileContents.Focus();
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                FileContents.ScrollIntoView(FileContents.SelectedItem);
            }));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileContents.SelectedItems.Count != 1)
                    return;

                var p = (FileLineItem)FileContents.SelectedItem;
                var r = p.Tag;
                {
                    bool hadToLoop = r.Strings.Count == 0;
                    while (r.Strings.Count == 0)
                    {
                        if (r.Previous == null)
                            return;

                        r = r.Previous;
                    }
                    var str = r.Strings.First();
                    if (!hadToLoop) str = str.Previous;
                    if (str == null)
                        return;
                    SetSelection(str.Paragraphs);
                }
            }
            finally
            {
                ScrollAndFocus();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileContents.SelectedItems.Count != 1)
                    return;

                var p = (FileLineItem)FileContents.SelectedItem;
                var r = p.Tag;
                {
                    bool hadToLoop = r.Strings.Count == 0;
                    while (r.Strings.Count == 0)
                    {
                        if (r.Previous == null)
                            return;

                        r = r.Previous;
                    }
                    var str = r.Strings.First();
                    if (!hadToLoop) str = str.Previous;
                    if (str == null)
                        return;
                    while (str.String.MissingInLanguages.Count == 0)
                    {
                        if (str.Previous == null)
                            return;
                        str = str.Previous;
                    }
                    SetSelection(str.Paragraphs);
                }
            }
            finally
            {
                ScrollAndFocus();
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileContents.SelectedItems.Count != 1)
                    return;

                var p = (FileLineItem)FileContents.SelectedItem;
                var r = p.Tag;
                {
                    bool hadToLoop = r.Strings.Count == 0;
                    while (r.Strings.Count == 0)
                    {
                        if (r.Next == null)
                            return;

                        r = r.Next;
                    }
                    var str = r.Strings.Last();
                    if (!hadToLoop) str = str.Next;
                    if (str == null)
                        return;
                    while (str.String.MissingInLanguages.Count == 0)
                    {
                        if (str.Next == null)
                            return;
                        str = str.Next;
                    }
                    SetSelection(str.Paragraphs);
                }
            }
            finally
            {
                ScrollAndFocus();
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            ScanFolder(Root.FullName);
        }

        private void UIElement1_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            TreeSearchBox.Clear();
        }

        private void UIElement2_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            FileSearchBox.Clear();
        }



        private void TreeSearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return && e.Key != Key.Enter)
                return;

            //FoldersTree.items
        }
    }
}
