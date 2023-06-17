using ARC.Client.Network.GameMessages;
using GameMessageOpcode = ACE.Server.Network.GameMessages.GameMessageOpcode;
using InboundMessage = ACE.Server.Network.ClientMessage;
using log4net;
using System.Reflection;
using ACE.Server.Network.GameEvent;
using ARC.Client.Network.GameMessages.Inbound;

namespace ARC.Client.Network;

public static class InboundMessageManager
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public delegate void MessageHandler(InboundMessage message, Session session);

    private static Dictionary<GameMessageOpcode, Type> messageHandlers = new();
    private static Dictionary<GameEventType, Type> eventHandlers = new();

    public static void Initialize()
    {
        DefineMessageHandlers();
    }

    private static void DefineMessageHandlers()
    {
        var GameMessageClasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.BaseType == typeof(InboundGameMessage));

        foreach (var GameMessageClass in GameMessageClasses) {

            var field = GameMessageClass.GetField("Opcode");
            GameMessageOpcode opcode = (GameMessageOpcode)field.GetValue(null);

            messageHandlers[opcode] = GameMessageClass;
        }
    }

    private static void DefineEventHandlers()
    {
        var GameEventlasses = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.BaseType == typeof(InboundGameEvent));

        foreach (var GameEventClass in GameEventlasses) {

            var eventType = (GameEventType)GameEventClass.GetField("EventType").GetValue(null);

            eventHandlers[eventType] = GameEventClass;
        }
    }

    public static void HandleInboundMessage(InboundMessage message, Session session)
    {
        var opcode = (GameMessageOpcode)message.Opcode;

        Type gameMessageClass;

        // Game events are special cases as they each have a unique packet structure
        if (opcode == GameMessageOpcode.GameEvent) {
            HandleInboundEvent(message, session);
        }
        if (messageHandlers.TryGetValue(opcode, out var GameMessageClass)) {
            // Todo: add these to a queue? Process multithreaded?
            try {
                InboundGameMessage GameMessage = (InboundGameMessage)Activator.CreateInstance(GameMessageClass);
                GameMessage.Handle(message, session);
                session.OnGameMessage(opcode, GameMessage);
            } catch (Exception ex) {
                log.Error($"Received GameMessage packet that threw an exception: opcode: 0x{(int)opcode:X4}:{opcode}");
                log.Error(ex);
            }
        } else {
            log.Warn($"Received unhandled fragment opcode: 0x{(int)opcode:X4} - {opcode}");
        }
    }

    public static void HandleInboundEvent(InboundMessage message, Session session)
    {
        GameEvent gameEventMessage = new GameEvent();
        gameEventMessage.Handle(message, session);

        if (eventHandlers.TryGetValue(gameEventMessage.EventType, out var gameEventClass)) {
            try {
                InboundGameEvent gameEvent = (InboundGameEvent)Activator.CreateInstance(gameEventClass);
                gameEvent.Handle(gameEventMessage, session);
                session.OnGameEvent(gameEventMessage.EventType, gameEvent);
            } catch (Exception ex) {
                log.Error($"Received GameEvent packet that threw an exception: type: 0x{(int)gameEventMessage.EventType:X4}:{gameEventMessage.EventType}");
                log.Error(ex);
            }
        } else {
            log.Warn($"Received unhandled event: type: 0x{(int)gameEventMessage.EventType:X4} - {gameEventMessage.EventType}");
        }
    }
}
