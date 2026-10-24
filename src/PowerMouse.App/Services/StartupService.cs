using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace PowerMouse.App.Services;

internal static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PowerMouse";

    public static void SetEnabled(bool enabled, bool elevated)
    {
        using var registration = new WindowsStartupRegistration();
        Apply(registration, enabled, elevated);
    }

    internal static void Apply(IStartupRegistration registration, bool enabled, bool elevated)
    {
        if (enabled && elevated)
        {
            // Keep the existing startup entry if Windows rejects task registration.
            registration.EnsureElevatedTask();
            registration.SetStandardStartup(false);
        }
        else
        {
            registration.RemoveElevatedTask();
            registration.SetStandardStartup(enabled);
        }
    }

    internal static string TaskName(string sid) => $"PowerMouse Startup {sid}";

    internal static dynamic? FindTask(dynamic folder, string taskName)
    {
        try { return folder.GetTask(taskName); }
        // COM interop can map HRESULT_FILE_NOT_FOUND to FileNotFoundException.
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070002)) { return null; }
    }

    internal static string CreateTaskXml(string sid, string executable)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement Element(string name, object content) => new(ns + name, content);
        return new XElement(ns + "Task", new XAttribute("version", "1.2"),
            Element("RegistrationInfo", Element("Description", "Start PowerMouse as administrator for this Windows user.")),
            Element("Triggers", Element("LogonTrigger", new object[] {
                Element("Enabled", "true"), Element("UserId", sid), Element("Delay", "PT3S") })),
            Element("Principals", new XElement(ns + "Principal", new XAttribute("id", "CurrentUser"),
                Element("UserId", sid), Element("LogonType", "InteractiveToken"), Element("RunLevel", "HighestAvailable"))),
            Element("Settings", new object[] {
                Element("MultipleInstancesPolicy", "IgnoreNew"),
                Element("DisallowStartIfOnBatteries", "false"), Element("StopIfGoingOnBatteries", "false"),
                Element("StartWhenAvailable", "false"), Element("ExecutionTimeLimit", "PT0S"), Element("Enabled", "true") }),
            new XElement(ns + "Actions", new XAttribute("Context", "CurrentUser"),
                Element("Exec", new object[] { Element("Command", executable), Element("WorkingDirectory", Path.GetDirectoryName(executable)!) })))
            .ToString();
    }

    internal static bool TaskMatches(string xml, string sid, string executable)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var task = XDocument.Parse(xml).Root!;
        var desired = XElement.Parse(CreateTaskXml(sid, executable));
        // Task Scheduler adds defaults and reformats XML when storing a definition.
        foreach (var section in new[] { "Triggers", "Principals", "Actions" })
        {
            var actualSection = task.Element(ns + section);
            if (actualSection?.Elements().Count() != 1) return false;
        }
        foreach (var expected in desired.Descendants().Where(element => !element.HasElements))
        {
            var actual = task;
            foreach (var name in expected.AncestorsAndSelf().Reverse().Skip(1).Select(element => element.Name))
                actual = actual?.Element(name);
            if (actual?.Value != expected.Value) return false;
        }
        return string.IsNullOrEmpty(task.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Arguments")?.Value);
    }

    private static void SetStandardStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unavailable.");
            key.SetValue(ValueName, $"\"{executable}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    internal interface IStartupRegistration
    {
        void EnsureElevatedTask();
        void RemoveElevatedTask();
        void SetStandardStartup(bool enabled);
    }

    private sealed class WindowsStartupRegistration : IStartupRegistration, IDisposable
    {
        private readonly string sid;
        private readonly string executable;
        private readonly dynamic service;
        private readonly dynamic folder;

        internal WindowsStartupRegistration()
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value ?? throw new InvalidOperationException("The Windows user could not be identified.");
            executable = Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unavailable.");
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!)!;
            try
            {
                service.Connect();
                folder = service.GetFolder(@"\");
            }
            catch
            {
                Marshal.FinalReleaseComObject(service);
                throw;
            }
        }

        public void EnsureElevatedTask()
        {
            var existing = GetTask();
            if (existing is not null)
            {
                try
                {
                    if (TaskMatches((string)existing.Xml, sid, executable)) return;
                }
                finally { Marshal.FinalReleaseComObject(existing); }
            }
            if (!AdministratorRestartService.IsAdministrator)
                throw new InvalidOperationException("Restart PowerMouse as administrator to enable or repair administrator startup.");

            // Match PowerToys: interactive current-user logon, no stored password,
            // and task management restricted to this user, administrators and SYSTEM.
            var task = folder.RegisterTask(TaskName(sid), CreateTaskXml(sid, executable), 6,
                sid, null, 3, $"D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;{sid})");
            Marshal.FinalReleaseComObject(task);
        }

        public void RemoveElevatedTask()
        {
            var task = GetTask();
            if (task is null) return;
            Marshal.FinalReleaseComObject(task);
            folder.DeleteTask(TaskName(sid), 0);
        }

        private dynamic? GetTask()
        {
            return FindTask(folder, TaskName(sid));
        }

        public void SetStandardStartup(bool enabled) => StartupService.SetStandardStartup(enabled);

        public void Dispose()
        {
            Marshal.FinalReleaseComObject(folder);
            Marshal.FinalReleaseComObject(service);
        }
    }
}
