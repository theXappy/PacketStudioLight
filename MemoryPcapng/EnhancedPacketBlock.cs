namespace MemoryPcapng
{
    public static class BinUtils
    {
        public static int CellingPadTo4(int num)
        {
            if (num % 4 == 0)
                return num;
            return num + (4 - num % 4);
        }
    }

    public ref struct EnhancedPacketBlock
    {
        private Memory<byte> _memory;
        public Memory<byte> Memory
        {
            readonly get => _memory;
            set
            {
                if (!_memory.Equals(default))
                {
                    // Secondary set, it is "changed" from the init
                    BackingMemoryChanged = true;
                }
                // Init, not "changed"
                _memory = value;
            }
        }

        public bool BackingMemoryChanged = false;

        public EnhancedPacketBlock(Memory<byte> memory)
        {
            Memory = memory;
        }

        public int BlockType
        {
            get => BitConverter.ToInt32(Memory[..4].Span);
            set => BitConverter.GetBytes(value).CopyTo(Memory[..4]); // TODO: Should this be allowed?
        }

        public int BlockTotalLength
        {
            get => BitConverter.ToInt32(Memory[4..8].Span);
            set
            {
                byte[] encoded = BitConverter.GetBytes(value);
                encoded.CopyTo(Memory[4..8]);
                encoded.CopyTo(Memory[^4..]);
            }
        }

        public int InterfaceID
        {
            get => BitConverter.ToInt32(Memory[8..12].Span);
            set => BitConverter.GetBytes(value).CopyTo(Memory[8..12]);
        }
        public long Timestamp
        {
            get => BitConverter.ToInt64(Memory[12..20].Span);
            set => BitConverter.GetBytes(value).CopyTo(Memory[12..20]);
        }

        public int CapturedPacketLength
        {
            get => BitConverter.ToInt32(Memory[20..24].Span);
            set => BitConverter.GetBytes(value).CopyTo(Memory[20..24]);
        }
        public int OriginalPacketLength
        {
            get => BitConverter.ToInt32(Memory[24..28].Span);
            set => BitConverter.GetBytes(value).CopyTo(Memory[24..28]);
        }
        public Memory<byte> PacketData
        {
            get => Memory[28..(28 + CapturedPacketLength)];
            set
            {
                if (value.Length == CapturedPacketLength)
                {
                    // Easy: Can insert new data into old Memory<byte>
                    value.CopyTo(Memory[28..(28 + CapturedPacketLength)]);
                    return;
                }

                // Hard: Need copying

                byte[] newData = new byte[28 + BinUtils.CellingPadTo4(value.Length) + OptionsLength + 4];
                Memory[..28].CopyTo(newData.AsMemory(0, 28));
                value.CopyTo(newData.AsMemory(28, value.Length));
                Options.CopyTo(newData.AsMemory(28 + BinUtils.CellingPadTo4(value.Length), OptionsLength));
                // Not copying Total block size, we will update in a second anyway.

                Memory = newData.AsMemory();
                CapturedPacketLength = value.Length;
                OriginalPacketLength = value.Length;
                BlockTotalLength = newData.Length;
            }
        }

        public Memory<byte> Options
        {
            get
            {
                int start = BinUtils.CellingPadTo4(28 + CapturedPacketLength);
                return Memory[start..^4];
            }
            set
            {
                if (value.Length % 4 != 0)
                {
                    throw new ArgumentException("Options section must align to 4-bytes boundary.");
                }

                if (value.Length == OptionsLength)
                {
                    // Easy: Can insert new data into old Memory<byte>
                    value.CopyTo(Memory[(28 + CapturedPacketLength)..^4]);
                    return;
                }

                // Hard: Need copying
                byte[] newData = new byte[28 + BinUtils.CellingPadTo4(CapturedPacketLength) + value.Length + 4];
                Memory[..(28 + CapturedPacketLength)].CopyTo(newData.AsMemory(0, 28 + CapturedPacketLength));
                value.CopyTo(newData.AsMemory(28 + BinUtils.CellingPadTo4(CapturedPacketLength), value.Length));
                // Not copying Total block size, we will update in a second anyway.

                Memory = newData.AsMemory();
                BlockTotalLength = newData.Length;
            }
        }

        public int OptionsLength => Options.Length;

        public override string ToString()
        {
            return $"Enhanced Packet Block:\n" +
                   $"  Block Type: {BlockType}\n" +
                   $"  Block Total Length: {BlockTotalLength}\n" +
                   $"  Interface ID: {InterfaceID}\n" +
                   $"  Timestamp: {Timestamp}\n" +
                   $"  Captured Packet Length: {CapturedPacketLength}\n" +
                   $"  Original Packet Length: {OriginalPacketLength}\n" +
                   $"  Packet Data Length: {PacketData.Length}\n" +
                   $"  Options Length: {OptionsLength}\n" +
                   $"  Backing Memory Changed: {BackingMemoryChanged}\n";
        }
    }
}
