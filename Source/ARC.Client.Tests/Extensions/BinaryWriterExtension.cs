using ACE.Server.Network;
using ARC.Client.Extensions;
using ACE.Common.Extensions;
using System.IO;

namespace ARC.Client.Tests.Extensions;


[TestClass]
public class BinaryWriterExtension : TestCase
{
    [TestMethod]
    public void WriteString32L()
    {
        var data = new MemoryStream();
        var writer = new BinaryWriter(data);

        string shortString = "abcdefg";
        string longString = new string('a', 500);

        writer.WriteString32L(shortString);
        writer.WriteString32L(longString);

        data.Position = 0;
        var reader = new BinaryReader(data);

        var shortStringOut = reader.ReadString32L();
        var longStringOut = reader.ReadString32L();

        Assert.AreEqual(shortString, shortStringOut);
        Assert.AreEqual(longString, longStringOut);
    }

    [TestMethod]
    public void Skip()
    {
        var data = new MemoryStream();
        var writer = new BinaryWriter(data);

        writer.Write(123);
        writer.Skip(6);
        writer.Write(456);

        data.Position = 0;
        var reader = new BinaryReader(data);

        var firstNumber = reader.ReadInt32();
        reader.Skip(6);
        var secondNumber = reader.ReadInt32();

        Assert.AreEqual(123, firstNumber);
        Assert.AreEqual(456, secondNumber);
    }
}
