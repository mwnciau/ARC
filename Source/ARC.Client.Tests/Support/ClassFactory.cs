using ACE.Server.Factories;
using ACE.Server.Network;
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
