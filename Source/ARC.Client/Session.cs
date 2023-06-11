using ACE.Server.Network.GameMessages;
using ARC.Client.Entity;
using ARC.Client.Network;
using GameMessage = ARC.Client.Network.GameMessages.GameMessage;

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

    public delegate void GameMessageHandler(GameMessageOpcode opcode, GameMessage message);
    public event GameMessageHandler? GameMessageEventListeners;
    public void OnGameMessage(GameMessageOpcode opcode, GameMessage? message)
    {
        GameMessageEventListeners?.Invoke(opcode, message);
    }
}
