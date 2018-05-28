using System;
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
        private string _templateName = "ByFileTemplate";

        public DirectoryInfo Root;
        public ObservableCollection<ComponentFolder> Folders { get; } = new ObservableCollection<ComponentFolder>();

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

        public Action CancelScanning = null;

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

        private string _externalEditorPath;
        private string _externalEditorCommandLinePattern;
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

        private bool _isScanningAllowed;
        private bool _canCancel;

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
                f.BuildDocument(FileContents, MissingLangs, _ => { });
#endif
                FileContents_OnSelectionChanged(FileContents, new RoutedEventArgs(e.RoutedEvent));
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
            var cp = FileContents.CaretPosition;
            var p = cp.Paragraph;
            if (p?.Tag is TranslationStringReference str)
            {
                DetailsPane.Document = str.String.CreateDetailsDocument();
            }
            else if (FoldersTree.SelectedItem is LangFile f)
            {
                DetailsPane.Document = f.CreateDetailsDocument();
            }
            else
            {
                DetailsPane.Document = new FlowDocument();
            }
        }

        private void ExternalEditorMenuItem_Click(object sender, RoutedEventArgs e)
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
                _externalEditorCommandLinePattern = ExternalEditorDialog.NameToPattern(cfg.ExternalEditorCommandLineStyle, cfg.ExternalEditorCommandLinePattern);
            }
        }

        private void FileContents_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var cp = FileContents.CaretPosition;
            if (cp.Paragraph.Tag is TranslationStringReference r)
            {
                var cmdline = _externalEditorCommandLinePattern
                    .Replace("$file$", r.Source.File.FullName)
                    .Replace("$line$", r.Context.Line.ToString());
                Process p = new Process();
                p.StartInfo = new ProcessStartInfo(_externalEditorPath, cmdline);
                p.Start();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FileContents.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Red);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            FileContents.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Pink);
        }
    }
}
