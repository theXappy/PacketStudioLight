using PacketDotNet;
using PacketDotNet.Utils;
using System;
using System.Collections.Generic;
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
            // // }
            // UDP_SOURCE_PORT = 1337
            // UDP_DEST_PORT = 7331

            byte[] UDP_PAYLOAD = new byte[0];
            if (variables.TryGetValue(nameof(UDP_PAYLOAD), out string payloadHex))
                UDP_PAYLOAD = Hextensions.GetBytesFromHex(payloadHex);

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
                IP_PAYLOAD = Hextensions.GetBytesFromHex(payloadHex);

            IPAddress IP_SOURCE_ADDR = IPAddress.Parse("127.0.0.1");
            if (variables.TryGetValue(nameof(IP_SOURCE_ADDR), out string srcIp))
                IPAddress.TryParse(srcIp, out IP_SOURCE_ADDR);

            IPAddress IP_DEST_ADDR = IPAddress.Parse("127.0.0.2");
            if (variables.TryGetValue(nameof(IP_DEST_ADDR), out string dstIp))
                IPAddress.TryParse(dstIp, out IP_DEST_ADDR);

            ProtocolType IP_NEXT_TYPE = 0;
            if (variables.TryGetValue(nameof(IP_NEXT_TYPE), out string ethType))
                Enum.TryParse(ethType, out IP_NEXT_TYPE);


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
                ETH_PAYLOAD = Hextensions.GetBytesFromHex(payloadHex);

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
                RAW_PAYLOAD = Hextensions.GetBytesFromHex(payloadHex);


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
                if (!Enum.TryParse(ENCAPSULATION_TYPE, true, out linkLayer))
                    throw new Exception($"Can't parse link layer '{ENCAPSULATION_TYPE}'");
            }

            RawPacket raw = new RawPacket(RAW_PAYLOAD);

            return (raw, linkLayer);
        }

    }

    public class RawPacket : Packet
    {
        public RawPacket(byte[] data)
        {
            this.Header = new ByteArraySegment(Array.Empty<byte>());
            this.PayloadData = data;
        }
    }
}
