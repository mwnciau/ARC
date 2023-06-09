namespace ACE.Server.Network.Packets;
using InboundPacket = ClientPacket;

public class InboundConnectRequest
{
    public double ServerTime { get; }
    public ulong Cookie { get; }
    public ushort ClientId { get; }
    public byte[] ServerSeed { get; }
    public byte[] ClientSeed { get; }

    public InboundConnectRequest(InboundPacket packet)
    {
        ServerTime = packet.DataReader.ReadDouble();
        Cookie = packet.DataReader.ReadUInt64();
        ClientId = (ushort)packet.DataReader.ReadUInt32();
        ServerSeed = packet.DataReader.ReadBytes(4);
        ClientSeed = packet.DataReader.ReadBytes(4);
    }
}
