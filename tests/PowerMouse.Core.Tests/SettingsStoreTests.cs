using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerMouse.Core.Models;

namespace PowerMouse.Core.Tests;

[TestClass]
public sealed class SettingsStoreTests
{
    [TestMethod]
    public void RoundTripPreservesProfilesAndEnumBindings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"PowerMouse.Tests-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "settings.json");

        try
        {
            var settings = PowerMouseSettings.CreateDefault();
            settings.StartWithWindows = true;
            settings.AlwaysRunAsAdministrator = true;
            settings.MouseHintsShortcut = new HotkeyGesture
            {
                Modifiers = HotkeyModifiers.Alt,
                VirtualKey = VirtualKeys.KeyA + ('H' - 'A'),
            };
            settings.MouseHintsAutoClick = false;
            settings.MouseHintsEnabled = false;
            settings.MouseHintsActionModifier = HotkeyModifiers.Control;
            settings.ActiveProfile.Name = "Editing";
            settings.ActiveProfile.Bindings[InputAction.MoveUp] = VirtualKeys.KeyW;
            settings.ActiveProfile.AccelerationCurve = AccelerationCurve.Smooth;
            settings.ActiveProfile.AccelerationDurationMilliseconds = 950;
            settings.ActiveProfile.InitialSpeedPercent = 25;
            settings.ActiveProfile.NormalizeDiagonalMovement = true;
            settings.ActiveProfile.ScrollStep = 240;
            var store = new SettingsStore(path);

            store.Save(settings);
            var loaded = store.Load();

            Assert.IsTrue(loaded.StartWithWindows);
            Assert.IsTrue(loaded.AlwaysRunAsAdministrator);
            Assert.AreEqual(settings.MouseHintsShortcut, loaded.MouseHintsShortcut);
            Assert.IsFalse(loaded.MouseHintsAutoClick);
            Assert.IsFalse(loaded.MouseHintsEnabled);
            Assert.AreEqual(HotkeyModifiers.Control, loaded.MouseHintsActionModifier);
            Assert.AreEqual("Editing", loaded.ActiveProfile.Name);
            Assert.AreEqual(VirtualKeys.KeyW, loaded.ActiveProfile.Bindings[InputAction.MoveUp]);
            Assert.AreEqual(AccelerationCurve.Smooth, loaded.ActiveProfile.AccelerationCurve);
            Assert.AreEqual(950, loaded.ActiveProfile.AccelerationDurationMilliseconds);
            Assert.AreEqual(25, loaded.ActiveProfile.InitialSpeedPercent);
            Assert.IsTrue(loaded.ActiveProfile.NormalizeDiagonalMovement);
            Assert.AreEqual(240, loaded.ActiveProfile.ScrollStep);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void MissingFileReturnsUsableDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PowerMouse-missing-{Guid.NewGuid():N}.json");

        var settings = new SettingsStore(path).Load();

        Assert.AreEqual(1, settings.Profiles.Count);
        Assert.AreEqual(settings.Profiles[0].Id, settings.ActiveProfileId);
    }

    [TestMethod]
    public void MalformedFileIsPreservedAndReplacedWithDefaultsInMemory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"PowerMouse.Tests-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "settings.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{ this is not JSON }");

            var settings = new SettingsStore(path).Load();

            Assert.AreEqual(1, settings.Profiles.Count);
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(1, Directory.GetFiles(root, "settings.json.invalid-*").Length);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ValidJsonWithNullProfileMembersIsNormalized()
    {
        var root = Path.Combine(Path.GetTempPath(), $"PowerMouse.Tests-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "settings.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                path,
                """
                {
                  "ActiveProfileId": "00000000-0000-0000-0000-000000000001",
                  "ClickHintsShortcut": null,
                  "Profiles": [
                    {
                      "Id": "00000000-0000-0000-0000-000000000001",
                      "Name": "Recovered",
                      "ActivationShortcut": null,
                      "Bindings": null
                    }
                  ]
                }
                """);

            var loaded = new SettingsStore(path).Load();
            var profile = loaded.ActiveProfile;

            Assert.IsFalse(loaded.AlwaysRunAsAdministrator, "Existing settings must not opt into elevation.");

            Assert.IsNotNull(profile.ActivationShortcut);
            Assert.AreEqual(VirtualKeys.Numpad8, profile.Bindings[InputAction.MoveUp]);
            Assert.AreEqual(VirtualKeys.Space, loaded.MouseHintsShortcut.VirtualKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void MissingMouseHintsSettingsUseDefaults()
    {
        var settings = PowerMouseSettings.CreateDefault();

        Assert.AreEqual(
            new HotkeyGesture
            {
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKey = VirtualKeys.Space,
            },
            settings.MouseHintsShortcut);
        Assert.IsTrue(settings.MouseHintsAutoClick);
        Assert.IsTrue(settings.MouseHintsEnabled);
        Assert.AreEqual(HotkeyModifiers.Shift, settings.MouseHintsActionModifier);
    }

    [TestMethod]
    public void AdvancedMovementSettingsUseBackwardCompatibleDefaults()
    {
        var profile = PowerMouseSettings.CreateDefault().ActiveProfile;

        Assert.AreEqual(AccelerationCurve.None, profile.AccelerationCurve);
        Assert.AreEqual(750, profile.AccelerationDurationMilliseconds);
        Assert.AreEqual(35, profile.InitialSpeedPercent);
        Assert.IsFalse(profile.NormalizeDiagonalMovement);
        Assert.AreEqual(120, profile.ScrollStep);
    }

    [TestMethod]
    public void AdvancedMovementSettingsAreNormalizedToSafeRanges()
    {
        var profile = new PowerMouseProfile
        {
            AccelerationCurve = (AccelerationCurve)999,
            AccelerationDurationMilliseconds = 0,
            InitialSpeedPercent = 500,
            ScrollStep = -1,
        };

        profile.Normalize();

        Assert.AreEqual(AccelerationCurve.None, profile.AccelerationCurve);
        Assert.AreEqual(100, profile.AccelerationDurationMilliseconds);
        Assert.AreEqual(100, profile.InitialSpeedPercent);
        Assert.AreEqual(20, profile.ScrollStep);
    }

    [TestMethod]
    public void LegacyClickHintsShortcutMigratesToMouseHintsShortcut()
    {
        var root = Path.Combine(Path.GetTempPath(), $"PowerMouse.Tests-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "settings.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                path,
                """
                {
                  "ClickHintsShortcut": {
                    "Modifiers": "Alt",
                    "VirtualKey": 72
                  }
                }
                """);

            var loaded = new SettingsStore(path).Load();

            Assert.AreEqual(HotkeyModifiers.Alt, loaded.MouseHintsShortcut.Modifiers);
            Assert.AreEqual(72, loaded.MouseHintsShortcut.VirtualKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
