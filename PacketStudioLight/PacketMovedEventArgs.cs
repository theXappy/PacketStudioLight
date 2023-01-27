using System;

namespace PacketStudioLight;

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