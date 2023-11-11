using System.Text;

namespace MemoryPcapng
{
    public class BlockOptions
    {
        public static ushort opt_endofopt = 0;
        public static ushort opt_comment = 1;

        private Dictionary<ushort, List<byte[]>> _opTypeToValue = new();

        public void Add(ushort opType, byte[] value)
        {
            if (!_opTypeToValue.ContainsKey(opType))
            {
                _opTypeToValue[opType] = new List<byte[]>();
            }
            _opTypeToValue[opType].Add(value);
        }

        public IReadOnlyList<byte[]> GetOptionValue(ushort opType)
        {
            if (!_opTypeToValue.ContainsKey(opType))
                return new List<byte[]>();
            return _opTypeToValue[opType].ToList();
        }

        public byte[] GetOptionValueSingle(ushort opType)
        {
            return _opTypeToValue[opType].Single();
        }

        public void RemoveOptionValue(ushort opType, byte[] value)
        {
            if (!_opTypeToValue.ContainsKey(opType))
                return;

            _opTypeToValue[opType].Remove(value);
            // Check if this was the last value
            if (_opTypeToValue[opType].Count == 0)
                _opTypeToValue.Remove(opType);
        }

        public IReadOnlyList<string> GetComments() => GetOptionValue(opt_comment).Select(Encoding.UTF8.GetString).ToList();

        public void RemoveComment(string comment)
        {
            IReadOnlyList<byte[]> commentsList = GetOptionValue(opt_comment);
            foreach (var commentBytes in commentsList)
            {
                string temp = Encoding.UTF8.GetString(commentBytes);
                if (temp != comment)
                    continue;

                RemoveOptionValue(opt_comment, commentBytes);
                break;
            }
        }
        public void AddComment(string comment) => Add(opt_comment, Encoding.UTF8.GetBytes(comment));

        public int CalculateSize()
        {
            int size = 0;
            foreach (var kvp in _opTypeToValue)
            {
                size += 2; // For option type
                foreach (byte[] bytes in kvp.Value)
                {
                    size += 2; // For value length
                    size += bytes.Length;
                    // Pad to 32bits
                    if (size % 4 != 0)
                    {
                        size += 4 - (size % 4);
                    }
                }
            }
            return size;
        }

        public void WriteTo(Memory<byte> output)
        {
            if (output.Length < CalculateSize())
            {
                throw new ArgumentException("Output Memory<byte> too small.");
            }

            bool addEndOfOptions = false;
            int offset = 0;
            foreach (var kvp in _opTypeToValue)
            {
                if (kvp.Key == opt_endofopt)
                {
                    // Skipping, this should be written last
                    addEndOfOptions = true;
                    continue;
                }


                BitConverter.GetBytes(kvp.Key).CopyTo(output[offset..(offset + 2)]);
                offset += 2;
                foreach (byte[] value in kvp.Value)
                {
                    BitConverter.GetBytes((ushort)value.Length).CopyTo(output[offset..(offset + 2)]);
                    offset += 2;
                    value.CopyTo(output[offset..(offset + value.Length)]);
                    offset += value.Length;
                    // Pad to 32bits
                    if (offset % 4 != 0)
                    {
                        byte[] padding = new byte[4 - (offset % 4)];
                        padding.CopyTo(output[offset..(offset + padding.Length)]);
                        offset += padding.Length;
                    }
                }
            }

            // End marker
            if (addEndOfOptions)
            {
                BitConverter.GetBytes(opt_endofopt).CopyTo(output[offset..(offset + 2)]); // Type
                offset += 2;
                BitConverter.GetBytes((ushort)0x0000).CopyTo(output[offset..(offset + 2)]); // Length
            }
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[CalculateSize()];
            WriteTo(data);
            return data;

        }

        public static BlockOptions Parse(Memory<byte> data) => Parse(data.ToArray());
        public static BlockOptions Parse(byte[] data)
        {
            int offset = 0;
            BlockOptions opts = new BlockOptions();
            while (offset < data.Length)
            {
                ushort opType = (ushort)(data[offset++] | (data[offset++] << 8));
                ushort opLength = (ushort)(data[offset++] | (data[offset++] << 8));
                byte[] opData = new byte[opLength];
                Array.Copy(data, offset, opData, 0, opLength);
                offset += opData.Length;
                // Padding to 32bit
                if (offset % 4 != 0)
                    offset += 4 - (offset % 4);

                opts.Add(opType, opData);
                if (opType == 0x0000)
                {
                    // End
                    break;
                }
            }
            return opts;
        }
    }
}
