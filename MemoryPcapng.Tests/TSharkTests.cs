﻿namespace MemoryPcapng.Tests
{
    internal class TSharkTests
    {
        [Test]
        public void WriteReadPcapng_FromBytes_ThreePacketsPrinted()
        {
            // Arrange
            byte[] input1 =
            {
                0x0A, 0x0D, 0x0D, 0x0A, 0xBC, 0x00, 0x00, 0x00, 0x4D, 0x3C, 0x2B, 0x1A, 0x01, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02, 0x00, 0x35, 0x00, 0x49, 0x6E, 0x74, 0x65,
                0x6C, 0x28, 0x52, 0x29, 0x20, 0x43, 0x6F, 0x72, 0x65, 0x28, 0x54, 0x4D, 0x29, 0x20, 0x69, 0x37,
                0x2D, 0x36, 0x37, 0x30, 0x30, 0x20, 0x43, 0x50, 0x55, 0x20, 0x40, 0x20, 0x33, 0x2E, 0x34, 0x30,
                0x47, 0x48, 0x7A, 0x20, 0x28, 0x77, 0x69, 0x74, 0x68, 0x20, 0x53, 0x53, 0x45, 0x34, 0x2E, 0x32,
                0x29, 0x00, 0x00, 0x00, 0x03, 0x00, 0x22, 0x00, 0x36, 0x34, 0x2D, 0x62, 0x69, 0x74, 0x20, 0x57,
                0x69, 0x6E, 0x64, 0x6F, 0x77, 0x73, 0x20, 0x28, 0x32, 0x32, 0x48, 0x32, 0x29, 0x2C, 0x20, 0x62,
                0x75, 0x69, 0x6C, 0x64, 0x20, 0x32, 0x33, 0x35, 0x38, 0x30, 0x00, 0x00, 0x04, 0x00, 0x32, 0x00,
                0x44, 0x75, 0x6D, 0x70, 0x63, 0x61, 0x70, 0x20, 0x28, 0x57, 0x69, 0x72, 0x65, 0x73, 0x68, 0x61,
                0x72, 0x6B, 0x29, 0x20, 0x34, 0x2E, 0x30, 0x2E, 0x37, 0x20, 0x28, 0x76, 0x34, 0x2E, 0x30, 0x2E,
                0x37, 0x2D, 0x30, 0x2D, 0x67, 0x30, 0x61, 0x64, 0x31, 0x38, 0x32, 0x33, 0x63, 0x63, 0x30, 0x39,
                0x30, 0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xBC, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                0x60, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x02, 0x00, 0x14, 0x00,
                0x5C, 0x44, 0x65, 0x76, 0x69, 0x63, 0x65, 0x5C, 0x4E, 0x50, 0x46, 0x5F, 0x4C, 0x6F, 0x6F, 0x70,
                0x62, 0x61, 0x63, 0x6B, 0x09, 0x00, 0x01, 0x00, 0x06, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x22, 0x00,
                0x36, 0x34, 0x2D, 0x62, 0x69, 0x74, 0x20, 0x57, 0x69, 0x6E, 0x64, 0x6F, 0x77, 0x73, 0x20, 0x28,
                0x32, 0x32, 0x48, 0x32, 0x29, 0x2C, 0x20, 0x62, 0x75, 0x69, 0x6C, 0x64, 0x20, 0x32, 0x33, 0x35,
                0x38, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00,
                0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x09, 0x06, 0x00, 0x7B, 0x46, 0x09, 0xC3,
                0x38, 0x00, 0x00, 0x00, 0x38, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x45, 0x00, 0x00, 0x34,
                0xE3, 0x25, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00, 0x7F, 0x00, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01,
                0xB1, 0x00, 0xB0, 0xFD, 0x0F, 0xC4, 0x3A, 0x3E, 0xDC, 0xF1, 0x42, 0x24, 0x50, 0x18, 0xF4, 0xDF,
                0x67, 0x9C, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, 0xB6, 0x15, 0x00, 0x00, 0xD5, 0x15, 0x00, 0x00,
                0x58, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x4C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x94, 0x09, 0x06, 0x00, 0x9C, 0x46, 0x09, 0xC3, 0x2C, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00, 0x45, 0x00, 0x00, 0x28, 0xE3, 0x26, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00,
                0x7F, 0x00, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0xB0, 0xFD, 0xB1, 0x00, 0xDC, 0xF1, 0x42, 0x24,
                0x0F, 0xC4, 0x3A, 0x4A, 0x50, 0x10, 0xF5, 0xC3, 0xF0, 0xEB, 0x00, 0x00, 0x4C, 0x00, 0x00, 0x00,
                0x06, 0x00, 0x00, 0x00, 0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x09, 0x06, 0x00,
                0x89, 0x72, 0x0A, 0xC3, 0x38, 0x00, 0x00, 0x00, 0x38, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
                0x45, 0x00, 0x00, 0x34, 0xE3, 0x27, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00, 0x7F, 0x00, 0x00, 0x01,
                0x7F, 0x00, 0x00, 0x01, 0xE4, 0xD6, 0xE4, 0xD4, 0x88, 0x32, 0xB5, 0xD1, 0xD1, 0xFD, 0x27, 0x05,
                0x50, 0x18, 0xB8, 0x8B, 0x81, 0x32, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF, 0x26, 0x29, 0x08, 0x00,
                0x42, 0x24, 0x08, 0x00, 0x58, 0x00, 0x00, 0x00
            };
            int packetsCounter = 0;
            if (!File.Exists(@"C:\Program Files\Wireshark\tshark.exe"))
                throw new InconclusiveException("TShark not found");
            TShark t = new TShark(@"C:\Program Files\Wireshark\tshark.exe", TSharkOutputMode.Fields);
            t.NewPacketLine += (sender, s) => packetsCounter++;

            // Act
            t.Pipe.Write(input1);
            t.Pipe.Flush();
            t.WaitForPackets(3);

            // Assert
            Assert.That(packetsCounter, Is.EqualTo(3));
        }

    }
}
