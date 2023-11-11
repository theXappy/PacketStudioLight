namespace MemoryPcapng;

public class TSharkOutputEvent
{
    public int index { get; private set; }
    public string Line { get; private set; }

    public TSharkOutputEvent(int index, string line)
    {
        this.index = index;
        Line = line;
    }
}