using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages;
using Castle.Components.DictionaryAdapter.Xml;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages.Inbound;
public class ServerMessage : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.ServerMessage;

    public string Message;
    public ChatMessageType ChatMessageType;

    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessageSystemChat"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        Message = reader.ReadString16L();
        ChatMessageType = (ChatMessageType)reader.ReadInt32();
    }

    public override string ToString()
    {
        return $@"

        <<< GameMessage: ServerMessage [0x{(int)Opcode:X4}:{Opcode}]
            Type:    [0x{(int)ChatMessageType:X4}:{ChatMessageType}]
            Message: {Message}
                     
        ";
    }
}
