using ACE.Server.Network;
using Moq;
using System.Net;
using ServerSession = ACE.Server.Network.Session;
using WorldObject = ACE.Server.WorldObjects.WorldObject;
using WorldObjectStub = ARC.Client.Tests.Stubs.WorldObject;

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
        return new WorldObjectStub();
    }
}
