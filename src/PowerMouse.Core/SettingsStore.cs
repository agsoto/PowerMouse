using System.Text.Json;
using System.Text.Json.Serialization;
using PowerMouse.Core.Models;

namespace PowerMouse.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public PowerMouseSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return PowerMouseSettings.CreateDefault();
            }

            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize<PowerMouseSettings>(stream, SerializerOptions) ??
                           PowerMouseSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            PreserveInvalidSettings();
            return PowerMouseSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PowerMouseSettings.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return PowerMouseSettings.CreateDefault();
        }
    }

    public void Save(PowerMouseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{FilePath}.{Environment.ProcessId}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void PreserveInvalidSettings()
    {
        try
        {
            var invalidPath = $"{FilePath}.invalid-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            File.Move(FilePath, invalidPath, overwrite: false);
        }
        catch (IOException)
        {
            // A malformed settings file should never prevent the app from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // A malformed settings file should never prevent the app from starting.
        }
    }
}
