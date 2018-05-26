using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TransDiffer.Annotations;
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
        }

        public void Show(string browsePath)
        {
            Show();
            ScanFolder(browsePath);
        }

        public void ScanFolder(string browsePath)
        {
            Folders.Clear();
            RunInWorker(progress => ScanFolder(browsePath, progress), () =>
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
    }
}
