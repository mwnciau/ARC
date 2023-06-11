using ACE.Server.Network.GameMessages;
using ARC.Client.Network.GameMessages.Outbound;
using ARC.Client.Network.GameMessages.Inbound;
using log4net;
using System.Reflection;
using System.Runtime.CompilerServices;
using InboundGameMessage = ARC.Client.Network.GameMessages.InboundGameMessage;

namespace ARC.Client.EventListeners;
public class LogCharacterIn
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private static Session? Session;
    private static string? AccountName;
    private static string? CharacterName;

    public static void Initialize(Session session, string accountName, string characterName)
    {
        session.GameMessageEventListeners += OnGameMessage;

        Session = session;
        AccountName = accountName;
        CharacterName = characterName;
    }

    public static void OnGameMessage(GameMessageOpcode opcode, InboundGameMessage message)
    {
        if (opcode == GameMessageOpcode.CharacterList) {
            var characterList = (CharacterList)message;
            uint characterId = characterList.Characters.Where(c => c.Name == CharacterName).First().Id;

            log.Info($"Logging in character {AccountName} / {CharacterName} [{characterId}]");
            Session.PacketQueue.EnqueueSend(new CharacterEnterWorld(characterId, AccountName));
        }
    }
}
