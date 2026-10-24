using System.ComponentModel;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerMouse.App.Services;

namespace PowerMouse.Windows.Tests;

[TestClass]
public sealed class AdministratorRestartTests
{
    [TestMethod]
    public async Task CancellingUacDoesNotAuthorizeShutdown()
    {
        var result = await AdministratorRestartService.RequestRestartAsync(_ => throw new Win32Exception(1223));
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task LaunchFailureDoesNotAuthorizeShutdown()
    {
        await Assert.ThrowsExactlyAsync<Win32Exception>(() =>
            AdministratorRestartService.RequestRestartAsync(_ => throw new Win32Exception(2)));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            AdministratorRestartService.RequestRestartAsync(_ => null));
    }

    [TestMethod]
    public async Task UnresponsiveReplacementDoesNotAuthorizeShutdown()
    {
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            AdministratorRestartService.RequestRestartAsync(_ => Process.GetCurrentProcess(), TimeSpan.FromMilliseconds(20)));
    }

    [TestMethod]
    public async Task RestartRequiresAcknowledgementAndUsesCurrentExecutable()
    {
        using var acknowledged = new ManualResetEventSlim();
        var result = await AdministratorRestartService.RequestRestartAsync(info =>
        {
            Assert.AreEqual(Environment.ProcessPath, info.FileName);
            Assert.IsTrue(info.UseShellExecute);
            Assert.AreEqual("runas", info.Verb);
            CollectionAssert.Contains(info.ArgumentList.ToArray(), "--show-settings");
            var request = AdministratorRestartService.ParseRestartRequest(info.ArgumentList.ToArray());
            Assert.IsNotNull(request);
            using var current = Process.GetCurrentProcess();
            Assert.AreEqual(current.Id, request.Value.ProcessId);
            Assert.AreEqual(current.StartTime.ToUniversalTime().Ticks, request.Value.StartTimeTicks);
            using var ready = EventWaitHandle.OpenExisting(request.Value.EventName);
            Assert.IsTrue(ready.Set());
            acknowledged.Set();
            return Process.GetCurrentProcess();
        });
        Assert.IsTrue(result);
        Assert.IsTrue(acknowledged.IsSet);
    }

    [TestMethod]
    public async Task AutomaticRestartPreservesBackgroundLaunch()
    {
        var result = await AdministratorRestartService.RequestRestartAsync(info =>
        {
            CollectionAssert.DoesNotContain(info.ArgumentList.ToArray(), "--show-settings");
            var request = AdministratorRestartService.ParseRestartRequest(info.ArgumentList.ToArray())!.Value;
            using var ready = EventWaitHandle.OpenExisting(request.EventName);
            ready.Set();
            return Process.GetCurrentProcess();
        }, showSettings: false);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void OrdinaryLaunchDoesNotRequestAnAdministratorHandoff()
    {
        Assert.IsNull(AdministratorRestartService.ParseRestartRequest(["PowerMouse.exe", "--show-settings"]));
        AdministratorRestartService.CompleteRestartIfRequested(["PowerMouse.exe", "--show-settings"]);
    }

    [TestMethod]
    public void MalformedHandoffArgumentsAreRejected()
    {
        string[][] invalid =
        [
            ["--restart-elevated"],
            ["--restart-elevated", "0", "10", Guid.NewGuid().ToString("N")],
            ["--restart-elevated", "123", "-1", Guid.NewGuid().ToString("N")],
            ["--restart-elevated", "123", "10", @"Global\UnrelatedEvent"],
            ["--restart-elevated", "123", "10", Guid.Empty.ToString("N")],
            ["--restart-elevated", "123", "10", Guid.NewGuid().ToString("N"), "--restart-elevated"],
        ];
        foreach (var arguments in invalid)
            Assert.ThrowsExactly<ArgumentException>(() => AdministratorRestartService.ParseRestartRequest(arguments));
    }
}
