namespace MemoryPcapng.Tests;

[TestFixture]
public class InterfaceDescriptionBlockTests
{
    [Test]
    public void InterfaceDescriptionBlock_ParsingExampleBlock_ShouldBeCorrect()
    {
        // Example block: 010000001400000001000000ffff000014000000
        byte[] exampleBlockBytes = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, // Block Type: Interface Description Block (0x00000001)
            0x14, 0x00, 0x00, 0x00, // Block Length: 20
            0x01, 0x00,             // Link Type: ETHERNET (1)
            0x00, 0x00,             // Reserved: 0x0000
            0xFF, 0xFF, 0x00, 0x00, // Snap Length: 65535
            0x14, 0x00, 0x00, 0x00  // Block Length (trailer): 20
        };

        InterfaceDescriptionBlock interfaceDescriptionBlock = new InterfaceDescriptionBlock(exampleBlockBytes);

        // Assertions
        Assert.AreEqual(0x00000001, interfaceDescriptionBlock.BlockType);
        Assert.AreEqual(20, interfaceDescriptionBlock.BlockTotalLength);
        Assert.AreEqual(1, interfaceDescriptionBlock.LinkType);
        Assert.AreEqual(0, interfaceDescriptionBlock.Reserved);
        Assert.AreEqual(65535, interfaceDescriptionBlock.SnapLen);
        Assert.AreEqual(16, interfaceDescriptionBlock.OptionsLength); // Block Length - Fixed Fields Length
    }

    [Test]
    public void InterfaceDescriptionBlock_SetOptions_ShouldModifyMemory()
    {
        // Example block: 010000001400000001000000ffff000014000000
        byte[] exampleBlockBytes = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, // Block Type: Interface Description Block (0x00000001)
            0x14, 0x00, 0x00, 0x00, // Block Length: 20
            0x01, 0x00,             // Link Type: ETHERNET (1)
            0x00, 0x00,             // Reserved: 0x0000
            0xFF, 0xFF, 0x00, 0x00, // Snap Length: 65535
            0x14, 0x00, 0x00, 0x00  // Block Length (trailer): 20
        };

        InterfaceDescriptionBlock interfaceDescriptionBlock = new InterfaceDescriptionBlock(exampleBlockBytes);

        // Set new options
        byte[] newOptions = new byte[] { 0xAA, 0xBB, 0xCC };
        interfaceDescriptionBlock.Options = newOptions;

        // Assertions
        Assert.IsTrue(interfaceDescriptionBlock.BackingMemoryChanged);
        Assert.AreEqual(newOptions.Length, interfaceDescriptionBlock.OptionsLength);
        CollectionAssert.AreEqual(newOptions, interfaceDescriptionBlock.Options.ToArray());
    }
}