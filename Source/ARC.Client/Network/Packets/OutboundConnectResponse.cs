using ACE.Server.Network;
using OutboundPacket = ACE.Server.Network.ServerPacket;

namespace ARC.Client.Network.Packets;

public class OutboundConnectResponse : OutboundPacket
{
    public OutboundConnectResponse(UInt64 connectionCookie)
    {
        Header.Flags = PacketHeaderFlags.ConnectResponse;

        InitializeDataWriter();

        DataWriter.Write(connectionCookie); // CConnectHeader.Cookie
    }
}
