namespace ACE.Server.Network.Packets;
using InboundPacket = ClientPacket;

public class InboundConnectRequest
{
    public double serverTime { get; }
    public ulong cookie { get; }
    public ushort clientId { get; }
    public byte[] issacServerSeed { get; }
    public byte[] issacClientSeed { get; }

    public InboundConnectRequest(InboundPacket packet)
    {
        serverTime = packet.DataReader.ReadDouble();
        cookie = packet.DataReader.ReadUInt64();
        clientId = (ushort)packet.DataReader.ReadUInt32();
        issacServerSeed = packet.DataReader.ReadBytes(4);
        issacClientSeed = packet.DataReader.ReadBytes(4);
    }
}
