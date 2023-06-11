using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using log4net;

using ACE.Server.Network;

using InboundPacket = ACE.Server.Network.ClientPacket;

namespace ARC.Client.Network;

public class Connection
{
    private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

    public Socket Socket { get; private set; }

    private readonly IPEndPoint localEndpoint;
    public readonly IPEndPoint ServerEndpoint;

    private readonly byte[] buffer = new byte[InboundPacket.MaxPacketSize];

    private InboundPacketProcessor packetProcessor;

    public Connection(IPAddress serverHost, int serverPort, int localPort, InboundPacketProcessor packetProcessor)
    {
        log.DebugFormat("Connection ctor, {0}", localEndpoint);

        localEndpoint = new IPEndPoint(IPAddress.Any, localPort);
        ServerEndpoint = new IPEndPoint(serverHost, serverPort);
        this.packetProcessor = packetProcessor;
    }

    public void Start()
    {
        log.DebugFormat("Starting Connection, {0}", localEndpoint);

        try
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(localEndpoint);
            Listen();
        }
        catch (Exception exception)
        {
            log.FatalFormat("Network Socket has thrown: {0}", exception.Message);
        }
    }

    public bool IsActive()
    {
        return Socket != null && Socket.IsBound;
    }

    public void Shutdown()
    {
        log.DebugFormat("Shutting down Connection, {0}", localEndpoint);

        if (Socket != null && Socket.IsBound) {
            Socket.Close();
        }
    }

    private void Listen()
    {
        try
        {
            EndPoint clientEndPoint = new IPEndPoint(localEndpoint.Address, 0);
            Socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref clientEndPoint, OnDataReceive, Socket);
        }
        catch (SocketException socketException)
        {
            log.DebugFormat("Connection.Listen() has thrown {0}: {1}", socketException.SocketErrorCode, socketException.Message);
            Listen();
        }
        catch (Exception exception)
        {
            log.FatalFormat("Connection.Listen() has thrown: {0}", exception.Message);
        }
    }

    private void OnDataReceive(IAsyncResult result)
    {
        EndPoint clientEndPoint = null;

        try
        {
            clientEndPoint = new IPEndPoint(localEndpoint.Address, 0);
            int dataSize = Socket.EndReceiveFrom(result, ref clientEndPoint);

            // TO-DO: generate ban entries here based on packet rates of endPoint, IP Address, and IP Address Range

            if (packetLog.IsDebugEnabled)
            {
                byte[] data = new byte[dataSize];
                Buffer.BlockCopy(buffer, 0, data, 0, dataSize);

                var sb = new StringBuilder();
                sb.AppendLine($"Received Packet (Len: {data.Length}) [{ServerEndpoint.Address}:{ServerEndpoint.Port}=>{localEndpoint.Address}:{localEndpoint.Port}]");
                sb.AppendLine(data.BuildPacketString());
                packetLog.Debug(sb.ToString());
            }

            var packet = new InboundPacket();

            if (packet.Unpack(buffer, dataSize))
            {
                packetProcessor.Process(packet);
            }

            packet.ReleaseBuffer();
        }
        catch (SocketException socketException)
        {
            // If we get "Connection has been forcibly closed..." error, just eat the exception and continue on
            // This gets sent when the remote host terminates the connection (on UDP? interesting...)
            // TODO: There might be more, should keep an eye out. Logged message will help here.
            if (socketException.SocketErrorCode == SocketError.MessageSize ||
                socketException.SocketErrorCode == SocketError.NetworkReset ||
                socketException.SocketErrorCode == SocketError.ConnectionReset)
            {
                log.DebugFormat("Connection.OnDataReceieve() has thrown {0}: {1} from client {2}", socketException.SocketErrorCode, socketException.Message, clientEndPoint != null ? clientEndPoint.ToString() : "Unknown");
            }
            else
            {
                log.FatalFormat("Connection.OnDataReceieve() has thrown {0}: {1} from client {2}", socketException.SocketErrorCode, socketException.Message, clientEndPoint != null ? clientEndPoint.ToString() : "Unknown");
                return;
            }
        }

        if (result.CompletedSynchronously) {
            Task.Run(() => Listen());
        } else {
            Listen();
        }
    }
}
