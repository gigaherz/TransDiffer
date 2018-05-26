using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using TransDiffer.Properties;

namespace TransDiffer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = new MainWindow();

            string browsePath = null;
            if (e.Args.Length > 0)
            {
                browsePath = e.Args[0];
            }
            else
            {
                var set = new Settings();
                var workspaceFolder = set.WorkspaceFolder;
                if (string.IsNullOrWhiteSpace(workspaceFolder) || !Directory.Exists(workspaceFolder))
                {
                    var dialog = new SelectFolderDialog();
                    if (dialog.ShowDialog() == true)
                    {
                        browsePath = dialog.WorkspaceFolder;
                        set.WorkspaceFolder = browsePath;
                        set.Save();
                    }
                }
                else
                {
                    browsePath = workspaceFolder;
                }
            }

            if (string.IsNullOrWhiteSpace(browsePath))
            {
                mainWindow.Close();
            }
            else
            {
                mainWindow.Show(browsePath);
            }
        }
    }
}
