using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;

namespace PacketGen
{

    public class Generator
    {
        Dictionary<string, MethodInfo> _generators;

        public Generator()
        {
            _generators = new Dictionary<string, MethodInfo>();
            foreach(var method in typeof(Generator).GetMethods())
            {
                string upperName = method.Name.ToUpper();
                if(upperName.StartsWith("GENERATE") && method.GetParameters().Length == 1)
                {
                    string rest = upperName.Substring("GENERATE".Length);
                    _generators.Add(rest, method);
                }
            }
        }

        public Packet Generate(string type, Dictionary<string, string> variables)
        {
            string upperType = type.ToUpper();
            if (!_generators.TryGetValue(upperType, out var generator))
                throw new Exception($"Couldn't find generator for '{type}'");

            Packet? res = generator.Invoke(this, new object[1] { variables }) as Packet;

            // TODO: Make this an extension method?
            while (res.ParentPacket != null)
                res = res.ParentPacket;
            return res;
        }

        public UdpPacket GenerateUdp(Dictionary<string,string> variables)
        {
            // UDP_PAYLOAD = {
            // aa bb cc dd
            // // }
            // UDP_SOURCE_PORT = 1337
            // UDP_DEST_PORT = 7331

            byte[] UDP_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(UDP_PAYLOAD), out string? payloadHex))
                UDP_PAYLOAD = GetBytesFromHex(payloadHex);

            ushort UDP_SOURCE_PORT = 1;
            if(variables.TryGetValue(nameof(UDP_SOURCE_PORT), out string? srcPort))
                ushort.TryParse(srcPort, out UDP_SOURCE_PORT);

            ushort UDP_DEST_PORT = 1;
            if(variables.TryGetValue(nameof(UDP_DEST_PORT), out string? dstPort))
                ushort.TryParse(dstPort, out UDP_DEST_PORT);

            UdpPacket udp = new UdpPacket(UDP_SOURCE_PORT, UDP_DEST_PORT);
            udp.PayloadData = UDP_PAYLOAD;

            IPv4Packet ip = GenerateIp(variables);
            ip.PayloadPacket = udp;

            return udp;
        }

        public IPv4Packet GenerateIp(Dictionary<string, string> variables)
        {
            // IP_PAYLOAD = {
            // aa bb cc dd
            // // }
            // IP_SOURCE_ADDR = 127.0.0.1
            // IP_DEST_ADDR = 127.0.0.2

            byte[] IP_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(IP_PAYLOAD), out string? payloadHex))
                IP_PAYLOAD = GetBytesFromHex(payloadHex);

            IPAddress? IP_SOURCE_ADDR = IPAddress.Parse("127.0.0.1");
            if (variables.TryGetValue(nameof(IP_SOURCE_ADDR), out string? srcIp))
                IPAddress.TryParse(srcIp, out IP_SOURCE_ADDR);

            IPAddress? IP_DEST_ADDR = IPAddress.Parse("127.0.0.2");
            if (variables.TryGetValue(nameof(IP_DEST_ADDR), out string? dstIp))
                IPAddress.TryParse(dstIp, out IP_DEST_ADDR);

            IPv4Packet ip = new IPv4Packet(IP_SOURCE_ADDR, IP_DEST_ADDR);
            ip.PayloadData = IP_PAYLOAD;

            EthernetPacket eth = GenerateEthernet(variables);
            eth.PayloadPacket = ip;

            return ip;
        }

        public EthernetPacket GenerateEthernet(Dictionary<string, string> variables)
        {
            // ETH_PAYLOAD = {
            // aa bb cc dd
            // // }
            // ETH_SOURCE_ADDR = aa:bb:cc:dd:ee:ff
            // ETH_DEST_ADDR = aa:bb:cc:dd:ee:00
            // ETH_TYPE = IPv4

            byte[] ETH_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(ETH_PAYLOAD), out string? payloadHex))
                ETH_PAYLOAD = GetBytesFromHex(payloadHex);

            PhysicalAddress? ETH_SOURCE_ADDR = PhysicalAddress.Parse("00:00:00:00:00:00");
            if (variables.TryGetValue(nameof(ETH_SOURCE_ADDR), out string? srcIp))
                PhysicalAddress.TryParse(srcIp, out ETH_SOURCE_ADDR);

            PhysicalAddress? ETH_DEST_ADDR = PhysicalAddress.Parse("00:00:00:00:00:00");
            if (variables.TryGetValue(nameof(ETH_DEST_ADDR), out string? dstIp))
                PhysicalAddress.TryParse(dstIp, out ETH_DEST_ADDR);

            EthernetType ETH_TYPE = EthernetType.None;
            if (variables.TryGetValue(nameof(ETH_TYPE), out string? ethType))
                Enum.TryParse(ethType, out ETH_TYPE);

            EthernetPacket eth = new EthernetPacket(ETH_SOURCE_ADDR, ETH_DEST_ADDR, ETH_TYPE);
            eth.PayloadData = ETH_PAYLOAD;

            return eth;
        }

        private static byte[] GetBytesFromHex(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}
