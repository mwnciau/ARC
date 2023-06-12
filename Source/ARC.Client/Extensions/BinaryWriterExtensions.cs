using ACE.Server.Network;

namespace ARC.Client.Extensions;

public static class BinaryWriterExtensions
{
    /// <see cref="ACE.Server.Network.Extensions.CalculatePadMultiple"/>
    private static uint CalculatePadMultiple(uint length, uint multiple) { return multiple * ((length + multiple - 1u) / multiple) - length; }

    /// <see cref="ACE.Common.Extensions.BinaryReaderExtensions.ReadString32L"/>
    public static void WriteString32L(this BinaryWriter writer, string data)
    {
        if (data == null) data = "";

        uint length = (uint)data.Length + 1;
        int skip = 1;

        if (data.Length > 255) {
            skip++;
            length++;
        }

        writer.Write(length);
        writer.Skip(skip);
        writer.Write(System.Text.Encoding.GetEncoding(1252).GetBytes(data));

        // client expects string length to be a multiple of 4 including the 2 bytes for length
        writer.Pad(CalculatePadMultiple(sizeof(uint) + (uint)data.Length, 4u));
    }

    public static void Skip(this BinaryWriter writer, int skip)
    {
        writer.BaseStream.Position += skip;
    }
}
