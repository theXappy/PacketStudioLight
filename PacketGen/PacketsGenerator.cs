using PacketDotNet;
using PacketDotNet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;

namespace PacketGen
{
    public static class PacketsGenerator
    {
        private static Dictionary<string, MethodInfo> _generators;

        static PacketsGenerator()
        {
            _generators = new Dictionary<string, MethodInfo>();
            foreach (var method in typeof(PacketsGenerator).GetMethods())
            {
                string upperName = method.Name.ToUpper();
                if (upperName.StartsWith("GENERATE") && method.GetParameters().Length == 1)
                {
                    string rest = upperName.Substring("GENERATE".Length);
                    _generators.Add(rest, method);
                }
            }
        }

        public static Dictionary<string, string> GetTemplateHints()
        {
            return new Dictionary<string, string>()
            {
                ["Raw"] =
                    "@ Generate: Raw\n@ RAW_PAYLOAD = {\n aa bb cc dd\n@ }\n@ ENCAPSULATION_TYPE = Ethernet",
                ["Ethernet"] =
                    "@ Generate: Ethernet\n@ ETH_PAYLOAD = {\n aa bb cc dd\n@ }\n@ ETH_SOURCE_ADDR = aa:bb:cc:dd:ee:ff\n@ ETH_DEST_ADDR = aa:bb:cc:dd:ee:00\n@ ETH_NEXT_TYPE = IPv4",
                ["IP"] =
                    "@ Generate: IP\n@ IP_PAYLOAD = {\n aa bb cc dd\n@ }\n@ IP_SOURCE_ADDR = 127.0.0.1\n@ IP_DEST_ADDR = 127.0.0.2\n@ IP_NEXT_TYPE = UDP",
                ["UDP"] =
                    "@ Generate: UDP\n@ UDP_PAYLOAD = {\n aa bb cc dd\n@ }\n@ UDP_SOURCE_PORT = 1337\n@ UDP_DEST_PORT = 7331",
                ["SCTP"] =
                    "@ Generate: SCTP\n@ SCTP_PAYLOAD_1 = {\n aa bb cc dd\n@ }\n\r\n@ SCTP_NEXT_TYPE_1 = 32\n@ SCTP_PAYLOAD_2 = {\n ee ff\n@ }\n@ SCTP_CHECKSUM_ALGO = Crc32c\n@ SCTP_CHECKSUM = 1337"
            };
        }

        public static (Packet, LinkLayers) Generate(string type, Dictionary<string, string> variables)
        {
            string upperType = type.ToUpper();
            if (!_generators.TryGetValue(upperType, out var generator))
                throw new Exception($"Couldn't find generator for '{type}'");

            (Packet res, LinkLayers firstLayer) = (ValueTuple<Packet, LinkLayers>)generator.Invoke(null, new object[] { variables });

            // TODO: Make this an extension method?
            while (res.ParentPacket != null)
                res = res.ParentPacket;
            return (res, firstLayer);
        }

        public static (Packet, LinkLayers) GenerateUdp(Dictionary<string, string> variables)
        {
            // UDP_PAYLOAD = {
            // aa bb cc dd
            // }
            // UDP_SOURCE_PORT = 1337
            // UDP_DEST_PORT = 7331

            byte[] UDP_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(UDP_PAYLOAD), out string payloadHex))
                UDP_PAYLOAD = Hextensions.DecodeHex(payloadHex);

            ushort UDP_SOURCE_PORT = 1;
            if (variables.TryGetValue(nameof(UDP_SOURCE_PORT), out string srcPort))
                ushort.TryParse(srcPort, out UDP_SOURCE_PORT);

            ushort UDP_DEST_PORT = 1;
            if (variables.TryGetValue(nameof(UDP_DEST_PORT), out string dstPort))
                ushort.TryParse(dstPort, out UDP_DEST_PORT);

            UdpPacket udp = new UdpPacket(UDP_SOURCE_PORT, UDP_DEST_PORT);
            udp.PayloadData = UDP_PAYLOAD;

            (Packet ip, LinkLayers firstLayer) = GenerateIp(variables);
            ip.PayloadPacket = udp;

