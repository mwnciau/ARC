using ACE.Server.Network;
using ACE.Server.Network.Enum;
using ACE.Server.Network.Packets;
using ARC.Client.Network.Packets;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using InboundPacket = ACE.Server.Network.ClientPacket;
using OutboundPacket = ACE.Server.Network.ServerPacket;

namespace ARC.Client.Tests.Network;

[TestClass]
public class Packets : TestCase
{
    private InboundPacket convertToInboundPacket(OutboundPacket outboundPacket)
    {
        InboundPacket inboundPacket = new();
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)(
            PacketHeader.HeaderSize
            + (outboundPacket.Data?.Length ?? 0)
            + outboundPacket.Fragments.Count * PacketFragment.MaxFragementSize
        ));
        outboundPacket.CreateReadyToSendPacket(buffer, out int size);

        inboundPacket.Unpack(buffer, size);

        return inboundPacket;
    }

    [TestMethod]
    public void InboundConnectRequest()
    {
        double serverTime = 12345;
        ulong cookie = 56789;
        uint clientId = 1234;
        byte[] serverSeed = { 0x20, 0x21, 0x22, 0x23 };
        byte[] clientSeed = { 0x30, 0x31, 0x32, 0x33 };

        InboundPacket inboundPacket = convertToInboundPacket(
            new PacketOutboundConnectRequest(serverTime, cookie, clientId, serverSeed, clientSeed)
        );
        InboundConnectRequest processedPacket = new(inboundPacket);

        Assert.AreEqual(serverTime, processedPacket.ServerTime);
        Assert.AreEqual(cookie, processedPacket.Cookie);
        Assert.AreEqual(clientId, processedPacket.ClientId);
        Assert.IsTrue(serverSeed.SequenceEqual(processedPacket.ServerSeed));
        Assert.IsTrue(clientSeed.SequenceEqual(processedPacket.ClientSeed));
    }

    [TestMethod]
    public void OutboundConnectResponse()
    {
        ulong cookie = 56789;

        InboundPacket inboundPacket = convertToInboundPacket(
            new OutboundConnectResponse(cookie)
        );
        PacketInboundConnectResponse processedPacket = new(inboundPacket);

        Assert.AreEqual(cookie, processedPacket.Check);
    }

    [TestMethod]
    public void OutboundLoginRequest()
    {
        string account = "username";
        string password = "password";

        InboundPacket inboundPacket = convertToInboundPacket(
            new OutboundLoginRequest(account, password)
        );
        PacketInboundLoginRequest processedPacket = new(inboundPacket);

        Assert.AreEqual(account, processedPacket.Account);
        Assert.AreEqual(password, processedPacket.Password);
        Assert.AreEqual(NetAuthType.AccountPassword, processedPacket.NetAuthType);
    }

    [TestMethod]
    public void OutboundRequestRetransmit()
    {
        List<uint> sequenceIds = new() { 2, 3, 5, 8, 13, 21 };

        InboundPacket inboundPacket = convertToInboundPacket(
            new OutboundRequestRetransmit(sequenceIds)
        );

        Assert.IsTrue(sequenceIds.SequenceEqual(inboundPacket.HeaderOptional.RetransmitData));
    }
}
