using System.Collections.ObjectModel;
using System.Diagnostics;

namespace MemoryPcapng;

public class PacketDescriptionsList
{
    public ObservableCollection<string> Descriptions { get; set; }

    private Action<ObservableCollection<string>, TSharkOutputEvent> _addAction;
    private readonly Action<ObservableCollection<string>, int> _removeAction;

    public PacketDescriptionsList(ObservableCollection<string> descriptions,
        Action<ObservableCollection<string>, TSharkOutputEvent> addAction,
        Action<ObservableCollection<string>, int> removeAction
        )
    {
        Descriptions = descriptions;
        _addAction = addAction;
        _removeAction = removeAction;
    }

    public Task UpdateAsync(Pcapng pcapng)
    {
        Debug.WriteLine($"[{DateTime.Now}] Starting task from main thread...");
        return Task.Run(() =>
        {
            Debug.WriteLine($"[{DateTime.Now}] TSharking...");
            bool useImmediateFlushFlag = pcapng.PacketsCount < 50;
            using TShark tshark = new TShark(@"C:\Program Files\Wireshark\tshark.exe", useImmediateFlushFlag);
            Debug.WriteLine($"[{DateTime.Now}] Opened.");
            tshark.NewPacketLine += TsharkOnNewPacketLine;
            Debug.WriteLine($"[{DateTime.Now}] write to...");
            pcapng.WriteTo(tshark.Pipe);
            Debug.WriteLine($"[{DateTime.Now}] written.");
            Debug.WriteLine($"[{DateTime.Now}] Closing pipe to TShark.");
            tshark.Pipe.Close();
            Debug.WriteLine($"[{DateTime.Now}] Waiting for packets in STDOUT");
            tshark.WaitForPackets(pcapng.PacketsCount);
            Debug.WriteLine($"[{DateTime.Now}] GOT all packets in STDOUT");

            // Remove trailing old packets
            Debug.WriteLine($"[{DateTime.Now}] wrapping up...");
            while (Descriptions.Count > pcapng.PacketsCount)
            {
                _removeAction(Descriptions, pcapng.PacketsCount);
            }

            tshark.NewPacketLine -= TsharkOnNewPacketLine;
            Debug.WriteLine($"[{DateTime.Now}] wrapping up... DONE");
        });
    }

    private void TsharkOnNewPacketLine(object? sender, TSharkOutputEvent e)
    {
        _addAction(Descriptions, e);
    }

    public static void NormalDescriptionsUpdate(ObservableCollection<string> descs, TSharkOutputEvent e)
    {
        if (descs.Count == e.index)
            descs.Add(e.Line);
        else
            descs[e.index] = e.Line;
    }
    public static void NormalDescriptionsRemover(ObservableCollection<string> descs, int index)
    {
        descs.RemoveAt(index);
    }
}