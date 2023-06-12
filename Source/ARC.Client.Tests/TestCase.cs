using ACE.Common;
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

        File.Copy(Path.Combine(Environment.CurrentDirectory, "..\\..\\..\\..\\..\\ACE.Server\\Config.js.example"), ".\\Config.js", true);
        ConfigManager.Initialize();
    }
}
