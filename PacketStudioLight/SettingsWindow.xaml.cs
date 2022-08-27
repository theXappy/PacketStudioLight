using PacketStudioLight.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PacketStudioLight
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        List<string> paths = new List<string>();

        public string SelectedWiresharkPath => sharksComboBox.SelectedItem as string;

        public SettingsWindow()
        {
            InitializeComponent();

            List<WiresharkDirectory>? dirs = SharksFinder.GetDirectories();
            paths.AddRange(dirs.Select(d => Path.GetDirectoryName(d.WiresharkPath) + Path.DirectorySeparatorChar));
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
    }
}
