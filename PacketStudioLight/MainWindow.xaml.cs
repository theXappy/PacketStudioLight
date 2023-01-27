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
using System.Windows.Controls.Ribbon;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Xml;
using FastPcapng;
using PacketGen;
using PacketDotNet;
using PacketStudioLight.Extensions;
using GongSolutions.Wpf.DragDrop;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace PacketStudioLight
{
    class PacketsListBoxViewModel : IDropTarget
    {
        public ObservableCollection<string> MyPacketsList { get; set; }

        public event EventHandler<PacketMovedEventArgs> Updated;

        public PacketsListBoxViewModel(ObservableCollection<string> myPacketsList)
        {
            MyPacketsList = myPacketsList;
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            //ExampleItemViewModel sourceItem = dropInfo.Data as ExampleItemViewModel;
            //ExampleItemViewModel targetItem = dropInfo.TargetItem as ExampleItemViewModel;

            //if (sourceItem != null && targetItem != null && targetItem.CanAcceptChildren)
            //{
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Move;
            //}
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            int toIndex = dropInfo.InsertIndex;
            int fromIndex = MyPacketsList.IndexOf(dropInfo.Data as string);
            if (toIndex > fromIndex)
            {
                // When removing the packet from the current index, it moves the dest index back by 1
                toIndex = toIndex - 1;
            }
            if (fromIndex == toIndex)
                return;
            // First let's move the UI elemenmts to show responsiveness to the user
            string textualItemToMove = MyPacketsList[fromIndex];
            MyPacketsList.RemoveAt(fromIndex);
            MyPacketsList.Insert(toIndex, textualItemToMove);

            // Finally ask the window to move pacets in the memory pcapng and  re-generate a packets list
            // (to, at least, fix indexes & timestamps. Possibly also the "info column" would be affected)
            Updated?.Invoke(this, new PacketMovedEventArgs(fromIndex, toIndex));
        }
    }

    public class PacketMovedEventArgs : EventArgs
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }

        public PacketMovedEventArgs(int fromIndex, int toIndex)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string _wsDir;
        string WiresharkPath => Path.Combine(_wsDir, "wireshark.exe");
        string TsharkPath => Path.Combine(_wsDir, "tshark.exe");
        TSharkInterop op => new TSharkInterop(TsharkPath);

        private MemoryPcapng _memoryPcapng;

        public MainWindow()
        {
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

        }

        private class PacketOverride
        {
            public byte[] Data { get; set; }
            public string OriginalText { get; set; }
            public LinkLayers? LinkLayer { get; set; }
        }

        Dictionary<string, IPacket> _packetsDict;
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
            packetsListBox.SelectedIndex = -1;
            packetsListBox.DataContext = null;
            packetsListBox.IsEnabled = false;
            packetTextBox.Text = "";
            statusLabel.Content = "Loading...";
            statusLabel.Visibility = Visibility.Visible;
            _packetsDict = new Dictionary<string, IPacket>();
            _overrides = new();


            _memoryPcapng = MemoryPcapng.ParsePcapng(capPath);
            await UpdatePacketsList();

            packetsListBox.IsEnabled = true;
            statusLabel.Visibility = Visibility.Collapsed;

            //string[]? descs = await op.GetPacketsDescriptions(capPath);

            //FileStream fs;
            //try
            //{
            //    fs = File.OpenRead(capPath);
            //}
            //catch
            //{
            //    return;
            //}

            //PcapNGReader? reader = new PcapNGReader(fs, false);
            //int j = 0;
            //foreach (var x in reader.HeadersWithInterfaceDescriptions)
            //{
            //    foreach (var iface in x.InterfaceDescriptions)
            //    {
            //        ifaces.Add(j, iface);
            //        j++;
            //    }
            //}

            //packetsListBox.Items.Clear();
            //int i = 0;
            //while (true)
            //{
            //    if (!reader.MoreAvailable)
            //        break;
            //    IPacket pkt = reader.ReadNextPacket();
            //    if (pkt == null)
            //        break;
            //    string summary = descs[i].TrimEnd();
            //    packetsDict.Add(summary, pkt);
            //    packetsListBox.Items.Add(summary);
            //    if (pkt is EnhancedPacketBlock epb)
            //    {
            //        // Packet Studio Light might've saved some data in one of the comments.
            //        // We are iterating them until we find one in the right format
            //        foreach (string comment in epb.Options.Comments)
            //        {
            //            if (PslcCommentsEncoder.TryDecode(comment, out string pslRepresentation))
            //            {
            //                overrides.Add(summary, new PacketOverride()
            //                {
            //                    Data = pkt.Data,
            //                    OriginalText = pslRepresentation,
            //                });
            //                break;
            //            }
            //        }
            //    }
            //    i++;
            //}
            //packetsCountLabel.Content = i.ToString();
        }

        private async Task UpdatePacketsList()
        {
            string[] results = await op.GetPacketsDescriptions(_memoryPcapng);
            var newDataContext = new PacketsListBoxViewModel(new ObservableCollection<string>(results));
            newDataContext.Updated += HandlePacketsDraggedAndDropped;
            packetsListBox.DataContext = newDataContext;
        }

        private void HandlePacketsDraggedAndDropped(object? sender, PacketMovedEventArgs e)
            => this.Dispatcher.Invoke(() => HandlePacketsDraggedAndDroppedUI(sender, e));
        private async void HandlePacketsDraggedAndDroppedUI(object? sender, PacketMovedEventArgs e)
        {
            statusLabel.Content = "Reordering...";
            statusLabel.Visibility = Visibility.Visible;

            // Now let's move the Packet Blocks in the memory pcap
            ApplyOverrides();
            await Task.Run(() => _memoryPcapng.MovePacket(e.FromIndex, e.ToIndex));

            PacketsListBoxViewModel vm = sender as PacketsListBoxViewModel;
            vm.Updated -= HandlePacketsDraggedAndDropped;

            await UpdatePacketsList();

            statusLabel.Visibility = Visibility.Hidden;
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

        private void packetsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (packetsListBox.SelectedIndex == -1)
                return;
            IPacket pkt = _memoryPcapng.GetPacket(packetsListBox.SelectedIndex);
            byte[] data = pkt.Data;
            if (_overrides.TryGetValue(packetsListBox.SelectedIndex, out var pktOverride))
            {
                packetTextBox.Text = pktOverride.OriginalText;
            }
            else
            {
                packetTextBox.Text = BitConverter.ToString(data).Replace("-", String.Empty);
            }
        }

        // This stays here in case we go back to a simple text box
        private void packetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => packetTextBox_TextChanged_Base(sender, e);
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


                    Packet p = PacketsGenerator.Generate(type, variables);

                    data = p.Bytes;
                    pktOverride = new PacketOverride()
                    {
                        Data = data,
                        OriginalText = originalText,
                        LinkLayer = p.GetLayerType()
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

                    // User changed the definiton of the packet.
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
                string summary = packetsListBox.SelectedItem as string;
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

                IPacket pkt = _packetsDict[summary];
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

        private void SelectWiresharkVersionButtonClicked(object sender, RoutedEventArgs e)
        {
            // TODO
            MessageBox.Show("Not supported yet. Current version: " + WiresharkPath);
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
    }
}
