using System.Text.Json.Serialization;

namespace PowerMouse.Core.Models;

public sealed class PowerMouseSettings
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool EnabledOnStartup { get; set; }

    public bool StartWithWindows { get; set; }

    public bool ShowNotifications { get; set; } = true;

    public bool ShowPointerIndicator { get; set; }

    public HotkeyGesture MouseHintsShortcut { get; set; } = new()
    {
        Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
        VirtualKey = VirtualKeys.Space,
    };

    public bool MouseHintsAutoClick { get; set; } = true;

    public HotkeyModifiers MouseHintsActionModifier { get; set; } = HotkeyModifiers.Shift;

    [JsonPropertyName("ClickHintsShortcut")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HotkeyGesture? LegacyClickHintsShortcut
    {
        get => null;
        set
        {
            if (value is not null)
            {
                MouseHintsShortcut = value;
            }
        }
    }

    public Guid ActiveProfileId { get; set; }

    public List<PowerMouseProfile> Profiles { get; set; } = [];

    public PowerMouseProfile ActiveProfile => Profiles.First(profile => profile.Id == ActiveProfileId);

    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;
        MouseHintsShortcut ??= new HotkeyGesture
        {
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = VirtualKeys.Space,
        };
        if (MouseHintsActionModifier is not (
                HotkeyModifiers.Alt or
                HotkeyModifiers.Control or
                HotkeyModifiers.Shift or
                HotkeyModifiers.Windows))
        {
            MouseHintsActionModifier = HotkeyModifiers.Shift;
        }
        Profiles ??= [];
        Profiles.RemoveAll(profile => profile is null);

        if (Profiles.Count == 0)
        {
            Profiles.Add(new PowerMouseProfile());
        }

        foreach (var profile in Profiles)
        {
            profile.Normalize();
        }

        if (Profiles.All(profile => profile.Id != ActiveProfileId))
        {
            ActiveProfileId = Profiles[0].Id;
        }
    }

    public static PowerMouseSettings CreateDefault()
    {
        var profile = new PowerMouseProfile();
        return new PowerMouseSettings
        {
            ActiveProfileId = profile.Id,
            Profiles = [profile],
        };
    }
}
