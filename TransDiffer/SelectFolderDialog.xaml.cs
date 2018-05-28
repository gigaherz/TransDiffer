using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using TransDiffer.Annotations;

namespace TransDiffer
{
    /// <summary>
    /// Interaction logic for SelectFolderDialog.xaml
    /// </summary>
    public partial class SelectFolderDialog : INotifyPropertyChanged
    {
        private string _workspaceFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public string WorkspaceFolder
        {
            get => _workspaceFolder;
            set
            {
                if (value == _workspaceFolder) return;
                _workspaceFolder = value;
                OnPropertyChanged();
            }
        }

        public SelectFolderDialog()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void BrowseForFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = Directory.Exists(WorkspaceFolder) ? WorkspaceFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                EnsurePathExists = true,
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                WorkspaceFolder = dialog.FileName;
            }
        }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WorkspaceFolder) || !Directory.Exists(WorkspaceFolder))
            {
                if (MessageBox.Show($"The folder '{WorkspaceFolder}' does not exist, do you want to create it?", "Error", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation, MessageBoxResult.OK) == MessageBoxResult.OK)
                {
                    try
                    {
                        var info = Directory.CreateDirectory(WorkspaceFolder);
                        if (info.Exists)
                        {
                            DialogResult = true;
                        }
                    }
                    catch (IOException)
                    {
                        MessageBox.Show($"The folder could not be created.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                DialogResult = true;
            }
        }
    }
}
