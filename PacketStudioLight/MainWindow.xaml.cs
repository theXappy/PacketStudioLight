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

namespace PacketStudioLight
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        string _wsDir;
        string WiresharkPath => Path.Combine(_wsDir, "wireshark.exe");
        string TsharkPath => Path.Combine(_wsDir, "tshark.exe");
        TSharkInterop op => new TSharkInterop(TsharkPath);


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

        Dictionary<int, InterfaceDescriptionBlock> ifaces;
        Dictionary<string, IPacket> packets;
        Dictionary<string, Tuple<byte[], string>> overrides;

        private async void OpenButtonClicked(object sender, RoutedEventArgs e)
        {
            packets = new Dictionary<string, IPacket>();
            overrides = new Dictionary<string, Tuple<byte[], string>>();
            ifaces = new Dictionary<int, InterfaceDescriptionBlock>();


            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Captures (*.pcapng)|*.pcapng";
            bool? res = ofd.ShowDialog();
            if (res != true)
                return;
            string capPath = ofd.FileName;


            string[]? descs = await op.GetPacketsDescriptions(capPath);

            FileStream fs;
            try
            {
                fs = File.OpenRead(capPath);
            }
            catch
            {
                return;
            }

            PcapNGReader? reader = new PcapNGReader(fs, false);
            int j = 0;
            foreach (var x in reader.HeadersWithInterfaceDescriptions)
            {
                foreach (var iface in x.InterfaceDescriptions)
                {
                    ifaces.Add(j, iface);
                    j++;
                }
            }

            packetsList.Items.Clear();
            int i = 0;
            while (true)
            {
                if (!reader.MoreAvailable)
                    break;
                IPacket pkt = reader.ReadNextPacket();
                if (pkt == null)
                    return;
                string summary = descs[i].TrimEnd();
                packets.Add(summary, pkt);
                packetsList.Items.Add(summary);
                if (pkt is EnhancedPacketBlock epb)
                {
                    // Pack Studio Light might've saved some data in one of the comments.
                    // We are iterating them until we find one in the right format
                    foreach (string comment in epb.Options.Comments)
                    {
                        if (PslcCommentsEncoder.TryDecode(comment, out string pslRepresentation))
                        {
                            overrides.Add(summary, new Tuple<byte[], string>(pkt.Data, pslRepresentation));
                            break;
                        }
                    }
                }

                i++;
            }
        }

        private void packetsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string summary = packetsList.SelectedItem as string;
            if (summary == null)
                return;

            IPacket pkt = packets[summary];
            byte[] data = pkt.Data;
            if (overrides.TryGetValue(summary, out Tuple<byte[], string>? newData))
            {
                packetTextBox.Text = newData.Item2;
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
                string summary = packetsList.SelectedItem as string;
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

                byte[] data = GetBytesFromHex(joined);

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
                    overrides[summary] = new Tuple<byte[], string>(data, originalText);
                }
                else
                {
                    overrides.Remove(summary);
                }

                // No errors in hex


                IPacket pkt = packets[summary];
                LinkLayerType llt = LinkLayerType.Ethernet;
                if (pkt is EnhancedPacketBlock)
                {
                    llt = (LinkLayerType)ifaces[((EnhancedPacketBlock)pkt).AssociatedInterfaceID.Value].LinkType;
                }
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

            foreach (string summary in packetsList.Items.Cast<string>())
            {
                IPacket originalPacket = packets[summary];
                byte[] data = originalPacket.Data;

                List<string> comments = new List<string>();
                if (originalPacket is EnhancedPacketBlock originalEpb) {
                    // Readding packet comments EXCEPT any old Packet Studio Light comments
                    // (We are going to add our one, if required, anyway.
                    foreach(string comment in originalEpb.Options.Comments)
                    {
                        if(!PslcCommentsEncoder.TryDecode(comment, out _))
                            comments.Add(comment);
                    }
                }
                if (overrides.TryGetValue(summary, out Tuple<byte[], string>? newData))
                {
                    data = newData.Item1;
                    comments.Add(PslcCommentsEncoder.Encode(newData.Item2));
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
                string summary = packetsList.SelectedItem as string;
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

                IPacket pkt = packets[summary];
                LinkLayerType llt = LinkLayerType.Ethernet;
                if (pkt is EnhancedPacketBlock)
                {
                    llt = (LinkLayerType)ifaces[((EnhancedPacketBlock)pkt).AssociatedInterfaceID.Value].LinkType;
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
                string summary = packetsList.SelectedItem as string;
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
