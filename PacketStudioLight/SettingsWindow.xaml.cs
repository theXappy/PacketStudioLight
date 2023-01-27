using PacketStudioLight.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace PacketStudioLight
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        ObservableCollection<string> paths = new ObservableCollection<string>();

        public string SelectedWiresharkPath => sharksComboBox.SelectedItem as string;

        public SettingsWindow()
        {
            InitializeComponent();

            List<WiresharkDirectory>? dirs = SharksFinder.GetDirectories();
            foreach (var path in dirs.Select(d => Path.GetDirectoryName(d.WiresharkPath) + Path.DirectorySeparatorChar))
            {
                paths.Add(path);
            }
            sharksComboBox.ItemsSource = paths;
        }

        public void SetCurrentWiresharkDir(string path)
        {
            string match = paths.FirstOrDefault(existingPath => existingPath.Equals(path, System.StringComparison.CurrentCultureIgnoreCase));
            if (match == null)
            {
                paths.Add(path);
                match = path;
            }
            sharksComboBox.SelectedIndex = paths.IndexOf(match);
        }


        private void OkClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Wireshark.exe|Wireshark.exe"
            };
            DialogResult res = ofd.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                if (ofd.CheckFileExists)
                {
                    string dirPath = Path.GetDirectoryName(ofd.FileName);
                    if (SharksFinder.TryGetByPath(dirPath, out WiresharkDirectory wd))
                    {
                        if (!dirPath.EndsWith(Path.DirectorySeparatorChar))
                            dirPath += Path.DirectorySeparatorChar;
                        paths.Add(dirPath);
                        SetCurrentWiresharkDir(dirPath);
                    }
                }
            }
        }
    }
}
