using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;

namespace PowerMouse.App.Services;

internal static class AdministratorRestartService
{
    private const string RestartArgument = "--restart-elevated";
    private const int CancelledError = 1223;
    private static readonly TimeSpan HandoffTimeout = TimeSpan.FromSeconds(15);

    internal readonly record struct RestartRequest(int ProcessId, long StartTimeTicks, Guid Nonce)
    {
        internal string EventName => $@"Local\PowerMouse.ElevatedRestart.{Nonce:N}";
    }

    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    // The caller shuts down only after the elevated process acknowledges readiness.
    // Cancellation, launch failure, or timeout leaves the original instance running.
    public static async Task<bool> RequestRestartAsync(
        Func<ProcessStartInfo, Process?>? launch = null,
        TimeSpan? readinessTimeout = null,
        bool showSettings = true)
    {
        using var current = Process.GetCurrentProcess();
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("PowerMouse's executable could not be located.");
        var request = new RestartRequest(current.Id, current.StartTime.ToUniversalTime().Ticks, Guid.NewGuid());
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, request.EventName);
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add(RestartArgument);
        startInfo.ArgumentList.Add(request.ProcessId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(request.StartTimeTicks.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(request.Nonce.ToString("N", CultureInfo.InvariantCulture));
        if (showSettings) startInfo.ArgumentList.Add("--show-settings");

        Process? replacement;
        try
        {
            replacement = (launch ?? Process.Start)(startInfo);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == CancelledError)
        {
            return false;
        }

        using (replacement)
        {
            if (replacement is null)
                throw new InvalidOperationException("Windows did not start an administrator instance. PowerMouse is still running normally.");

            if (!await Task.Run(() => ready.WaitOne(readinessTimeout ?? HandoffTimeout)).ConfigureAwait(false))
                throw new TimeoutException("The administrator instance did not become ready. PowerMouse is still running normally. Use the same Windows account when approving the permission prompt.");
        }

        return true;
    }

    public static void CompleteRestartIfRequested(string[] arguments)
    {
        var request = ParseRestartRequest(arguments);
        if (request is null) return;
        if (!IsAdministrator)
            throw new InvalidOperationException("The replacement instance does not have administrator permissions.");

        using var parent = Process.GetProcessById(request.Value.ProcessId);
        using var current = Process.GetCurrentProcess();
        if (parent.Id == current.Id || parent.SessionId != current.SessionId ||
            parent.StartTime.ToUniversalTime().Ticks != request.Value.StartTimeTicks ||
            !string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The administrator restart no longer matches the original PowerMouse instance.");

        // This one-use event permits the elevated process to acknowledge the lower-
        // integrity parent without weakening the ordinary command pipe's access rules.
        using var ready = EventWaitHandle.OpenExisting(request.Value.EventName);
        if (!ready.Set()) throw new InvalidOperationException("Could not acknowledge the administrator restart.");
        if (!parent.WaitForExit(checked((int)HandoffTimeout.TotalMilliseconds)))
            throw new TimeoutException("The original PowerMouse instance did not exit; the replacement will exit to avoid duplicate input hooks.");
    }

    internal static RestartRequest? ParseRestartRequest(string[] arguments)
    {
        var index = Array.IndexOf(arguments, RestartArgument);
        if (index < 0) return null;
        if (arguments.Count(argument => argument == RestartArgument) != 1 || index + 3 >= arguments.Length ||
            !int.TryParse(arguments[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var processId) || processId <= 0 ||
            !long.TryParse(arguments[index + 2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0 || ticks > DateTime.MaxValue.Ticks ||
            !Guid.TryParseExact(arguments[index + 3], "N", out var nonce) || nonce == Guid.Empty)
            throw new ArgumentException("The administrator restart arguments are invalid.", nameof(arguments));
        return new RestartRequest(processId, ticks, nonce);
    }
}
