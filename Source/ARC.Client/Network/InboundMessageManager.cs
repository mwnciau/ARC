using ARC.Client.Network.GameMessages;
using GameMessageOpcode = ACE.Server.Network.GameMessages.GameMessageOpcode;
using InboundMessage = ACE.Server.Network.ClientMessage;
using log4net;
using System.Reflection;

namespace ARC.Client.Network;

public static class InboundMessageManager
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private class MessageHandlerInfo
    {
        public MessageHandler Handler { get; set; }
        public GameMessageAttribute Attribute { get; set; }
    }

    public delegate void MessageHandler(InboundMessage message, Session session);

    private static Dictionary<GameMessageOpcode, Type> messageHandlers = new();

    public static void Initialize()
    {
        DefineMessageHandlers();
    }

    private static void DefineMessageHandlers()
    {
        var GameMessageClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.BaseType == typeof(GameMessage));

        foreach (var GameMessageClass in GameMessageClasses)
        {

            var field = GameMessageClass.GetField("Opcode");
            GameMessageOpcode opcode = (GameMessageOpcode)field.GetValue(null);

            messageHandlers[opcode] = GameMessageClass;
        }
    }

    public static void HandleInboundMessage(InboundMessage message, Session session)
    {
        var opcode = (GameMessageOpcode)message.Opcode;

        if (messageHandlers.TryGetValue(opcode, out var GameMessageClass))
        {
            // Todo: add these to a queue? Process multithreaded?
            try
            {
                GameMessage GameMessage = (GameMessage)Activator.CreateInstance(GameMessageClass);
                GameMessage.Handle(message, session);
                session.OnGameMessage(opcode, GameMessage);
            }
            catch (Exception ex)
            {
                log.Error($"Received GameMessage packet that threw an exception: opcode: 0x{(int)opcode:X4}:{opcode}");
                log.Error(ex);
            }
        }
        else
        {
            log.Warn($"Received unhandled fragment opcode: 0x{(int)opcode:X4} - {opcode}");
        }
    }
}
