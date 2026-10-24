using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.Core.Tests;

[TestClass]
public sealed class InputEngineTests
{
    [TestMethod]
    public void LockKeyPassesThroughWithoutInferringItsToggleState()
    {
        var engine = CreateEngine();

        var keyDown = engine.Process(Down(VirtualKeys.ScrollLock));
        var keyUp = engine.Process(Up(VirtualKeys.ScrollLock));

        Assert.IsFalse(engine.IsEnabled);
        Assert.IsFalse(keyDown.Suppress);
        Assert.IsFalse(keyUp.Suppress);
    }

    [TestMethod]
    public void ShortcutTogglesAndSuppressesFinalKey()
    {
        var profile = new PowerMouseProfile
        {
            ActivationMode = ActivationMode.Shortcut,
            ActivationShortcut = new HotkeyGesture
            {
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
                VirtualKey = VirtualKeys.F10,
            },
        };
        var engine = new InputEngine(profile);

        engine.Process(Down(VirtualKeys.LeftControl));
        engine.Process(Down(VirtualKeys.LeftMenu));
        var activation = engine.Process(Down(VirtualKeys.F10));

        Assert.IsTrue(engine.IsEnabled);
        Assert.IsTrue(activation.Suppress);
        Assert.IsTrue(engine.Process(Up(VirtualKeys.F10)).Suppress);
    }

    [TestMethod]
    public void HeldDirectionProducesMovementUntilReleased()
    {
        var engine = CreateEngine(enabled: true);

        Assert.IsTrue(engine.Process(Down(VirtualKeys.Numpad6)).Suppress);
        Assert.AreEqual(new MovementVector(20, 0), engine.GetMovement());

        Assert.IsTrue(engine.Process(Up(VirtualKeys.Numpad6)).Suppress);
        Assert.IsTrue(engine.GetMovement().IsZero);
    }

    [TestMethod]
    public void ForwardedKeyDownKeepsItsKeyUpForwardedWhenEnabledMidPress()
    {
        var engine = CreateEngine();

        var keyDown = engine.Process(Down(VirtualKeys.Numpad8));
        engine.SetEnabled(true);
        var keyUp = engine.Process(Up(VirtualKeys.Numpad8));

        Assert.IsFalse(keyDown.Suppress);
        Assert.IsTrue(engine.IsEnabled);
        Assert.IsFalse(keyUp.Suppress);
        Assert.AreEqual(0, keyUp.Commands.Count);
    }

    [TestMethod]
    public void DiagonalMovementPreservesFullSpeedOnBothAxes()
    {
        var engine = CreateEngine(enabled: true);

        engine.Process(Down(VirtualKeys.Numpad7));

        Assert.AreEqual(new MovementVector(-20, -20), engine.GetMovement());
    }

    [TestMethod]
    public void DiagonalMovementCanBeNormalized()
    {
        var profile = new PowerMouseProfile { NormalizeDiagonalMovement = true };
        var engine = new InputEngine(profile, enabled: true);

        engine.Process(Down(VirtualKeys.Numpad7));

        Assert.AreEqual(new MovementVector(-14, -14), engine.GetMovement());
    }

    [TestMethod]
    public void LinearAccelerationRampsFromInitialToConfiguredSpeed()
    {
        var clock = new ManualTimeProvider();
        var profile = new PowerMouseProfile
        {
            MovementSpeed = 20,
            AccelerationCurve = AccelerationCurve.Linear,
            AccelerationDurationMilliseconds = 1000,
            InitialSpeedPercent = 25,
        };
        var engine = new InputEngine(profile, enabled: true, timeProvider: clock);

        engine.Process(Down(VirtualKeys.Numpad6));
        Assert.AreEqual(new MovementVector(5, 0), engine.GetMovement());

        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(new MovementVector(13, 0), engine.GetMovement());

        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.AreEqual(new MovementVector(20, 0), engine.GetMovement());
    }

    [TestMethod]
    public void AccelerationRestartsAfterMovementStops()
    {
        var clock = new ManualTimeProvider();
        var profile = new PowerMouseProfile
        {
            MovementSpeed = 20,
            AccelerationCurve = AccelerationCurve.Linear,
            AccelerationDurationMilliseconds = 1000,
            InitialSpeedPercent = 25,
        };
        var engine = new InputEngine(profile, enabled: true, timeProvider: clock);

        engine.Process(Down(VirtualKeys.Numpad6));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(new MovementVector(20, 0), engine.GetMovement());

        engine.Process(Up(VirtualKeys.Numpad6));
        engine.Process(Down(VirtualKeys.Numpad6));
        Assert.AreEqual(new MovementVector(5, 0), engine.GetMovement());
    }

    [TestMethod]
    public void OpposingDirectionsCancel()
    {
        var engine = CreateEngine(enabled: true);

        engine.Process(Down(VirtualKeys.Numpad4));
        engine.Process(Down(VirtualKeys.Numpad6));

        Assert.AreEqual(new MovementVector(0, 0), engine.GetMovement());
    }

