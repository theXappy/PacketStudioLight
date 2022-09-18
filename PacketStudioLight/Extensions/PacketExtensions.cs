using PacketDotNet;
using System;

namespace PacketStudioLight.Extensions
{
    public static class PacketExtensions
    {
        public static LinkLayers GetLayerType(this Packet pkt)
        {
            var typeName = pkt.GetType().Name;
            var layerName = typeName.EndsWith("Packet") ? typeName.Substring(0, typeName.Length - "Packet".Length) : typeName;

            if(!Enum.TryParse<LinkLayers>(layerName, out LinkLayers layer))
            {
                throw new Exception($"Can't figure out link layer for type '{typeName}'");
            }
            return layer;
        }
    }
}
