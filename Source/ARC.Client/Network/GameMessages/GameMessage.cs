using ACE.Server.Network.GameMessages;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages;
public abstract class GameMessage
{
    public static GameMessageOpcode Opcode;

    public abstract void Handle(InboundMessage message, Session session);
}
