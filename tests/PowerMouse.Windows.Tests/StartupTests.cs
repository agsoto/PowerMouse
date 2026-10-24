using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerMouse.App.Services;

namespace PowerMouse.Windows.Tests;

[TestClass]
public sealed class StartupTests
{
    private static readonly string[] ElevatedOperations = ["elevated", "standard:False"];
    private static readonly string[] RejectedOperations = ["elevated"];

    [TestMethod]
    public void ElevatedStartupReplacesStandardEntryOnlyAfterTaskIsReady()
    {
        var registration = new Registration();
        StartupService.Apply(registration, true, true);
        CollectionAssert.AreEqual(ElevatedOperations, registration.Operations);
    }

    [TestMethod]
    public void RejectedElevatedTaskPreservesExistingStartup()
    {
        var registration = new Registration { RejectTask = true };
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => StartupService.Apply(registration, true, true));
        CollectionAssert.AreEqual(RejectedOperations, registration.Operations);
    }

    [TestMethod]
    public void DisablingElevationOrStartupRemovesTheTask()
    {
        foreach (var (startup, elevated) in new[] { (true, false), (false, true), (false, false) })
        {
            var registration = new Registration();
            StartupService.Apply(registration, startup, elevated);
            CollectionAssert.AreEqual(new[] { "remove", $"standard:{startup}" }, registration.Operations);
        }
    }

    [TestMethod]
    public void TaskTargetsOnlyCurrentUserAndEscapesExecutablePath()
    {
        const string sid = "S-1-5-21-1-2-3-1001";
        const string executable = @"C:\Apps & Tools\PowerMouse\PowerMouse.exe";
        var xml = StartupService.CreateTaskXml(sid, executable);
        var root = XElement.Parse(xml);
        XNamespace ns = root.Name.Namespace;
        Assert.IsTrue(StartupService.TaskMatches(xml, sid, executable));
        Assert.IsFalse(StartupService.TaskMatches(xml, sid, executable + ".other"));
        Assert.IsFalse(StartupService.TaskMatches(xml, sid + "2", executable));
        Assert.AreEqual("InteractiveToken", root.Descendants(ns + "LogonType").Single().Value);
        Assert.AreEqual("HighestAvailable", root.Descendants(ns + "RunLevel").Single().Value);
        Assert.AreEqual(sid, root.Descendants(ns + "LogonTrigger").Single().Element(ns + "UserId")!.Value);
        Assert.AreEqual("PT0S", root.Descendants(ns + "ExecutionTimeLimit").Single().Value);
        Assert.AreEqual("false", root.Descendants(ns + "StopIfGoingOnBatteries").Single().Value);
        root.Descendants(ns + "Exec").Single().Add(new XElement(ns + "Arguments", "--shutdown"));
        Assert.IsFalse(StartupService.TaskMatches(root.ToString(), sid, executable));
    }

    [TestMethod]
    public void DisabledOrExtraActionTaskNeedsRepair()
    {
        const string sid = "S-1-5-21-1-2-3-1001";
        const string exe = @"C:\PowerMouse\PowerMouse.exe";
        var root = XElement.Parse(StartupService.CreateTaskXml(sid, exe));
        XNamespace ns = root.Name.Namespace;
        root.Element(ns + "Settings")!.Element(ns + "Enabled")!.Value = "false";
        Assert.IsFalse(StartupService.TaskMatches(root.ToString(), sid, exe));
        root.Element(ns + "Settings")!.Element(ns + "Enabled")!.Value = "true";
        root.Element(ns + "Actions")!.Add(new XElement(ns + "Exec", new XElement(ns + "Command", "other.exe")));
        Assert.IsFalse(StartupService.TaskMatches(root.ToString(), sid, exe));
    }

    [TestMethod]
    public void WindowsAcceptsTaskDefinitionWithoutRegisteringIt()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User!.Value;
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        try
        {
            service.Connect();
            dynamic definition = service.NewTask(0);
            try
            {
                definition.XmlText = StartupService.CreateTaskXml(sid, Environment.ProcessPath!);
                Assert.IsTrue(StartupService.TaskMatches((string)definition.XmlText, sid, Environment.ProcessPath!),
                    "Windows-normalized definitions must be recognized without re-registering on each launch.");
            }
            finally { Marshal.FinalReleaseComObject(definition); }
            dynamic folder = service.GetFolder(@"\");
            try
            {
                Assert.IsNull((object?)StartupService.FindTask(folder, $"PowerMouse.Tests.Missing-{Guid.NewGuid():N}"));
                // TASK_VALIDATE_ONLY = 1: validates XML without creating a task.
                folder.RegisterTask(null, StartupService.CreateTaskXml(sid, Environment.ProcessPath!), 1, sid, null, 3, null);
            }
            finally { Marshal.FinalReleaseComObject(folder); }
        }
        finally { Marshal.FinalReleaseComObject(service); }
    }

    private sealed class Registration : StartupService.IStartupRegistration
    {
        internal List<string> Operations { get; } = [];
        internal bool RejectTask { get; init; }
        public void EnsureElevatedTask()
        {
            Operations.Add("elevated");
            if (RejectTask) throw new UnauthorizedAccessException();
        }
        public void RemoveElevatedTask() => Operations.Add("remove");
        public void SetStandardStartup(bool enabled) => Operations.Add($"standard:{enabled}");
    }
}
