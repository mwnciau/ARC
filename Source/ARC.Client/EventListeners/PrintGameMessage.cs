using ACE.Server.Network.GameMessages;
using log4net;
using System.Reflection;
using InboundGameMessage = ARC.Client.Network.GameMessages.InboundGameMessage;

namespace ARC.Client.EventListeners;
public class PrintGameMessage
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public static void Initialize(Session session)
    {
        session.GameMessageEventListeners += OnGameMessage;
    }
    public static void OnGameMessage(GameMessageOpcode opcode, InboundGameMessage message)
    {
        log.Info(message.ToString());
    }
}
