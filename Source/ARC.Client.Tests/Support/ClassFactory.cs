using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.WorldObjects;
using Moq;
using System.Net;
using ServerSession = ACE.Server.Network.Session;
using WorldObject = ACE.Server.WorldObjects.WorldObject;

namespace ARC.Client.Tests.Support;
internal static class ClassFactory
{
    public static Mock<ServerSession> MockServerSession()
    {
        return new Mock<ServerSession>(
            new ConnectionListener(IPAddress.Any, 9000),
            new IPEndPoint(IPAddress.Any, 9000),
            (ushort)0,
            (ushort)0
        );
    }

    public static Player Player()
    {
        /*return new Player(new ACE.Entity.Models.Weenie(), new ObjectGuid(123), 1) {
            CombatTableDID = 1
        };*/
        var charInfo = new CharacterCreateInfo() {
            Heritage = HeritageGroup.Aluvian,
            Gender = 1,
        };
        ACE.Entity.Models.Weenie weenie = DatabaseManager.World.GetCachedWeenie("human");
        PlayerFactory.Create(charInfo, weenie, GuidManager.NewPlayerGuid(), 0, ACE.Entity.Enum.WeenieType.Undef, out Player player);
        return player;
    }

    public static WorldObject WorldObject()
    {
        var treasureProfile = new ACE.Database.Models.World.TreasureDeath();
        treasureProfile.Tier = 5;
        treasureProfile.MundaneItemChance = 100;
        treasureProfile.MundaneItemMinAmount = 1;
        treasureProfile.MundaneItemMaxAmount = 1;
        treasureProfile.MundaneItemTypeSelectionChances = 1;

        return LootGenerationFactory.CreateRandomLootObjects(treasureProfile)[0];
    }
}
