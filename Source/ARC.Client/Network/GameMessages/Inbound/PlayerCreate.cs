using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages.Inbound;

public class PlayerCreate : InboundGameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.PlayerCreate;

    public ObjectGuid Guid;


    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessagePrivateUpdatePropertyInt"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        Guid = reader.ReadGuid();
    }

    public override string ToString()
    {
        return $@"

        <<< GameMessage: PlayerCreate [0x{(int)Opcode:X4}:{Opcode}]
            Guid:  {Guid}

        ";
    }
}