    [TestMethod]
    public void AlternateSpeedKeyUsesAlternateSpeedAndPassesThrough()
    {
        var engine = CreateEngine(enabled: true);

        var modifierResult = engine.Process(Down(VirtualKeys.LeftShift));
        engine.Process(Down(VirtualKeys.Numpad8));

        Assert.IsFalse(modifierResult.Suppress);
        Assert.AreEqual(new MovementVector(0, -1), engine.GetMovement());
    }

    [TestMethod]
    public void MouseButtonCanBeHeldForDragAndIsReleasedOnKeyUp()
    {
        var engine = CreateEngine(enabled: true);

        var down = engine.Process(Down(VirtualKeys.Numpad0));
        var repeatedDown = engine.Process(Down(VirtualKeys.Numpad0));
        var up = engine.Process(Up(VirtualKeys.Numpad0));

        CollectionAssert.AreEqual(
            new[] { new MouseCommand(MouseCommandKind.LeftButtonDown) },
            down.Commands.ToArray());
        Assert.AreEqual(0, repeatedDown.Commands.Count);
        CollectionAssert.AreEqual(
            new[] { new MouseCommand(MouseCommandKind.LeftButtonUp) },
            up.Commands.ToArray());
    }

    [TestMethod]
    public void DisablingReleasesHeldMouseButtons()
    {
        var engine = CreateEngine(enabled: true);
        engine.Process(Down(VirtualKeys.NumpadEnter));

        var commands = engine.SetEnabled(false);

        CollectionAssert.AreEqual(
            new[] { new MouseCommand(MouseCommandKind.RightButtonUp) },
            commands.ToArray());
    }

    [TestMethod]
    public void CapturedKeyUpRemainsSuppressedWhenDisabledMidPress()
    {
        var engine = CreateEngine(enabled: true);
        var keyDown = engine.Process(Down(VirtualKeys.Numpad8));

        engine.SetEnabled(false);
        var keyUp = engine.Process(Up(VirtualKeys.Numpad8));

        Assert.IsTrue(keyDown.Suppress);
        Assert.IsTrue(keyUp.Suppress);
    }

    [TestMethod]
    public void ProfileChangeReleasesHeldButtonAndStillSuppressesItsKeyUp()
    {
        var engine = CreateEngine(enabled: true);
        engine.Process(Down(VirtualKeys.Numpad0));
        var replacement = new PowerMouseProfile();
        replacement.Bindings[InputAction.LeftButton] = VirtualKeys.None;

        var commands = engine.UpdateProfile(replacement);
        var keyUp = engine.Process(Up(VirtualKeys.Numpad0));

        CollectionAssert.AreEqual(
            new[] { new MouseCommand(MouseCommandKind.LeftButtonUp) },
            commands.ToArray());
        Assert.IsTrue(keyUp.Suppress);
    }

    [TestMethod]
    public void WheelRepeatsForRepeatedKeyDownEvents()
    {
        var engine = CreateEngine(enabled: true);

        var first = engine.Process(Down(VirtualKeys.Divide));
        var repeat = engine.Process(Down(VirtualKeys.Divide));

        Assert.AreEqual(120, first.Commands.Single().Data);
        Assert.AreEqual(120, repeat.Commands.Single().Data);
    }

    [TestMethod]
    public void WheelUsesConfiguredStep()
    {
        var profile = new PowerMouseProfile { ScrollStep = 240 };
        var engine = new InputEngine(profile, enabled: true);

        var result = engine.Process(Down(VirtualKeys.Divide));

        Assert.AreEqual(240, result.Commands.Single().Data);
    }

    [TestMethod]
    public void InjectedKeyboardInputIsIgnored()
    {
        var engine = CreateEngine(enabled: true);

        var result = engine.Process(new KeyboardStroke(VirtualKeys.Numpad8, 0, false, true, IsInjected: true));

        Assert.IsFalse(result.Suppress);
        Assert.IsTrue(engine.GetMovement().IsZero);
    }

    [TestMethod]
    public void NumericKeypadNavigationCodesNormalizeWithNumLockOff()
    {
        var stroke = new KeyboardStroke(VirtualKeys.Up, ScanCode: 0x48, IsExtended: false, IsKeyDown: true);

        Assert.AreEqual(VirtualKeys.Numpad8, KeyNormalizer.Normalize(stroke));
    }

    [TestMethod]
    public void PhysicalArrowKeysRemainNavigationKeys()
    {
        var stroke = new KeyboardStroke(VirtualKeys.Up, ScanCode: 0x48, IsExtended: true, IsKeyDown: true);

        Assert.AreEqual(VirtualKeys.Up, KeyNormalizer.Normalize(stroke));
    }

    private static InputEngine CreateEngine(bool enabled = false) => new(new PowerMouseProfile(), enabled);

    private static KeyboardStroke Down(int key) => new(key, 0, false, true);

    private static KeyboardStroke Up(int key) => new(key, 0, false, false);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
