using System.IO.Pipes;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using PowerMouse.App.Diagnostics;

namespace PowerMouse.App.Services;

internal enum SingleInstanceCommand
{
    ShowSettings,
    Shutdown,
    InspectHints,
}

internal sealed class SingleInstanceCommandService : IDisposable
{
    private const int ClientConnectTimeoutMilliseconds = 3_000;
    private const int MaximumCommandLength = 64;
    private static readonly string PipeName = $"PowerMouse.Commands.{Process.GetCurrentProcess().SessionId}";

    private readonly DispatcherQueue dispatcherQueue;
    private readonly Action<SingleInstanceCommand> commandHandler;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Task listenerTask;
    private bool disposed;

    public SingleInstanceCommandService(
        DispatcherQueue dispatcherQueue,
        Action<SingleInstanceCommand> commandHandler)
    {
        this.dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        this.commandHandler = commandHandler ?? throw new ArgumentNullException(nameof(commandHandler));
        listenerTask = ListenAsync(cancellationTokenSource.Token);
    }

    public static bool TrySend(SingleInstanceCommand command)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            client.Connect(ClientConnectTimeoutMilliseconds);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(command.ToString());
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Logger.LogWarning($"Could not send the {command} command to the running instance: {exception.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellationTokenSource.Cancel();
        try
        {
            listenerTask.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Logger.LogError("The single-instance command listener did not stop cleanly.", exception);
        }

        cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (message is null || message.Length > MaximumCommandLength ||
                    !Enum.TryParse<SingleInstanceCommand>(message, ignoreCase: true, out var command))
                {
                    Logger.LogWarning("The running instance received an invalid command.");
                    continue;
                }

                if (!dispatcherQueue.TryEnqueue(() => commandHandler(command)))
                {
                    Logger.LogWarning($"The {command} command could not be queued on the application thread.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("The single-instance command listener failed and will restart.", exception);
            }
        }
    }
}
