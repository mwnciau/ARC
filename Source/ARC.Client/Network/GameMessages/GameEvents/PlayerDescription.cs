using ACE.Server.Network.GameEvent;
using ARC.Client.Network.GameMessages.Inbound;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARC.Client.Network.GameMessages.GameEvents;
public class PlayerDescription : InboundGameEvent
{
    public static GameEventType GameEventType = GameEventType.PlayerDescription;

    /// <see cref="ACE.Server.Network.GameEvent.Events.GameEventPlayerDescription"/>
    public override void Handle(GameEvent gameEvent, Session session)
    {

    }
}
