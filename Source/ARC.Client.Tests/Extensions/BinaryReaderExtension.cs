using ACE.Server.Network;
using ARC.Client.Extensions;
using System.IO;

namespace ARC.Client.Tests.Extensions;


[TestClass]
public class BinaryReaderExtension : TestCase
{
    [TestMethod]
    public void ReadPackedDword()
    {
        var data = new MemoryStream();
        var writer = new BinaryWriter(data);

        writer.WritePackedDword(1234u);
        writer.WritePackedDword(123456u);

        data.Position = 0;
        var reader = new BinaryReader(data);

        uint ushortDword = reader.ReadPackedDword();
        uint uintDword = reader.ReadPackedDword();

        Assert.AreEqual(ushortDword, 1234u);
        Assert.AreEqual(uintDword, 123456u);
    }

    [TestMethod]
    public void ReadPackedDwordOfKnownType()
    {
        var data = new MemoryStream();
        var writer = new BinaryWriter(data);

        writer.WritePackedDwordOfKnownType(0x4001234u, 0x4000000u);
        writer.WritePackedDwordOfKnownType(0x5012345u, 0x5000000u);

        var reader = new BinaryReader(data);

        data.Position = 0;
        uint firstDwordRaw = reader.ReadPackedDword();
        uint secondDwordRaw = reader.ReadPackedDword();

        data.Position = 0;
        uint firstDword = reader.ReadPackedDwordOfKnownType(0x4000000u);
        uint secondDword = reader.ReadPackedDwordOfKnownType(0x5000000u);

        Assert.AreEqual(0x1234u, firstDwordRaw);
        Assert.AreEqual(0x12345u, secondDwordRaw);
        Assert.AreEqual(0x4001234u, firstDword);
        Assert.AreEqual(0x5012345u, secondDword);
    }
}
