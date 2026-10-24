using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace PowerMouse.App.Diagnostics;

internal static class Logger
{
    private const int RetainedVersionDirectories = 3;
    private const int RetainedDailyLogs = 14;
    private const int CrossProcessLockTimeoutMilliseconds = 2_000;
    private const string CrossProcessMutexName = @"Local\PowerMouse.Logger";
    private static readonly Lock WriterLock = new();
    private static StreamWriter? writer;

    public static string AppLogDirectoryPath { get; private set; } = string.Empty;

    public static string CurrentVersionLogDirectoryPath { get; private set; } = string.Empty;

    public static string CurrentLogFile { get; private set; } = string.Empty;

    public static void InitializeLogger()
    {
        lock (WriterLock)
        {
            if (writer is not null)
            {
                return;
            }

            try
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
                AppLogDirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PowerMouse",
                    "Logs");
                CurrentVersionLogDirectoryPath = Path.Combine(AppLogDirectoryPath, version);
                Directory.CreateDirectory(CurrentVersionLogDirectoryPath);

                CurrentLogFile = Path.Combine(
                    CurrentVersionLogDirectoryPath,
                    $"Log_{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");
                var stream = new FileStream(
                    CurrentLogFile,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true,
                };

                CleanupOldLogs();
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                Debug.WriteLine($"PowerMouse logger could not be initialized: {exception}");
            }
        }
    }

    public static void LogError(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        Log(message, "Error", memberName, sourceFilePath, sourceLineNumber);

    public static void LogError(
        string message,
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var details = new StringBuilder(message)
            .AppendLine()
            .Append(exception.GetType().FullName)
            .Append(" (0x")
            .Append(exception.HResult.ToString("X8", CultureInfo.InvariantCulture))
            .Append("): ")
            .AppendLine(exception.Message);
        if (exception.InnerException is not null)
        {
            details.Append("Inner exception: ")
                .Append(exception.InnerException.GetType().FullName)
                .Append(" (0x")
                .Append(exception.InnerException.HResult.ToString("X8", CultureInfo.InvariantCulture))
                .Append("): ")
                .AppendLine(exception.InnerException.Message);
        }

        details.AppendLine("Stack trace:").Append(exception.StackTrace ?? "Unavailable");
        Log(details.ToString(), "Error", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void LogWarning(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        Log(message, "Warning", memberName, sourceFilePath, sourceLineNumber);

    public static void LogInfo(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        Log(message, "Info", memberName, sourceFilePath, sourceLineNumber);

    public static void LogDebug(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
#if DEBUG
        Log(message, "Debug", memberName, sourceFilePath, sourceLineNumber);
#endif
    }

    public static void LogTrace(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        Log(string.Empty, "Trace", memberName, sourceFilePath, sourceLineNumber);

    public static void Shutdown()
    {
        lock (WriterLock)
        {
            writer?.Dispose();
            writer = null;
        }
    }

    private static void Log(string message, string level, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        var fileName = Path.GetFileName(sourceFilePath);
        var entry = new StringBuilder()
            .Append('[')
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append("] [p-")
            .Append(Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
            .Append("] [t-")
            .Append(Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture))
            .Append("] [")
            .Append(level)
            .Append("] ")
            .Append(string.IsNullOrEmpty(fileName) ? "Unknown" : fileName)
            .Append("::")
            .Append(memberName)
            .Append("::")
            .AppendLine(sourceLineNumber.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(message))
        {
            using var reader = new StringReader(message);
            while (reader.ReadLine() is { } line)
            {
                entry.Append("    ").AppendLine(line);
            }
        }

        lock (WriterLock)
        {
            Mutex? crossProcessMutex = null;
            var ownsCrossProcessMutex = false;
            try
            {
                if (writer is null)
                {
                    return;
                }

                crossProcessMutex = new Mutex(initiallyOwned: false, CrossProcessMutexName);
                try
                {
                    ownsCrossProcessMutex = crossProcessMutex.WaitOne(CrossProcessLockTimeoutMilliseconds);
                }
                catch (AbandonedMutexException)
                {
                    ownsCrossProcessMutex = true;
                }

                if (!ownsCrossProcessMutex)
                {
                    Debug.WriteLine("PowerMouse logger timed out while waiting for the cross-process log lock.");
                    return;
                }

                writer.Flush();
                writer.BaseStream.Seek(0, SeekOrigin.End);
                writer.Write(entry.ToString());
                writer.Flush();
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                Debug.WriteLine($"PowerMouse logger could not write: {exception}");
            }
            finally
            {
                if (ownsCrossProcessMutex)
                {
                    crossProcessMutex?.ReleaseMutex();
                }

                crossProcessMutex?.Dispose();
            }
        }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            foreach (var file in new DirectoryInfo(CurrentVersionLogDirectoryPath)
                         .EnumerateFiles("Log_*.log")
                         .OrderByDescending(file => file.LastWriteTimeUtc)
                         .Skip(RetainedDailyLogs))
            {
                file.Delete();
            }

            foreach (var directory in new DirectoryInfo(AppLogDirectoryPath)
                         .EnumerateDirectories()
                         .Where(directory => !string.Equals(
                             directory.FullName,
                             CurrentVersionLogDirectoryPath,
                             StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(directory => directory.LastWriteTimeUtc)
                         .Skip(RetainedVersionDirectories - 1))
            {
                directory.Delete(recursive: true);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Debug.WriteLine($"PowerMouse logger cleanup failed: {exception}");
        }
    }
}
