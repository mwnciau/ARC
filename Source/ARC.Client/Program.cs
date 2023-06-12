using ACE.Server;
using ACE.Server.Command;
using ARC.Client.EventListeners;
using ARC.Client.Network;
using ARC.Client.Network.Packets;
using log4net;
using log4net.Config;
using System.Globalization;
using System.Net;
using System.Reflection;

namespace ARC.Client;

internal class Program
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    static void Main(string[] args)
    {
        Console.Title = "ARC";

        // Typically, you wouldn't force the current culture on an entire application unless you know sure your application is used in a specific region (which ACE is not)
        // We do this because almost all of the client/user input/output code does not take culture into account, and assumes en-US formatting.
        // Without this, many commands that require special characters like , and . will break
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        // Init our text encoding options. This will allow us to use more than standard ANSI text, which the client also supports.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        setupLogger();

        InboundMessageManager.Initialize();

        log.Info("Connecting to server...");
        var session = new Session();
        var packetProcessor = new InboundPacketProcessor(session);
        var connection = new Connection(IPAddress.Parse("127.0.0.1"), 9100, 9000, packetProcessor);
        var packetQueue = new OutboundPacketQueue(connection);
        session.setPacketQueue(packetQueue);

        // Register event listeners
        PrintGameMessage.Initialize(session);
        LogCharacterIn.Initialize(session, "user", "Character Name");

        connection.Start();
        packetQueue.SendLoginRequest("user", "password");


        log.Info("Initializing CommandManager...");
        CommandManager.Initialize();

        while (true) {
            Thread.Sleep(100);
            if (packetQueue.ConnectionData != null) {
                packetQueue.Update();
            }
        }
    }

    private static uint CurrentConsolePosition = 0;

    static void setupLogger()
    {
        var exeLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var log4netConfig = Path.Combine(exeLocation, "log4net.config");
        var log4netConfigExample = Path.Combine(exeLocation, "log4net.config.example");

        var log4netFileInfo = new FileInfo("log4net.config");
        if (!log4netFileInfo.Exists)
            log4netFileInfo = new FileInfo(log4netConfig);

        if (!log4netFileInfo.Exists) {
            var exampleFile = new FileInfo(log4netConfigExample);
            if (!exampleFile.Exists) {
                Console.WriteLine("log4net Configuration file is missing.  Please copy the file log4net.config.example to log4net.config and edit it to match your needs before running ACE.");
                throw new Exception("missing log4net configuration file");
            } else {
                Console.WriteLine("log4net Configuration file is missing,  cloning from example file.");
                File.Copy(log4netConfigExample, log4netConfig);
            }
        }

        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        XmlConfigurator.ConfigureAndWatch(logRepository, log4netFileInfo);
    }
}
