using ACE.Server.Network;
using ACE.Server.Network.GameMessages;

namespace ARC.Client.Network.GameMessages.Outbound;

public class CharacterEnterWorld : GameMessage
{
    public CharacterEnterWorld (uint characterId, string account)
        : base(GameMessageOpcode.CharacterEnterWorld, GameMessageGroup.UIQueue)
    {
        Writer.Write(characterId);
        Writer.WriteString16L(account);
    }
}
