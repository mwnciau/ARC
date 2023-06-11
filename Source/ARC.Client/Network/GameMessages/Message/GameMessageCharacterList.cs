using ACE.Common.Extensions;
using ACE.Server.Network.GameMessages;
using ARC.Client.Entity;
using Character = ARC.Client.Entity.Character;
using InboundMessage = ACE.Server.Network.ClientMessage;

namespace ARC.Client.Network.GameMessages.Message;

public class GameMessageCharacterList : GameMessage
{
    public static new GameMessageOpcode Opcode = GameMessageOpcode.CharacterList;

    public string? AccountName { get; private set; }
    public uint CharacterSlots{ get; private set; }
    public List<Character>? CharacterList { get; private set; }
    public bool GlobalChatChannelsEnabled { get; private set; }

    /// <see cref="ACE.Server.Network.GameMessages.Messages.GameMessageCharacterList"/>
    public override void Handle(InboundMessage message, Session session)
    {
        var reader = new BinaryReader(message.Data);

        // Unused uint32 - always 0
        reader.Skip(4);

        int characterCount = reader.ReadInt32();

        CharacterList = new List<Character>();

        for (int i = 0; i < characterCount; i++) {
            uint characterId = reader.ReadUInt32();
            string characterName = reader.ReadString16L();
            uint deleteTime = reader.ReadUInt32();

            CharacterList.Add(new Character(characterId, characterName, deleteTime));
        }

        // Unused uint32 - always 0
        reader.Skip(4);

        CharacterSlots = reader.ReadUInt32();
        AccountName = reader.ReadString16L();

        GlobalChatChannelsEnabled = reader.ReadUInt32() == 1;

        // Message ends with `hasThroneOfDestiny` which is always 1

        session.Account = new Account(AccountName, CharacterList, CharacterSlots);
        session.GlobalChatChannelsEnabled = GlobalChatChannelsEnabled;
    }

    public override string ToString()
    {
        string output = $@"

        <<< GameMessageCharacterList [0x{(int)Opcode:X4}:{Opcode}]
            Account name:         {AccountName}
            Character slots:      {CharacterSlots}
            Global Chat Channels: {GlobalChatChannelsEnabled}
            Characters:";
        foreach (var character in CharacterList) {
            output += $"\n\t\t{character.Name} // id: {character.Id} deleteTime: {character.DeleteTime}";
        }
        output += "\n";

        return output;
    }
}
