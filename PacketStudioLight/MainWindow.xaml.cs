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

namespace PacketStudioLight
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TSharkInterop op = new TSharkInterop(@"C:\Development\wsbuild64\run\RelWithDebInfo\tshark.exe");


        public MainWindow()
        {
            InitializeComponent();
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
            ofd.ShowDialog();
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
                string summary = descs[i].TrimEnd();
                packets.Add(summary, pkt);
                packetsList.Items.Add(summary);
                i++;
            }
        }

        private void packetsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string summary = packetsList.SelectedItem as string;

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

        private void packetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

                byte[] data = GetBytesFromHex(joined);

                overrides[summary] = new Tuple<byte[], string>(data, originalText);

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = Path.ChangeExtension(Path.GetTempFileName(), "pcapng");
            using FileStream fs = File.OpenWrite(tempPath);
            PcapNGWriter writer = new PcapNGWriter(fs);

            foreach (string summary in packetsList.Items.Cast<string>())
            {
                IPacket originalPacket = packets[summary];
                byte[] data = originalPacket.Data;
                if (overrides.TryGetValue(summary, out Tuple<byte[], string>? newData))
                {
                    data = newData.Item1;
                }
                EnhancedPacketBlock epb = new EnhancedPacketBlock(0,
                    new Haukcode.PcapngUtils.PcapNG.CommonTypes.TimestampHelper(originalPacket.Seconds, originalPacket.Microseconds),
                    data.Length,
                    data,
                    new Haukcode.PcapngUtils.PcapNG.OptionTypes.EnhancedPacketOption());

                writer.WritePacket(epb);
            }

            writer.Close();
            fs.Close();
            Process.Start(@"C:\Development\wsbuild64\run\RelWithDebInfo\Wireshark.exe", tempPath);
        }

        private void SpecifyLayersButtonClicked(object sender, RoutedEventArgs e)
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
                            int index = originalText.IndexOf(hex);
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
    }
}
