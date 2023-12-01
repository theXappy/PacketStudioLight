using System.Diagnostics;

namespace MemoryPcapng
{
    public class Pcapng
    {
        private static int EnhancedPacketBlockType = 0x00_00_00_06;
        private static int InterfaceDescriptionBlockType = 0x00_00_00_01;

        private SemaphoreSlim _dataSemaphore = new(1);
        private Memory<byte> _sectionHeader;
        private List<Memory<byte>> _interfaceBlocks;
        private List<Memory<byte>> _packetBlocks;
        public int InterfacesCount => _interfaceBlocks.Count;
        public int PacketsCount => _packetBlocks.Count;
        private int Size => _sectionHeader.Length + _interfaceBlocks.Sum(iface => iface.Length) + _packetBlocks.Sum(b => b.Length);

        private Pcapng(Memory<byte> sectionHeader, List<Memory<byte>> interfaces, List<Memory<byte>> packetBlocks)
        {
            _sectionHeader = sectionHeader;
            _interfaceBlocks = interfaces;
            _packetBlocks = packetBlocks;
        }

        public static Pcapng FromFile(string path) => FromBytes(File.ReadAllBytes(path));
        public static Pcapng FromBytes(byte[] bytes)
        {
            // Naive parse
            int offset = 0;
            int firstBlockLen = BitConverter.ToInt32(bytes.AsSpan(offset + 4, 4));
            Memory<byte> header = bytes.AsMemory(offset, firstBlockLen);
            offset += firstBlockLen;

            List<Memory<byte>> ifaces = new List<Memory<byte>>();
            while (offset < bytes.Length)
            {
                int type = BitConverter.ToInt32(bytes.AsSpan(offset, 4));
                int len = BitConverter.ToInt32(bytes.AsSpan(offset + 4, 4));
                if (type != InterfaceDescriptionBlockType)
                {
                    // Assuming we reached the end of the packets list. move on to 'packet blocks' loop
                    // Note we specifically DON'T move `offset` here.
                    break;
                }
                Memory<byte> packet = bytes.AsMemory(offset, len);
                ifaces.Add(packet);
                offset += len;
            }

            List<Memory<byte>> packets = new List<Memory<byte>>();
            while (offset < bytes.Length)
            {
                int type = BitConverter.ToInt32(bytes.AsSpan(offset, 4));
                int len = BitConverter.ToInt32(bytes.AsSpan(offset + 4, 4));
                if (type != EnhancedPacketBlockType)
                {
                    // Assuming we reached the end of the packets list. move on to 'boring blocks' loop
                    // Note we specifically DON'T move `offset` here.
                    break;
                }
                Memory<byte> packet = bytes.AsMemory(offset, len);
                packets.Add(packet);
                offset += len;
            }

            // Drop boring blocks
            while (offset < bytes.Length)
            {
                int type = BitConverter.ToInt32(bytes.AsSpan(offset, 4));
                int len = BitConverter.ToInt32(bytes.AsSpan(offset + 4, 4));
                if (type == EnhancedPacketBlockType)
                    throw new Exception("Ahhhhhhhhhhhh");
                Memory<byte> packet = bytes.AsMemory(offset, len);
                offset += len;
            }

            return new Pcapng(header, ifaces, packets);
        }

        public Memory<byte> GetSectionHeaderBlock()
        {
            return _sectionHeader;
        }

        public Memory<byte> GetPacketBlock(int index)
        {
            _dataSemaphore.Wait();
            var packetBlock = _packetBlocks[index];
            _dataSemaphore.Release();
            return packetBlock;
        }

        public void SetPacketBlock(int index, Memory<byte> packet)
        {
            _dataSemaphore.Wait();
            if (index == _packetBlocks.Count)
                _packetBlocks.Add(packet);
            else if (index < _packetBlocks.Count)
                _packetBlocks[index] = packet;
            else
                throw new ArgumentOutOfRangeException("Packet index out of range");
            _dataSemaphore.Release();
        }

        public void MovePacketBlock(int fromIndex, int toIndex)
        {
            _dataSemaphore.Wait();
            Memory<byte> block = _packetBlocks[fromIndex];
            _packetBlocks.RemoveAt(fromIndex);
            _packetBlocks.Insert(toIndex, block);
            _dataSemaphore.Release();
        }

        public void AppendPacketBlock(Memory<byte> packet)
        {
            SetPacketBlock(_packetBlocks.Count, packet);
        }
        public void RemovePacketBlock(int index)
        {
            _dataSemaphore.Wait();
            _packetBlocks.RemoveAt(index);
            _dataSemaphore.Release();
        }

        public void WriteTo(Stream s)
        {
            _dataSemaphore.Wait();
            try
            {
                BufferedStream bufferedStream = new BufferedStream(s, 50_000);

                bufferedStream.Write(_sectionHeader.Span);
                foreach (Memory<byte> ifaceBlock in _interfaceBlocks)
                {
                    bufferedStream.Write(ifaceBlock.Span);
                }
                bufferedStream.Flush();

                Memory<byte> last = default;
                foreach (Memory<byte> packetBlock in _packetBlocks)
                {
                    bufferedStream.Write(packetBlock.Span);
                }
                bufferedStream.Flush();

                Debug.WriteLine("done");
            }
            finally
            {
                _dataSemaphore.Release();
            }
        }

        public async Task WriteToAsync(Stream s)
        {
            await _dataSemaphore.WaitAsync();
            try
            {
                await s.WriteAsync(_sectionHeader);
                foreach (Memory<byte> ifaceBlock in _interfaceBlocks)
                {
                    await s.WriteAsync(ifaceBlock);
                }
                foreach (Memory<byte> packetBlock in _packetBlocks)
                {
                    await s.WriteAsync(packetBlock);
                }
            }
            finally
            {
                _dataSemaphore.Release();
            }

        }

        public byte[] ToBytes()
        {
            byte[] output = new byte[Size];
            MemoryStream memStream = new MemoryStream(output);
            WriteTo(memStream);
            //int offset = 0;
            //_header.CopyTo(output.AsMemory(offset));
            //offset += _header.Length;
            //foreach (Memory<byte> ifaceBlock in _interfaceBlocks)
            //{
            //    ifaceBlock.CopyTo(output.AsMemory(offset));
            //    offset += ifaceBlock.Length;
            //}
            //foreach (Memory<byte> packetBlock in _packetBlocks)
            //{
            //    packetBlock.CopyTo(output.AsMemory(offset));
            //    offset += packetBlock.Length;
            //}
            return output;
        }

        public IReadOnlyList<Memory<byte>> GetInterfaceBlocks()
        {
            _dataSemaphore.Wait();
            var ifaces = _interfaceBlocks.ToList();
            _dataSemaphore.Release();
            return ifaces;
        }

        public Memory<byte> GetInterfaceBlock(int interfaceID)
        {
            _dataSemaphore.Wait();
            var iface = _interfaceBlocks[interfaceID];
            _dataSemaphore.Release();
            return iface;
        }

        public void AppendInterfaceBlock(Memory<byte> ifaceBlock)
        {
            _dataSemaphore.Wait();
            _interfaceBlocks.Add(ifaceBlock);
            _dataSemaphore.Release();
        }
    }



}