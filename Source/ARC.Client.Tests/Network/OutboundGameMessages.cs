using ACE.Common.Extensions;
using ARC.Client.Network.GameMessages.Outbound;
using InboundMessage = ACE.Server.Network.ClientMessage;
using OutboundGameMessage = ACE.Server.Network.GameMessages.GameMessage;

namespace ARC.Client.Tests.Network;

[TestClass]
public class OutboundGameMessages : TestCase
{
    private InboundMessage convertToInboundMessage(OutboundGameMessage outboundPacket)
    {
        InboundMessage inboundMessage = new(outboundPacket.Data.GetBuffer());

        return inboundMessage;
    }

    [TestMethod]
    public void CharacterEnterWorld()
    {
        uint characterId = 12345;
        string account = "my_account";

        InboundMessage message = convertToInboundMessage(new CharacterEnterWorld(
            characterId,
            account
        ));

        #region Code copied from message handler
        /// <see cref="ACE.Server.Network.Handlers.CharacterHandler.CharacterEnterWorld"/>
        var guid = message.Payload.ReadUInt32();
        string clientString = message.Payload.ReadString16L();
        #endregion

        Assert.AreEqual(characterId, guid);
        Assert.AreEqual(clientString, account);
    }
}
