using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent;
using ACE.Server.Network.GameMessages;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages.Inbound;

public class GameEvent : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.GameEvent;

    public ObjectGuid Guid;
    public uint Sequence;
    public GameEventType EventType;
    public BinaryReader Reader;


    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessagePrivateUpdatePropertyInt"/>
    public override void Handle(InboundMessage message, Session session)
    {
        Reader = new BinaryReader(message.Data);

        Guid = Reader.ReadGuid();
        Sequence = Reader.ReadUInt32();
        EventType = (GameEventType)Reader.ReadUInt32();
    }

    public override string ToString()
    {
        return $@"

        <<< GameMessage: GameEvent [0x{(int)Opcode:X4}:{Opcode}]
            Guid:      {Guid}
            Sequence:  {Sequence}
            Type:      [0x{(int)EventType:X4}:{EventType}]

        ";
    }
}
