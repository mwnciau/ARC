using ACE.Server.Network.GameMessages;
using ARC.Client.Entity;
using ARC.Client.Network;
using InboundGameMessage = ARC.Client.Network.GameMessages.InboundGameMessage;

namespace ARC.Client;
public class Session
{
    public bool GlobalChatChannelsEnabled;
    public Account? Account;

    public OutboundPacketQueue PacketQueue { get; private set; }

    public void setPacketQueue(OutboundPacketQueue packetQueue)
    {
        PacketQueue = packetQueue;
    }

    public delegate void GameMessageHandler(GameMessageOpcode opcode, InboundGameMessage message);
    public event GameMessageHandler? GameMessageEventListeners;
    public void OnGameMessage(GameMessageOpcode opcode, InboundGameMessage? message)
    {
        GameMessageEventListeners?.Invoke(opcode, message);
    }
}
