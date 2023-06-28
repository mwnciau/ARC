using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ARC.Client.Network.GameMessages.GameEvents;
using ARC.Client.Network.GameMessages.Inbound;
using ARC.Client.Tests.Support;
using Moq;
using System.Collections.Generic;
using System.Net;
using InboundMessage = ACE.Server.Network.ClientMessage;
using OutboundGameMessage = ACE.Server.Network.GameMessages.GameMessage;
using ServerSession = ACE.Server.Network.Session;

namespace ARC.Client.Tests.Network;

[TestClass]
public class InboundGameEvents : TestCase
{
    private InboundMessage convertToInboundMessage(OutboundGameMessage outboundPacket)
    {
        InboundMessage inboundMessage = new(outboundPacket.Data.GetBuffer());

        return inboundMessage;
    }

    [TestMethod]
    public void PlayerDescription()
    {
        var player = ClassFactory.Player();
        Mock<ServerSession> serverSessionMock = ClassFactory.MockServerSession();
        serverSessionMock.SetupGet(x => x.Player).Returns(player);

        var outboundPacket = new GameEventPlayerDescription(serverSessionMock.Object);
        var gameEvent = new GameEvent();
        gameEvent.Handle(convertToInboundMessage(outboundPacket), new Session());

        var playerDescriptionMessage = new PlayerDescription();
        playerDescriptionMessage.Handle(gameEvent, new Session());
    }

    public void CharacterTitle()
    {
    }

    public void FriendsListUpdate()
    {
    }

    public void ViewContents()
    {
    }

    public void WeenieError()
    {
    }

    public void WeenieErrorWithString()
    {
    }

    public void SetTurbineChatChannels()
    {
    }
}
