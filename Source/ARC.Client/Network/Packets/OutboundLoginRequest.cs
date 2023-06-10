using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.Enum;
using Google.Protobuf.WellKnownTypes;
using Org.BouncyCastle.Bcpg;
using System.Net.Sockets;
using OutboundPacket = ACE.Server.Network.ServerPacket;

namespace ARC.Client.Network.Packets;

public class OutboundLoginRequest : OutboundPacket
{
    public OutboundLoginRequest(string accountName, string password)
    {
        Header.Flags = PacketHeaderFlags.LoginRequest;
        InitializeDataWriter();

        // Junk string ignored by the server, always 1802
        DataWriter.WriteString16L("1802");

        // Data left in the packet - ignored by the server
        DataWriter.Write((uint)0);

        DataWriter.Write((uint)NetAuthType.AccountPassword);

        // Auth flags
        DataWriter.Write((uint)AuthFlags.None);

        // Current timestamp
        DataWriter.Write((uint)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());

        DataWriter.WriteString16L(accountName);

        // Account to login as, special admin action only
        DataWriter.WriteString16L("");

        DataWriter.Write(password);


    }
}
