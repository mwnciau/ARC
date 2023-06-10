using ARC.Client.Network;
using ARC.Client.Network.Packets;
using log4net;
using System.Net;

namespace ARC.Client;

internal class Program
{
    static void Main(string[] args)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var connection = new Connection(IPAddress.Parse("127.0.0.1"), 9100, 9000);
        var packetCoordinator = new OutboundPacketCoordinator(connection);
        var packetProcessor = new InboundPacketProcessor(packetCoordinator);
        connection.SetPacketProcessor(packetProcessor);

        connection.Start();
        packetCoordinator.SendLoginRequest("username", "password");

        while(true) {
            Thread.Sleep(100);
            if (packetCoordinator.ConnectionData != null) {
                packetCoordinator.Update();
            }
        }
    }
}
