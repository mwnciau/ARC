namespace ARC.Client.Extensions;

public static class BinaryReaderExtensions
{
    public static uint ReadPackedDword(this BinaryReader reader)
    {
        uint dword = BitConverter.ToUInt16(reader.ReadBytes(2));

        if (dword > 32767) {
            // This was packed as 4 bytes
            uint secondPart = BitConverter.ToUInt16(reader.ReadBytes(2));

            dword = ((dword ^ 0x8000) << 16) | secondPart;
        }

        return dword;
    }

    public static uint ReadPackedDwordOfKnownType(this BinaryReader reader, uint type)
    {
        uint dword = reader.ReadPackedDword();

        return dword + type;
    }
}