            return (ip, firstLayer);
        }

        public static (Packet, LinkLayers) GenerateSctp(Dictionary<string, string> variables) =>
            SctpPacketGenerator.GenerateSctp(variables);

        public static (Packet, LinkLayers) GenerateIpv4(Dictionary<string, string> variables) => GenerateIp(variables);
        public static (Packet, LinkLayers) GenerateIp(Dictionary<string, string> variables)
        {
            // IP_PAYLOAD = {
            // aa bb cc dd
            // // }
            // IP_SOURCE_ADDR = 127.0.0.1
            // IP_DEST_ADDR = 127.0.0.2
            // IP_NEXT_TYPE = UDP

            byte[] IP_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(IP_PAYLOAD), out string payloadHex))
                IP_PAYLOAD = Hextensions.DecodeHex(payloadHex);

            IPAddress IP_SOURCE_ADDR = IPAddress.Parse("127.0.0.1");
            if (variables.TryGetValue(nameof(IP_SOURCE_ADDR), out string srcIp))
                IPAddress.TryParse(srcIp, out IP_SOURCE_ADDR);

            IPAddress IP_DEST_ADDR = IPAddress.Parse("127.0.0.2");
            if (variables.TryGetValue(nameof(IP_DEST_ADDR), out string dstIp))
                IPAddress.TryParse(dstIp, out IP_DEST_ADDR);

            ProtocolType IP_NEXT_TYPE = 0;
            if (variables.TryGetValue(nameof(IP_NEXT_TYPE), out string ethType))
                Enum.TryParse(ethType, true, out IP_NEXT_TYPE);
            if ("SCTP".Equals(ethType, StringComparison.CurrentCultureIgnoreCase))
                IP_NEXT_TYPE = (ProtocolType)132;


            IPv4Packet ip = new IPv4Packet(IP_SOURCE_ADDR, IP_DEST_ADDR);
            ip.PayloadData = IP_PAYLOAD;
            ip.Protocol = IP_NEXT_TYPE;

            (Packet eth, LinkLayers firstLayer) = GenerateEthernet(variables);
            eth.PayloadPacket = ip;

            return (ip, firstLayer);
        }

        public static (Packet, LinkLayers) GenerateEthernet(Dictionary<string, string> variables)
        {
            // ETH_PAYLOAD = {
            // aa bb cc dd
            // // }
            // ETH_SOURCE_ADDR = aa:bb:cc:dd:ee:ff
            // ETH_DEST_ADDR = aa:bb:cc:dd:ee:00
            // ETH_NEXT_TYPE = IPv4

            byte[] ETH_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(ETH_PAYLOAD), out string payloadHex))
                ETH_PAYLOAD = Hextensions.DecodeHex(payloadHex);

            PhysicalAddress ETH_SOURCE_ADDR = PhysicalAddress.Parse("00:00:00:00:00:00");
            if (variables.TryGetValue(nameof(ETH_SOURCE_ADDR), out string srcIp))
                ETH_SOURCE_ADDR = PhysicalAddress.Parse(srcIp);

            PhysicalAddress ETH_DEST_ADDR = PhysicalAddress.Parse("00:00:00:00:00:00");
            if (variables.TryGetValue(nameof(ETH_DEST_ADDR), out string dstIp))
                ETH_DEST_ADDR = PhysicalAddress.Parse(dstIp);

            EthernetType ETH_NEXT_TYPE = EthernetType.None;
            if (variables.TryGetValue(nameof(ETH_NEXT_TYPE), out string ethType))
                Enum.TryParse(ethType, out ETH_NEXT_TYPE);

            EthernetPacket eth = new EthernetPacket(ETH_SOURCE_ADDR, ETH_DEST_ADDR, ETH_NEXT_TYPE);
            eth.PayloadData = ETH_PAYLOAD;

            return (eth, LinkLayers.Ethernet);
        }

        public static (Packet, LinkLayers) GenerateRaw(Dictionary<string, string> variables)
        {
            // RAW_PAYLOAD = {
            // aa bb cc dd
            // // }
            // ENCAPSULATION_TYPE = Ethernet

            byte[] RAW_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(RAW_PAYLOAD), out string payloadHex))
                RAW_PAYLOAD = Hextensions.DecodeHex(payloadHex);


            LinkLayers linkLayer;
            string ENCAPSULATION_TYPE;
            if (!variables.TryGetValue(nameof(ENCAPSULATION_TYPE), out ENCAPSULATION_TYPE))
            {
                throw new Exception($"Link Type variable ({nameof(ENCAPSULATION_TYPE)}) must be provided.");
            }

            if (int.TryParse(ENCAPSULATION_TYPE, out int encTypeNum))
            {
                linkLayer = (LinkLayers)encTypeNum;
            }
            else
            {
                if (!Enum.TryParse<LinkLayers>(ENCAPSULATION_TYPE, true, out linkLayer))
                    throw new Exception($"Can't parse link layer '{ENCAPSULATION_TYPE}'");
            }

            GenericPacket generic = new GenericPacket(RAW_PAYLOAD);

            return (generic, linkLayer);
        }

    }

    public class GenericPacket : Packet
    {
        public GenericPacket(byte[] data)
        {
            this.Header = new ByteArraySegment(Array.Empty<byte>());
            this.PayloadData = data;
        }
    }
}
