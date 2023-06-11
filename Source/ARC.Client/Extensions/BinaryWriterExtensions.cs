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

        if (data.Length > 255)
        {
            data = "~~" + data;
        }
        else if (data.Length > 0)
        {
            data = "~" + data;
        }

        writer.Write((uint)data.Length);
        writer.Write(System.Text.Encoding.GetEncoding(1252).GetBytes(data));

        // client expects string length to be a multiple of 4 including the 2 bytes for length
        writer.Pad(CalculatePadMultiple(sizeof(uint) + (uint)data.Length, 4u));
    }
}
