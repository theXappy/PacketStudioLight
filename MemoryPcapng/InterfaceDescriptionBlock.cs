namespace MemoryPcapng;

public ref struct InterfaceDescriptionBlock
{
    private Memory<byte> _originalMemory;
    public Memory<byte> Memory { get; set; }
    public bool BackingMemoryChanged => Memory.Equals(_originalMemory);

    public InterfaceDescriptionBlock(Memory<byte> memory)
    {
        Memory = memory;
    }

    public int BlockType
    {
        get => BitConverter.ToInt32(Memory[..4].Span);
        set => BitConverter.GetBytes(value).CopyTo(Memory[..4]);
    }

    public int BlockTotalLength
    {
        get => BitConverter.ToInt32(Memory[4..8].Span);
        set => BitConverter.GetBytes(value).CopyTo(Memory[4..8]);
    }

    public short LinkType
    {
        get => BitConverter.ToInt16(Memory[8..10].Span);
        set => BitConverter.GetBytes(value).CopyTo(Memory[8..10]);
    }

    public short Reserved
    {
        get => BitConverter.ToInt16(Memory[10..12].Span);
        set => BitConverter.GetBytes(value).CopyTo(Memory[10..12]);
    }

    public int SnapLen
    {
        get => BitConverter.ToInt32(Memory[12..16].Span);
        set => BitConverter.GetBytes(value).CopyTo(Memory[12..16]);
    }

    public Memory<byte> Options
    {
        get => Memory[16..^4];
        set
        {
            if (value.Length == OptionsLength)
            {
                value.CopyTo(Memory[16..^4]);
                return;
            }
            throw new NotImplementedException("Can't set Options field in InterfaceDescriptionBlock");
        }
    }

    public int OptionsLength => Options.Length;
}