using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;
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
public class InboundGameMessages : TestCase
{
    private InboundMessage convertToInboundMessage(OutboundGameMessage outboundPacket)
    {
        InboundMessage inboundMessage = new(outboundPacket.Data.GetBuffer());

        return inboundMessage;
    }

    [TestMethod]
    public void CharacterList()
    {
        Mock<ServerSession> serverSessionMock = ClassFactory.MockServerSession();

        MockShardConfig shardConfig = new MockShardConfig();
        shardConfig
            .MockLong("max_chars_per_account", 10)
            .MockBool("use_turbine_chat", true);

        Character characterBob = new();
        characterBob.Name = "Bob";
        Character characterAlice = new();
        characterAlice.Name = "Alice";

        InboundMessage inboundMessage = convertToInboundMessage(new GameMessageCharacterList(
            new List<Character> { characterBob, characterAlice },
            serverSessionMock.Object
        ));

        CharacterList characterList = new();
        characterList.Handle(inboundMessage, new Session());

        Assert.AreEqual("Bob", characterList.Characters[0].Name);
        Assert.AreEqual("Alice", characterList.Characters[1].Name);
        Assert.AreEqual(10u, characterList.CharacterSlots);
        Assert.AreEqual(true, characterList.GlobalChatChannelsEnabled);
    }

    [TestMethod]
    public void ServerName()
    {
        string name = "My Server";
        int currentConnections = 21;
        int maxConnections = 60;

        InboundMessage inboundMessage = convertToInboundMessage(new GameMessageServerName(
            name,
            currentConnections,
            maxConnections
        ));

        ServerName serverNameMessage = new();
        serverNameMessage.Handle(inboundMessage, new Session());

        Assert.AreEqual(name, serverNameMessage.Name);
        Assert.AreEqual(currentConnections, serverNameMessage.CurrentConnections);
        Assert.AreEqual(maxConnections, serverNameMessage.MaxConnections);
    }

    public void GameEvent()
    { }

    public void ServerMessage()
    { }

    [TestMethod]
    public void PrivateUpdatePropertyInt()
    {
        PropertyInt property = PropertyInt.AccountRequirements;
        int value = 10;

        InboundMessage inboundMessage = convertToInboundMessage(new GameMessagePrivateUpdatePropertyInt(
            ClassFactory.WorldObject(),
            property,
            value
        ));

        PrivateUpdatePropertyInt updatePropertyIntMessage = new();
        updatePropertyIntMessage.Handle(inboundMessage, new Session());

        Assert.AreEqual(property, updatePropertyIntMessage.Property);
        Assert.AreEqual(value, updatePropertyIntMessage.Value);
    }

    public void PlayerCreate()
    { }

    [TestMethod]
    public void ObjectCreate()
    {
        InboundMessage inboundMessage = convertToInboundMessage(
            new GameMessageCreateObject(ClassFactory.WorldObject())
        );
        ObjectCreate objectCreate = new();
        objectCreate.Handle(inboundMessage, new Session());

        Assert.AreEqual(null, objectCreate.Object);
    }

    public void PlayEffect()
    { }
}
