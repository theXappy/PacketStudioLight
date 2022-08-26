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
using System.Windows.Controls;
using System.Windows.Media;

namespace PacketStudioLight
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        string _wsDir = @"C:\Development\wsbuild64\run\RelWithDebInfo\";
        string WiresharkPath => Path.Combine(_wsDir, "wireshark.exe");
        string TsharkPath => Path.Combine(_wsDir, "tshark.exe");
        TSharkInterop op => new TSharkInterop(TsharkPath);


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

        private void ExportToWiresharkButtonClicked (object sender, RoutedEventArgs e)
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

        private void ExitButtonClicked(object sender, RoutedEventArgs e) => Environment.Exit(0);

        private void SettingsButtonClicked(object sender, RoutedEventArgs e)
        {
            // TODO:
            MessageBox.Show("Not settings yet.");
        }
    }
    public class StretchingTreeViewItem : TreeViewItem
    {
        static XElementToColorConverter _colorConverter = new XElementToColorConverter();

        public StretchingTreeViewItem()
        {
            this.Loaded += new RoutedEventHandler(StretchingTreeViewItem_Loaded);
            this.Selected += StretchingTreeViewItem_Selected;
        }

        private void StretchingTreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            this.Background = Brushes.AliceBlue;
        }

        private void StretchingTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            var dataContext = (sender as System.Windows.FrameworkElement)?.DataContext;
            this.Background = _colorConverter.Convert(dataContext, typeof(Brush), new object(), System.Globalization.CultureInfo.CurrentCulture) as Brush;
            this.Resources[SystemColors.HighlightBrush] = Brushes.Green;
            this.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = Brushes.Green;
            if (this.VisualChildrenCount > 0)
            {
                Grid grid = this.GetVisualChild(0) as Grid;
                if (grid != null && grid.ColumnDefinitions.Count == 3)
                {
                    grid.ColumnDefinitions.RemoveAt(2);
                    grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }
    }
    public class StretchingTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }
    }
}
