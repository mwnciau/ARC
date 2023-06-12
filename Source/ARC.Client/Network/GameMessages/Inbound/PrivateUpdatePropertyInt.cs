using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages.Inbound;

public class PrivateUpdatePropertyInt : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.PrivateUpdatePropertyInt;

    public byte[] Sequence { get; private set; }
    public PropertyInt Property { get; private set; }
    public int Value { get; private set; }


    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessagePrivateUpdatePropertyInt"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        Sequence = reader.ReadBytes(1);
        Property = (PropertyInt)reader.ReadUInt32();
        Value = reader.ReadInt32();
    }

    public override string ToString()
    {
        return $@"

        <<< GameMessage: PrivateUpdatePropertyInt [0x{(int)Opcode:X4}:{Opcode}]
            Sequence:  {Sequence}
            Property:  [0x{(int)Property:X4}:{Property}]
            Value:     {Value}

        ";
    }
}
