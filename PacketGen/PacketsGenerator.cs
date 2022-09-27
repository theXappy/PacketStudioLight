using PacketDotNet;
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
            foreach(var method in typeof(PacketsGenerator).GetMethods())
            {
                string upperName = method.Name.ToUpper();
                if(upperName.StartsWith("GENERATE") && method.GetParameters().Length == 1)
                {
                    string rest = upperName.Substring("GENERATE".Length);
                    _generators.Add(rest, method);
                }
            }
        }

        public static Packet Generate(string type, Dictionary<string, string> variables)
        {
            string upperType = type.ToUpper();
            if (!_generators.TryGetValue(upperType, out var generator))
                throw new Exception($"Couldn't find generator for '{type}'");

            Packet res = generator.Invoke(null, new object[1] { variables }) as Packet;

            // TODO: Make this an extension method?
            while (res.ParentPacket != null)
                res = res.ParentPacket;
            return res;
        }

        public static UdpPacket GenerateUdp(Dictionary<string,string> variables)
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
            if(variables.TryGetValue(nameof(UDP_SOURCE_PORT), out string srcPort))
                ushort.TryParse(srcPort, out UDP_SOURCE_PORT);

            ushort UDP_DEST_PORT = 1;
            if(variables.TryGetValue(nameof(UDP_DEST_PORT), out string dstPort))
                ushort.TryParse(dstPort, out UDP_DEST_PORT);

            UdpPacket udp = new UdpPacket(UDP_SOURCE_PORT, UDP_DEST_PORT);
            udp.PayloadData = UDP_PAYLOAD;

            IPv4Packet ip = GenerateIp(variables);
            ip.PayloadPacket = udp;

            return udp;
        }

        public static IPv4Packet GenerateIpv4(Dictionary<string, string> variables) => GenerateIp(variables);
        public static IPv4Packet GenerateIp(Dictionary<string, string> variables)
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

            EthernetPacket eth = GenerateEthernet(variables);
            eth.PayloadPacket = ip;

            return ip;
        }

        public static EthernetPacket GenerateEthernet(Dictionary<string, string> variables)
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

            return eth;
        }

    }
}
