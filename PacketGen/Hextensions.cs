using System;
using System.Collections.Generic;
using System.Linq;

namespace PacketGen
{
    public static class Hextensions
    {
        public static string ToHex(this byte[] data) => BitConverter.ToString(data).Replace("-", String.Empty);
        public static string ToHex(this IEnumerable<byte> data) => ToHex(data.ToArray());

        public static byte[] DecodeHex(this string hex)
        {
            hex = hex.Replace(" ", string.Empty);
            hex = hex.Replace("\t", string.Empty);
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}
