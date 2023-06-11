using ARC.Client.Network.GameMessage;
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

    private static Dictionary<GameMessageOpcode, MessageHandlerInfo> messageHandlers;

    public static void Initialize()
    {
        DefineMessageHandlers();
    }

    private static void DefineMessageHandlers()
    {
        messageHandlers = new Dictionary<GameMessageOpcode, MessageHandlerInfo>();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            foreach (var methodInfo in type.GetMethods())
            {
                foreach (var messageHandlerAttribute in methodInfo.GetCustomAttributes<GameMessageAttribute>())
                {
                    var messageHandler = new MessageHandlerInfo()
                    {
                        Handler = (MessageHandler)Delegate.CreateDelegate(typeof(MessageHandler), methodInfo),
                        Attribute = messageHandlerAttribute
                    };

                    messageHandlers[messageHandlerAttribute.Opcode] = messageHandler;
                }
            }
        }
    }

    public static void HandleInboundMessage(InboundMessage message, Session session)
    {
        var opcode = (GameMessageOpcode)message.Opcode;

        if (messageHandlers.TryGetValue(opcode, out var messageHandlerInfo))
        {
            // Todo: add these to a queue? Process multithreaded?
            try
            {
                messageHandlerInfo.Handler.Invoke(message, session);
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
