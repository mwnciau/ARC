using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.Server.Managers;
using log4net;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ARC.Client.Tests;

[TestClass]
public class TestCase
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void TestFixtureSetup(TestContext context)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        File.Copy(Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\..\\..\\ACE.Server\\bin\\x64\\Debug\\net6.0\\Config.js"), ".\\Config.js", true);
        ConfigManager.Initialize();

        DatManager.Initialize(ConfigManager.Config.Server.DatFilesDirectory, true);

        DatabaseManager.Initialize();
        DatabaseManager.Start();

        GuidManager.Initialize();

    }
}
