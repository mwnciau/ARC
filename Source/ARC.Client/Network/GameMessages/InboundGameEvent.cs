using ACE.Server.Network.GameEvent;
using ARC.Client.Network.GameMessages.Inbound;

namespace ARC.Client.Network.GameMessages;

public abstract class InboundGameEvent
{
    public static GameEventType EventType;

    public abstract void Handle(GameEvent gameEvent, Session session);
}
