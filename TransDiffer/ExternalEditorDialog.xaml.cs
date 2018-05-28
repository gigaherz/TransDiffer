using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using TransDiffer.Annotations;

namespace TransDiffer
{
    /// <summary>
    /// Interaction logic for ExternalEditorDialog.xaml
    /// </summary>
    public partial class ExternalEditorDialog : INotifyPropertyChanged
    {
        private EditorCommandLineStyle _selectedStyle;
        private string _commandLinePattern;
        private string _externalEditorPath;

        public class EditorCommandLineStyle
        {
            public string Name { get; set; }
            public string CommandLine { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public static List<EditorCommandLineStyle> CommandLineStyles { get; } = new List<EditorCommandLineStyle>
        {
            new EditorCommandLineStyle { Name="Generic (no line support)", CommandLine = "$file$"},
            new EditorCommandLineStyle { Name="Notepad++", CommandLine = "-n$line$ $file$"},
            new EditorCommandLineStyle { Name="Custom", CommandLine = ""},
        };

        public EditorCommandLineStyle SelectedStyle
        {
            get { return _selectedStyle; }
            set
            {
                if (Equals(value, _selectedStyle)) return;
                _selectedStyle = value;
                if (_selectedStyle == null)
                    _selectedStyle = CommandLineStyles.First(s => s.Name == "Custom");
                OnPropertyChanged();

                if (!string.IsNullOrWhiteSpace(SelectedStyle.CommandLine))
                    _commandLinePattern = SelectedStyle.CommandLine;
                OnPropertyChanged(nameof(CommandLinePattern));
            }
        }

        // for external use to load/save config
        public string CommandLineStyle
        {
            get { return SelectedStyle?.Name ?? ""; }
            set
            {
                if (value == SelectedStyle?.Name) return;
                if (value == null) SelectedStyle = null;
                SelectedStyle = CommandLineStyles.FirstOrDefault(s => s.Name == value);
                OnPropertyChanged();
            }
        }

        public string CommandLinePattern
        {
            get { return _commandLinePattern; }
            set
            {
                if (value == _commandLinePattern) return;
                _commandLinePattern = value;
                OnPropertyChanged();

                _selectedStyle = CommandLineStyles.First(s => s.Name == "Custom");
                OnPropertyChanged(nameof(SelectedStyle));
            }
        }

        public string ExternalEditorPath
        {
            get { return _externalEditorPath; }
            set
            {
                if (value == _externalEditorPath) return;
                _externalEditorPath = value;
                OnPropertyChanged();
            }
        }

        public ExternalEditorDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void SelectEditor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                EnsurePathExists = true,
                EnsureFileExists = true,
                Filters = { new CommonFileDialogFilter("Executable Program (*.exe)", "*.exe"), new CommonFileDialogFilter("All Files (*.*)", "*.*") }
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ExternalEditorPath = dialog.FileName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static string NameToPattern(string style, string pattern)
        {
            var info = CommandLineStyles.FirstOrDefault(s => s.Name == style);
            if (info == null || info.Name == "Custom")
                return pattern;
            return info.CommandLine;
        }
    }
}
