using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;

namespace ARC.Client.Network.GameMessage;

public static class GameMessagePacket
{
    [GameMessage(GameMessageOpcode.GameAction, SessionState.WorldConnected)]
    public static void HandleGameAction(ClientMessage message, Session session)
    {
        // TODO: verify sequence
        uint sequence = message.Payload.ReadUInt32();
        uint opcode   = message.Payload.ReadUInt32();

        InboundMessageManager.HandleGameAction((GameMessageType)opcode, message, session);
    }
}
