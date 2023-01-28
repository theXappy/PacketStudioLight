using Haukcode.PcapngUtils.Common;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;
using Haukcode.PcapngUtils.PcapNG;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Xml.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Xml;
using FastPcapng;
using PacketGen;
using PacketDotNet;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace PacketStudioLight
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class FileLoadUiFreezeContext : IDisposable
        {
            private MainWindow _mw;
            public FileLoadUiFreezeContext(MainWindow mw, string state)
            {
                _mw = mw;
                _mw.packetsListBox.SelectedIndex = -1;
                _mw.packetsListBox.DataContext = null;
                _mw.packetsListBox.IsEnabled = false;
                _mw.packetTextBox.Text = "";
                _mw.loadingStatusLabel.Content = state;
                _mw.loadingStatusLabel.Visibility = Visibility.Visible;
                _mw.disablerBlock.Visibility = Visibility.Hidden;
                _mw.CenterPanelsArea.IsEnabled = false;
                _mw.MainToolBarTray.IsEnabled = false;
                _mw._overrides = new();
            }

            public void Dispose()
            {
                _mw.packetsListBox.IsEnabled = true;
                _mw.CenterPanelsArea.IsEnabled = true;
                _mw.MainToolBarTray.IsEnabled = true;
                _mw.loadingStatusLabel.Visibility = Visibility.Collapsed;
            }
        }


        private string _wsDir { get; set; }
        string WiresharkPath => Path.Combine(_wsDir, "wireshark.exe");
        string TsharkPath => Path.Combine(_wsDir, "tshark.exe");
        TSharkInterop op => new TSharkInterop(TsharkPath);

        private MemoryPcapng _memoryPcapng;

        private Dictionary<string, string> _templates;

        public MainWindow()
        {
            LefToRightHack.Init();
            InitializeComponent();
            var myAssembly = typeof(MainWindow).Assembly;
            using (Stream s = File.OpenRead("MyHighlighting.xshd"))
            {
                using (XmlReader reader = new XmlTextReader(s))
                {
                    packetTextBox.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            _wsDir = Properties.Settings.Default.WiresharkDirectory.Trim('"', ' ');

            LoadPacketTemplates();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            // RemoveToolBarsOverflows
            void ToolBar_Loaded(ToolBar toolBar)
            {
                var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
                if (overflowGrid != null)
                {
                    overflowGrid.Visibility = Visibility.Collapsed;
                }
                var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
                if (mainPanelBorder != null)
                {
                    mainPanelBorder.Margin = new Thickness();
                }
            }

            foreach (ToolBar toolBar in MainToolBarTray.ToolBars)
            {
                foreach (object toolBarItem in toolBar.Items)
                {
                    ToolBar.SetOverflowMode(toolBarItem as DependencyObject, OverflowMode.Never);
                }
                ToolBar_Loaded(toolBar);
            }
        }

        public void LoadPacketTemplates()
        {
            _templates = PacketsGenerator.GetTemplateHints();
            foreach (string templatesKey in _templates.Keys)
            {
                MenuItem mi = new MenuItem()
                {
                    Header = templatesKey,
                };
                mi.Click += InsertTemplateClicked;
                InsertTemplatesMenuItem.Items.Add(mi);
            }
        }

        private void InsertTemplateClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                string template = _templates[mi.Header as string];
                packetTextBox.Text = template + "\n" + packetTextBox.Text;
            }
        }

        private class PacketOverride
        {
            public byte[] Data { get; set; }
            public string OriginalText { get; set; }
            public LinkLayers? LinkLayer { get; set; }
        }

        Dictionary<int, PacketOverride> _overrides;

        private async void OpenButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Captures (*.pcapng)|*.pcapng";
            bool? res = ofd.ShowDialog();
            if (res != true)
                return;
            string capPath = ofd.FileName;
            if (Path.GetExtension(capPath) != ".pcapng")
            {
                MessageBox.Show("Error: File did not have a .pcapng extension", "Error");
                return;
            }

            // User didn't cancel & actually chose a pcapng file. We can clear the old data now.
            using (new FileLoadUiFreezeContext(this, "Loading..."))
            {
                _memoryPcapng = MemoryPcapng.ParsePcapng(capPath);
                await UpdatePacketsList();
            }
        }

        private async Task UpdatePacketsList()
        {
            // When you are ready to implement Packet List Coloring use this line
            // string[] results = await op.GetTextOutputAsync(_memoryPcapng);
            string[] results = await op.GetPacketsDescriptions(_memoryPcapng);
            var newDataContext = new PacketsListBoxViewModel(new ObservableCollection<string>(results));
            newDataContext.Updated += HandlePacketsDraggedAndDropped;
            packetsListBox.DataContext = newDataContext;
        }

        private void HandlePacketsDraggedAndDropped(object? sender, PacketMovedEventArgs e)
            => this.Dispatcher.Invoke(() => HandlePacketsDraggedAndDroppedUI(sender, e));
        private async void HandlePacketsDraggedAndDroppedUI(object? sender, PacketMovedEventArgs e)
        {
            loadingStatusLabel.Content = "Reordering...";
            loadingStatusLabel.Visibility = Visibility.Visible;

            // Now let's move the Packet Blocks in the memory pcap
            ApplyOverrides();
            await Task.Run(() => _memoryPcapng.MovePacket(e.FromIndex, e.ToIndex));

            PacketsListBoxViewModel vm = sender as PacketsListBoxViewModel;
            vm.Updated -= HandlePacketsDraggedAndDropped;

            await UpdatePacketsList();

            loadingStatusLabel.Visibility = Visibility.Hidden;
        }

        private void ApplyOverrides()
        {
            foreach (var (pktIndex, pktOverride) in _overrides)
            {
                // TODO: Does using "first" here make sense?
                InterfaceDescriptionBlock? iface = (_memoryPcapng.Interfaces.FirstOrDefault(iface =>
                    (LinkLayerType)iface.LinkType == (LinkLayerType)pktOverride.LinkLayer));
                int ifaceIndex = _memoryPcapng.Interfaces.IndexOf(iface);

                // Construct new packet, mostly using the old packet in the same index
                EnhancedPacketBlock originalPacket = _memoryPcapng.GetPacket(pktIndex);
                originalPacket.InterfaceID = ifaceIndex; // TODO: We might move packets between interface for no reason if the link types are the same
                originalPacket.Data = pktOverride.Data;
                List<string> comments = new List<string>();
                if (originalPacket is EnhancedPacketBlock originalEpb)
                {
                    // Readding packet comments EXCEPT any old Packet Studio Light comments
                    // (We are going to add our one, if required, anyway.
                    foreach (string comment in originalEpb.Options.Comments)
                    {
                        if (!PslcCommentsEncoder.TryDecode(comment, out _))
                            comments.Add(comment);
                    }
                }
                byte[] data = pktOverride.Data;
                comments.Add(PslcCommentsEncoder.Encode(pktOverride.OriginalText));
                EnhancedPacketBlock epb = new EnhancedPacketBlock(0,
                    new Haukcode.PcapngUtils.PcapNG.CommonTypes.TimestampHelper(originalPacket.Seconds, originalPacket.Microseconds),
                    data.Length,
                    data,
                    new Haukcode.PcapngUtils.PcapNG.OptionTypes.EnhancedPacketOption(comments));

                // Update packet!
                _memoryPcapng.UpdatePacket(pktIndex, epb);
            }
            _overrides.Clear();
        }

        private int? _lastPacketEditorOffset;
        private void packetsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (packetsListBox.SelectedIndex == -1)
            {
                _lastPacketEditorOffset = packetTextBox.CaretOffset;
                return;
            }


            IPacket pkt = _memoryPcapng.GetPacket(packetsListBox.SelectedIndex);
            byte[] data = pkt.Data;
            if (_overrides.TryGetValue(packetsListBox.SelectedIndex, out var pktOverride))
            {
                packetTextBox.Text = pktOverride.OriginalText;
            }
            else
            {
                bool recoveredFromComment = false;
                if (pkt is EnhancedPacketBlock epb)
                {
                    foreach (string comment in epb.Options.Comments)
                    {
                        if (PslcCommentsEncoder.TryDecode(comment, out string pktDescription))
                        {
                            // Found a PacketStudioLight packet desc, we'll use it for the text editor.
                            packetTextBox.Text = pktDescription;
                            recoveredFromComment = true;
                            break;
                        }
                    }
                }

                if (!recoveredFromComment)
                {
                    // Just use the bytes from the EPB
                    packetTextBox.Text = BitConverter.ToString(data).Replace("-", String.Empty);
                }
            }

            if (_lastPacketEditorOffset != null)
            {
                packetTextBox.CaretOffset = _lastPacketEditorOffset.Value;
                _lastPacketEditorOffset = null;
            }
        }

        // This overload is here because that's the signature for the event in case of AvalonEdit
        private void packetTextBox_TextChanged_Base(object sender, EventArgs e)
        {
            try
            {
                int pktIndex = packetsListBox.SelectedIndex;
                if (pktIndex == -1)
                    return;
                string originalText = packetTextBox.Text;

                string[] lines = packetTextBox.Text.Split('\n');

                byte[] data = null;
                PacketOverride pktOverride = null;
                if (lines.Length > 0 &&
                    lines[0].StartsWith("@") &&
                    lines[0].Contains("Generate: "))
                {
                    (string type, Dictionary<string, string> variables) = Parser.Parse(lines);


                    (Packet p, LinkLayers linkLayer) = PacketsGenerator.Generate(type, variables);

                    data = p.Bytes;
                    pktOverride = new PacketOverride()
                    {
                        Data = data,
                        OriginalText = originalText,
                        LinkLayer = linkLayer
                    };
                    _overrides[pktIndex] = pktOverride;
                }
                else
                {
                    // Normal HEX!

                    // Removing comment lines and whitespaces
                    string[] cleanedLines = lines
                        .Where(line => !line.TrimStart().StartsWith("//"))
                        .Select(line => line.Replace(" ", string.Empty)
                                            .Replace("\r", string.Empty)
                                            .Replace("\n", string.Empty))
                        .ToArray();
                    string joined = String.Join(String.Empty, cleanedLines);

                    data = GetBytesFromHex(joined);

                    // User changed the definition of the packet.
                    // If it's a custom definition (with new lines, comments, spaces to seperate bytes...)
                    // we want to store it as an "override".
                    // If it's just the normal hex stream (either because we JUST opened this packet OR the user
                    // restored the editor's state to unchanged) we DON'T want to restore it and further more - we
                    // want to get rid of any left over overrides.
                    //
                    // This checks if the hex editor actually has different content than the raw hex stream.
                    if (!joined.Equals(originalText, StringComparison.CurrentCultureIgnoreCase))
                    {
                        pktOverride = new PacketOverride
                        {
                            Data = data,
                            OriginalText = originalText,
                        };
                        _overrides[pktIndex] = pktOverride;
                    }
                    else
                    {
                        _overrides.Remove(pktIndex);
                    }
                }

                // No errors in hex


                IPacket pkt = _memoryPcapng.GetPacket(pktIndex);
                LinkLayerType llt = LinkLayerType.Ethernet;
                if (pkt is EnhancedPacketBlock)
                {
                    llt = (LinkLayerType)_memoryPcapng.Interfaces[((EnhancedPacketBlock)pkt).AssociatedInterfaceID.Value].LinkType;
                }

                if (pktOverride?.LinkLayer != null)
                    llt = (LinkLayerType)pktOverride.LinkLayer.Value;

                // BEWARE: LAZY CODE AHEAD
                bool packetChanged = false;
                if (!pkt.Data.SequenceEqual(data))
                {
                    packetChanged = true;
                    pktOverride = new PacketOverride
                    {
                        Data = data,
                        OriginalText = originalText,
                        LinkLayer = (LinkLayers)llt
                    };
                    _overrides[pktIndex] = pktOverride;
                    ApplyOverrides();

                    pkt = _memoryPcapng.GetPacket(pktIndex);
                    data = pkt.Data;
                }
;

                // Update tree
                var tsharkTask = op.GetPdmlAsync(new TempPacketSaveData(data, llt)).ContinueWith(t =>
                {
                    XElement pdml = t.Result;
                    this.Dispatcher.Invoke(() =>
                    {
                        XDocument doc = new XDocument(pdml);
                        this.treeView.DataContext = doc;
                        this.treeView.ItemsSource = doc.Root.Elements();
                    });
                });
                if (packetChanged)
                {
                    // Update packets list
                    UpdatePacketsList().ContinueWith((Action<Task>)(t => Dispatcher.Invoke(() =>
                    {
                        packetsListBox.SelectedIndex = pktIndex;
                    })));
                }
            }
            catch { }
        }

        public static byte[] GetBytesFromHex(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        private void ExportToWiresharkButtonClicked(object sender, RoutedEventArgs e)
        {
            string tempPath = Path.ChangeExtension(Path.GetTempFileName(), "pcapng");
            using FileStream fs = File.OpenWrite(tempPath);
            PcapNGWriter writer = new PcapNGWriter(fs);

            for (int pktIndex = 0; pktIndex < packetsListBox.Items.Count; pktIndex++)
            {
                IPacket originalPacket = _memoryPcapng.GetPacket(pktIndex);
                byte[] data = originalPacket.Data;

                List<string> comments = new List<string>();
                if (originalPacket is EnhancedPacketBlock originalEpb)
                {
                    // Readding packet comments EXCEPT any old Packet Studio Light comments
                    // (We are going to add our one, if required, anyway.
                    foreach (string comment in originalEpb.Options.Comments)
                    {
                        if (!PslcCommentsEncoder.TryDecode(comment, out _))
                            comments.Add(comment);
                    }
                }
                if (_overrides.TryGetValue(pktIndex, out var pktOverride))
                {
                    data = pktOverride.Data;
                    comments.Add(PslcCommentsEncoder.Encode(pktOverride.OriginalText));
                }
                EnhancedPacketBlock epb = new EnhancedPacketBlock(0,
                    new Haukcode.PcapngUtils.PcapNG.CommonTypes.TimestampHelper(originalPacket.Seconds, originalPacket.Microseconds),
                    data.Length,
                    data,
                    new Haukcode.PcapngUtils.PcapNG.OptionTypes.EnhancedPacketOption(comments));

                writer.WritePacket(epb);
            }

            writer.Close();
            fs.Close();
            Process.Start(@"C:\Development\wsbuild64\run\RelWithDebInfo\Wireshark.exe", tempPath);
        }

        private void AddLayersCommentsButtonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string originalText = packetTextBox.Text;
                string[] lines = packetTextBox.Text.Split('\n');
                // Removing comment lines and whitespaces
                string[] cleanedLines = lines
                    .Where(line => !line.TrimStart().StartsWith("//"))
                    .Select(line => line.Replace(" ", string.Empty)
                                        .Replace("\r", string.Empty)
                                        .Replace("\n", string.Empty))
                    .ToArray();
                string joined = String.Join(String.Empty, cleanedLines);

                // Calling this makes sure the hex is valid (otherwise an exception is thrown)
                byte[] data = GetBytesFromHex(joined);

                IPacket pkt = _memoryPcapng.GetPacket(packetsListBox.SelectedIndex);
                LinkLayerType llt = LinkLayerType.Ethernet;
                if (pkt is EnhancedPacketBlock)
                {
                    llt = (LinkLayerType)_memoryPcapng.Interfaces[((EnhancedPacketBlock)pkt).AssociatedInterfaceID.Value].LinkType;
                }


                var tsharkTask = op.GetJsonRawAsync(new TempPacketSaveData(data, llt)).ContinueWith(t =>
                {
                    Dictionary<int, string> indexToLayer = new Dictionary<int, string>();
                    JToken? layers = t.Result["_source"]["layers"];
                    foreach (var layer in layers)
                    {
                        string name = (layer as JProperty)?.Name;
                        if (name.EndsWith("_raw") && name != "frame_raw")
                        {
                            string? hex = ((layer.Values().First() as JValue).Value as string)?.ToUpper();
                            string layerName = name.Replace("_raw", String.Empty);
                            int index = originalText.IndexOf(hex, StringComparison.CurrentCultureIgnoreCase);
                            if (index != -1)
                            {
                                int layerIndex = index;
                                indexToLayer[layerIndex] = layerName;
                            }
                        }
                    }

                    var indices = indexToLayer.Keys.ToList();
                    indices.Sort();
                    indices.Reverse();
                    foreach (int index in indices)
                    {
                        string layerName = indexToLayer[index];
                        string commentToAdd = $"// {layerName.ToUpper()}\r\n";
                        if (index != 0)
                        {
                            commentToAdd = "\r\n" + commentToAdd;
                        }
                        originalText = originalText.Insert(index, commentToAdd);
                    }

                    this.Dispatcher.Invoke(() =>
                    {
                        packetTextBox.Text = originalText;
                    });
                });
            }
            catch
            { }
        }

        private void NormalizeHexClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string summary = packetsListBox.SelectedItem as string;
                string originalText = packetTextBox.Text.ToUpper();
                string[] lines = packetTextBox.Text.Split('\n');
                // Removing comment lines and whitespaces
                string[] cleanedLines = lines
                    .Where(line => !line.TrimStart().StartsWith("//"))
                    .Select(line => line.Replace(" ", string.Empty)
                                        .Replace("\r", string.Empty)
                                        .Replace("\n", string.Empty))
                    .ToArray();
                string joined = String.Join(String.Empty, cleanedLines);


                // Make sure the hex makes sense
                byte[] data = GetBytesFromHex(joined);

                packetTextBox.Text = joined;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Can't normalize hex.\r\nException:\r\n" + ex, "Error");

            }
        }

        private void ExitButtonClicked(object sender, RoutedEventArgs e)
        {
            // Running in task so the GUI doesn't freeze
            Task.Run(() => Environment.Exit(0));
        }

        private void SettingsButtonClicked(object sender, RoutedEventArgs e)
        {
            // TODO:
            SettingsWindow settings = new SettingsWindow();
            settings.Owner = this;
            settings.SetCurrentWiresharkDir(_wsDir);
            bool? results = settings.ShowDialog();
            if (results == true)
            {
                _wsDir = settings.SelectedWiresharkPath;
                Properties.Settings.Default.WiresharkDirectory = _wsDir;
                Properties.Settings.Default.Save();
            }
        }

        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Pcapng File (*.pcapng)|*.pcapng";
            bool? res = sfd.ShowDialog();
            if (res != true)
                return;
            string capPath = sfd.FileName;
            if (Path.GetExtension(capPath) != ".pcapng")
            {
                MessageBox.Show("Error: File did not have a .pcapng extension", "Error");
                return;
            }

            if (File.Exists(capPath))
            {
                var overrideRes = MessageBox.Show($"Overriding existing file {capPath} ?\n", "Save",
                    MessageBoxButton.YesNo);
                if (overrideRes != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            FileStream fs;
            try
            {
                fs = File.Open(capPath, FileMode.Create, FileAccess.Write);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't open destination file. Error:\n" + e);
                return;
            }


            ApplyOverrides();
            _memoryPcapng.WriteTo(fs);
            fs.Close();
        }

        private void PasteClicked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CopyClicked(object sender, RoutedEventArgs e)
        {
            packetTextBox.Copy();
            SetStatus("Copied.", isError: false);
        }

        private void CopyCSharpClicked(object sender, RoutedEventArgs e)
        {
            packetTextBox.Copy();
            var x = Clipboard.GetText() as string;

            byte[] bArr;
            try
            {
                bArr = Hextensions.GetBytesFromHex(x);
            }
            catch
            {
                SetStatus("Failed to parse selected text as hex.", isError: true);
                return;
            }

            string final = "new byte[] { 0x" + string.Join(", 0x", bArr.Select(b => b.ToString("X2"))) + " }";
            Clipboard.SetText(final);
            SetStatus("Copied.", isError: false);
        }

        private void SetStatus(string status, bool isError)
        {
            statusBarStatusLabel.Text = status;
            okStatusImage.Visibility = isError ? Visibility.Collapsed : Visibility.Visible;
            errorStatusImage.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CutClicked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ChangeHexEditorZeroesEmphasis(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mi))
            {
                return;
            }

            mi.IsChecked = !mi.IsChecked;
            bool deEmphasis = mi.IsChecked;

            string highlights = "MyHighlighting.xshd";
            if (deEmphasis)
                highlights = "MyHighlighting_00.xshd";

            using (Stream s = File.OpenRead(highlights))
            {
                using (XmlReader reader = new XmlTextReader(s))
                {
                    packetTextBox.SyntaxHighlighting =
                        HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }

        private async void NewFileClicked(object sender, RoutedEventArgs e)
        {
            using (new FileLoadUiFreezeContext(this, "Creating..."))
            {
                TempPacketsSaver saver = new TempPacketsSaver();
                string tempFile = await Task.Run(() => saver.WritePacket(
                    new TempPacketSaveData(new byte[] { 0x11, 0x22, 0x33 }, LinkLayerType.Ethernet)));

                _memoryPcapng = MemoryPcapng.ParsePcapng(tempFile);
                await UpdatePacketsList();
            }
        }

    }
}
