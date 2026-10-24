using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMouse.App.Controls;
using PowerMouse.App.Diagnostics;
using PowerMouse.App.Services;
using PowerMouse.Core.Models;

namespace PowerMouse.App.Pages;

public sealed partial class SettingsPage : UserControl
{
    private static readonly IReadOnlyList<KeyOption> ShortcutKeys = VirtualKeyCatalog.BindableKeys
        .Where(option => option.Code != VirtualKeys.None && !VirtualKeyCatalog.IsModifier(option.Code))
        .ToArray();
    private static readonly IReadOnlyList<ModifierOption> MouseHintModifiers =
    [
        new(HotkeyModifiers.Shift, "Shift"),
        new(HotkeyModifiers.Control, "Ctrl"),
        new(HotkeyModifiers.Alt, "Alt"),
        new(HotkeyModifiers.Windows, "Win"),
    ];
    private static readonly IReadOnlyList<AccelerationCurveOption> AccelerationCurves =
    [
        new(AccelerationCurve.None, "Disabled"),
        new(AccelerationCurve.Linear, "Linear"),
        new(AccelerationCurve.EaseIn, "Ease in"),
        new(AccelerationCurve.Smooth, "Smooth"),
    ];

    private readonly PowerMouseController controller;
    private bool loading;

    public SettingsPage()
    {
        InitializeComponent();
        controller = App.CurrentApp.Controller;

        ActivationKeyCombo.ItemsSource = VirtualKeyCatalog.LockKeys;
        ShortcutKeyCombo.ItemsSource = ShortcutKeys;
        MouseHintsKeyCombo.ItemsSource = ShortcutKeys;
        MouseHintsActionModifierCombo.ItemsSource = MouseHintModifiers;
        AlternateSpeedKeyCombo.ItemsSource = VirtualKeyCatalog.BindableKeys;
        AccelerationCurveCombo.ItemsSource = AccelerationCurves;

        AddBindingRows();
        ReloadProfiles();
        ReloadSettings();
        if (AdministratorRestartService.IsAdministrator)
        {
            AdministratorCard.Description = "PowerMouse is running as administrator and can control elevated apps.";
            RestartAsAdministratorButton.Content = "Running as administrator";
            RestartAsAdministratorButton.IsEnabled = false;
            AlwaysAdministratorToggle.IsEnabled = true;
            AlwaysAdministratorCard.Description = "Use administrator permissions on future launches. Enable Start with Windows to also start elevated at sign-in.";
        }
    }

    private async void OnRestartAsAdministratorClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        RestartAsAdministratorButton.IsEnabled = false;
        AdministratorInfo.IsOpen = false;
        try
        {
            if (await App.CurrentApp.RestartAsAdministratorAsync())
            {
                return;
            }

            AdministratorInfo.Severity = InfoBarSeverity.Informational;
            AdministratorInfo.Title = "Restart canceled";
            AdministratorInfo.Message = "PowerMouse is still running with its current permissions.";
            AdministratorInfo.IsOpen = true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Logger.LogError("Could not restart PowerMouse as administrator.", exception);
            AdministratorInfo.Severity = InfoBarSeverity.Error;
            AdministratorInfo.Title = "Could not restart as administrator";
            AdministratorInfo.Message = exception.Message;
            AdministratorInfo.IsOpen = true;
        }
        RestartAsAdministratorButton.IsEnabled = true;
    }

    internal void SetEnabledStatus(bool enabled)
    {
        loading = true;
        EnabledToggle.IsOn = enabled;
        loading = false;
    }

    private PowerMouseProfile ActiveProfile => controller.Settings.ActiveProfile;

    private void AddBindingRows()
    {
        AddBindingRow(MovementBindingsExpander, InputAction.MoveUp, "Up", "Move the pointer upward.", "\uE74A");
        AddBindingRow(MovementBindingsExpander, InputAction.MoveDown, "Down", "Move the pointer downward.", "\uE74B");
        AddBindingRow(MovementBindingsExpander, InputAction.MoveLeft, "Left", "Move the pointer to the left.", "\uE76B");
        AddBindingRow(MovementBindingsExpander, InputAction.MoveRight, "Right", "Move the pointer to the right.", "\uE76C");
        AddBindingRow(MovementBindingsExpander, InputAction.MoveUpLeft, "Up-left", "Move diagonally upward and left.", "\u2196", useSymbolFont: true);
        AddBindingRow(MovementBindingsExpander, InputAction.MoveUpRight, "Up-right", "Move diagonally upward and right.", "\u2197", useSymbolFont: true);
        AddBindingRow(MovementBindingsExpander, InputAction.MoveDownLeft, "Down-left", "Move diagonally downward and left.", "\u2199", useSymbolFont: true);
        AddBindingRow(MovementBindingsExpander, InputAction.MoveDownRight, "Down-right", "Move diagonally downward and right.", "\u2198", useSymbolFont: true);

        AddBindingRow(ButtonBindingsExpander, InputAction.LeftButton, "Left button", "Press and hold for click-and-drag.", "\uE962");
        AddBindingRow(ButtonBindingsExpander, InputAction.RightButton, "Right button", "Press and hold for right-button dragging.", "\uE962");
        AddBindingRow(ButtonBindingsExpander, InputAction.MiddleButton, "Middle button", "Press and hold the middle mouse button.", "\uE962");
        AddBindingRow(ButtonBindingsExpander, InputAction.ScrollUp, "Scroll up", "Scroll one wheel step upward.", "\uE70E");
        AddBindingRow(ButtonBindingsExpander, InputAction.ScrollDown, "Scroll down", "Scroll one wheel step downward.", "\uE70D");
    }

    private void AddBindingRow(
        SettingsExpander expander,
        InputAction action,
        string header,
        string description,
        string iconGlyph,
        bool useSymbolFont = false)
    {
        var comboBox = new ComboBox
        {
            Width = 230,
            ItemsSource = VirtualKeyCatalog.BindableKeys,
            DisplayMemberPath = nameof(KeyOption.Name),
            SelectedValuePath = nameof(KeyOption.Code),
            Tag = action,
        };
        comboBox.SelectionChanged += OnBindingChanged;

        var card = new SettingsCard
        {
            Header = header,
            Description = description,
            HeaderIcon = new FontIcon
            {
                Glyph = iconGlyph,
                FontFamily = useSymbolFont ? new FontFamily("Segoe UI Symbol") : new FontFamily("Segoe Fluent Icons"),
            },
            Content = comboBox,
            Tag = action,
        };
        var insertionIndex = ReferenceEquals(expander, ButtonBindingsExpander)
            ? Math.Max(0, expander.Items.Count - 1)
            : expander.Items.Count;
        expander.Items.Insert(insertionIndex, card);
    }

    private void ReloadProfiles()
    {
        loading = true;
        ProfileCombo.ItemsSource = null;
        ProfileCombo.ItemsSource = controller.Settings.Profiles;
        ProfileCombo.SelectedItem = controller.Settings.ActiveProfile;
        DeleteProfileButton.IsEnabled = controller.Settings.Profiles.Count > 1;
        loading = false;
    }

    private void ReloadSettings()
    {
        loading = true;
        var settings = controller.Settings;
        var profile = settings.ActiveProfile;

        EnabledToggle.IsOn = controller.IsEnabled;
        ActivationModeCombo.SelectedIndex = (int)profile.ActivationMode;
        ActivationKeyCombo.SelectedValue = profile.ActivationKey;
        ShortcutControl.IsChecked = profile.ActivationShortcut.Modifiers.HasFlag(HotkeyModifiers.Control);
        ShortcutAlt.IsChecked = profile.ActivationShortcut.Modifiers.HasFlag(HotkeyModifiers.Alt);
        ShortcutShift.IsChecked = profile.ActivationShortcut.Modifiers.HasFlag(HotkeyModifiers.Shift);
        ShortcutWindows.IsChecked = profile.ActivationShortcut.Modifiers.HasFlag(HotkeyModifiers.Windows);
        ShortcutKeyCombo.SelectedValue = profile.ActivationShortcut.VirtualKey;
        MouseHintsEnabledToggle.IsOn = settings.MouseHintsEnabled;
        MouseHintsSettingsPanel.IsEnabled = settings.MouseHintsEnabled;
        MouseHintsControl.IsChecked = settings.MouseHintsShortcut.Modifiers.HasFlag(HotkeyModifiers.Control);
        MouseHintsAlt.IsChecked = settings.MouseHintsShortcut.Modifiers.HasFlag(HotkeyModifiers.Alt);
        MouseHintsShift.IsChecked = settings.MouseHintsShortcut.Modifiers.HasFlag(HotkeyModifiers.Shift);
        MouseHintsWindows.IsChecked = settings.MouseHintsShortcut.Modifiers.HasFlag(HotkeyModifiers.Windows);
        MouseHintsKeyCombo.SelectedValue = settings.MouseHintsShortcut.VirtualKey;
        MouseHintsAutoClickCheckBox.IsChecked = settings.MouseHintsAutoClick;
        MouseHintsActionModifierCombo.SelectedValue = settings.MouseHintsActionModifier;
        MovementSpeedBox.Value = profile.MovementSpeed;
        AlternateSpeedKeyCombo.SelectedValue = profile.AlternateSpeedKey;
        AlternateSpeedBox.Value = profile.AlternateSpeed;
        AccelerationCurveCombo.SelectedValue = profile.AccelerationCurve;
        AccelerationDurationBox.Value = profile.AccelerationDurationMilliseconds;
        InitialSpeedPercentBox.Value = profile.InitialSpeedPercent;
        NormalizeDiagonalToggle.IsOn = profile.NormalizeDiagonalMovement;
        ScrollStepBox.Value = profile.ScrollStep;

        StartWithWindowsToggle.IsOn = settings.StartWithWindows;
        AlwaysAdministratorToggle.IsOn = settings.AlwaysRunAsAdministrator;
        EnabledOnStartupToggle.IsOn = settings.EnabledOnStartup;
        PointerIndicatorToggle.IsOn = settings.ShowPointerIndicator;
        NotificationsToggle.IsOn = settings.ShowNotifications;

        foreach (var card in MovementBindingsExpander.Items.Concat(ButtonBindingsExpander.Items).OfType<SettingsCard>())
        {
            if (card.Tag is InputAction action && card.Content is ComboBox comboBox)
            {
                comboBox.SelectedValue = profile.Bindings[action];
            }
        }

        UpdateActivationVisibility();
        UpdateAccelerationState();
        loading = false;
        ValidateBindings();
    }

    private void OnEnabledToggled(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading)
        {
            controller.SetEnabled(EnabledToggle.IsOn);
        }
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (loading || ProfileCombo.SelectedItem is not PowerMouseProfile profile)
        {
            return;
        }

        controller.Settings.ActiveProfileId = profile.Id;
        controller.SaveProfileSettings();
        ReloadSettings();
    }

    private void OnAddProfileClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        var profile = new PowerMouseProfile { Name = GetUniqueProfileName("New profile") };
        controller.Settings.Profiles.Add(profile);
        controller.Settings.ActiveProfileId = profile.Id;
        controller.SaveProfileSettings();
        ReloadProfiles();
        ReloadSettings();
    }

    private void OnDuplicateProfileClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        var profile = ActiveProfile.Clone(GetUniqueProfileName($"{ActiveProfile.Name} copy"));
        controller.Settings.Profiles.Add(profile);
        controller.Settings.ActiveProfileId = profile.Id;
        controller.SaveProfileSettings();
        ReloadProfiles();
        ReloadSettings();
    }

    private async void OnRenameProfileClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        var textBox = new TextBox
        {
            Text = ActiveProfile.Name,
            SelectionStart = 0,
            SelectionLength = ActiveProfile.Name.Length,
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename profile",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            ActiveProfile.Name = textBox.Text.Trim();
            controller.SaveProfileSettings();
            ReloadProfiles();
        }
    }

    private async void OnDeleteProfileClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (controller.Settings.Profiles.Count <= 1)
        {
            return;
        }

        var profile = ActiveProfile;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Delete {profile.Name}?",
            Content = "This profile and all its key bindings will be removed.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            controller.Settings.Profiles.Remove(profile);
            controller.Settings.ActiveProfileId = controller.Settings.Profiles[0].Id;
            controller.SaveProfileSettings();
            ReloadProfiles();
            ReloadSettings();
        }
    }

    private void OnActivationModeChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (loading || ActivationModeCombo.SelectedIndex < 0)
        {
            return;
        }

        ActiveProfile.ActivationMode = (ActivationMode)ActivationModeCombo.SelectedIndex;
        UpdateActivationVisibility();
        SaveProfile();
    }

    private void OnActivationKeyChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading && ActivationKeyCombo.SelectedValue is int key)
        {
            ActiveProfile.ActivationKey = key;
            SaveProfile();
        }
    }

    private void OnShortcutChanged(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        SaveShortcut();
    }

    private void OnShortcutChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        SaveShortcut();
    }

    private void SaveShortcut()
    {
        if (loading || ShortcutKeyCombo.SelectedValue is not int key)
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (ShortcutControl.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (ShortcutAlt.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (ShortcutShift.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (ShortcutWindows.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        ActiveProfile.ActivationShortcut = new HotkeyGesture { Modifiers = modifiers, VirtualKey = key };
        SaveProfile();
    }

    private void OnBindingChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = args;
        if (loading || sender is not ComboBox { Tag: InputAction action, SelectedValue: int key })
        {
            return;
        }

        ActiveProfile.Bindings[action] = key;
        SaveProfile();
    }

    private void OnMouseHintsShortcutChanged(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        SaveMouseHintsShortcut();
    }

    private void OnMouseHintsEnabledToggled(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (loading)
        {
            return;
        }

        controller.Settings.MouseHintsEnabled = MouseHintsEnabledToggle.IsOn;
        MouseHintsSettingsPanel.IsEnabled = MouseHintsEnabledToggle.IsOn;
        controller.SaveGeneralSettings();
        ValidateBindings();
    }

    private void OnMouseHintsShortcutChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        SaveMouseHintsShortcut();
    }

    private void SaveMouseHintsShortcut()
    {
        if (loading || MouseHintsKeyCombo.SelectedValue is not int key)
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (MouseHintsControl.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (MouseHintsAlt.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (MouseHintsShift.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (MouseHintsWindows.IsChecked == true)
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        controller.Settings.MouseHintsShortcut = new HotkeyGesture { Modifiers = modifiers, VirtualKey = key };
        controller.SaveGeneralSettings();
        ValidateBindings();
    }

    private void OnMouseHintsAutoClickChanged(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading)
        {
            controller.Settings.MouseHintsAutoClick = MouseHintsAutoClickCheckBox.IsChecked == true;
            controller.SaveGeneralSettings();
        }
    }

    private void OnMouseHintsActionModifierChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading && MouseHintsActionModifierCombo.SelectedValue is HotkeyModifiers modifier)
        {
            controller.Settings.MouseHintsActionModifier = modifier;
            controller.SaveGeneralSettings();
        }
    }

    private void OnMovementSpeedChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _ = sender;
        if (!loading && !double.IsNaN(args.NewValue))
        {
            ActiveProfile.MovementSpeed = checked((int)args.NewValue);
            SaveProfile();
        }
    }

    private void OnAlternateSpeedKeyChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading && AlternateSpeedKeyCombo.SelectedValue is int key)
        {
            ActiveProfile.AlternateSpeedKey = key;
            SaveProfile();
        }
    }

    private void OnAlternateSpeedChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _ = sender;
        if (!loading && !double.IsNaN(args.NewValue))
        {
            ActiveProfile.AlternateSpeed = checked((int)args.NewValue);
            SaveProfile();
        }
    }

    private void OnAccelerationCurveChanged(object sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading && AccelerationCurveCombo.SelectedValue is AccelerationCurve curve)
        {
            ActiveProfile.AccelerationCurve = curve;
            UpdateAccelerationState();
            SaveProfile();
        }
    }

    private void OnAccelerationDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _ = sender;
        if (!loading && !double.IsNaN(args.NewValue))
        {
            ActiveProfile.AccelerationDurationMilliseconds = checked((int)args.NewValue);
            SaveProfile();
        }
    }

    private void OnInitialSpeedPercentChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _ = sender;
        if (!loading && !double.IsNaN(args.NewValue))
        {
            ActiveProfile.InitialSpeedPercent = checked((int)args.NewValue);
            SaveProfile();
        }
    }

    private void OnNormalizeDiagonalToggled(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (!loading)
        {
            ActiveProfile.NormalizeDiagonalMovement = NormalizeDiagonalToggle.IsOn;
            SaveProfile();
        }
    }

    private void OnScrollStepChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _ = sender;
        if (!loading && !double.IsNaN(args.NewValue))
        {
            ActiveProfile.ScrollStep = checked((int)args.NewValue);
            SaveProfile();
        }
    }

    private void OnStartupToggleChanged(object sender, RoutedEventArgs args)
    {
        if (loading || controller is null) return;
        AdministratorInfo.IsOpen = false;
        try
        {
            controller.SaveStartupSettings(StartWithWindowsToggle.IsOn, AlwaysAdministratorToggle.IsOn);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            Logger.LogError("Could not change startup preferences.", exception);
            loading = true;
            StartWithWindowsToggle.IsOn = controller.Settings.StartWithWindows;
            AlwaysAdministratorToggle.IsOn = controller.Settings.AlwaysRunAsAdministrator;
            loading = false;
            AdministratorInfo.Severity = InfoBarSeverity.Error;
            AdministratorInfo.Title = "Could not change startup preferences";
            AdministratorInfo.Message = exception.Message;
            AdministratorInfo.IsOpen = true;
        }
    }

    private void OnGeneralToggleChanged(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (loading)
        {
            return;
        }

        var settings = controller.Settings;
        settings.EnabledOnStartup = EnabledOnStartupToggle.IsOn;
        settings.ShowPointerIndicator = PointerIndicatorToggle.IsOn;
        settings.ShowNotifications = NotificationsToggle.IsOn;
        controller.SaveGeneralSettings();
    }

    private void SaveProfile()
    {
        controller.SaveProfileSettings();
        ValidateBindings();
    }

    private void UpdateActivationVisibility()
    {
        var usesLockKey = ActiveProfile.ActivationMode == ActivationMode.LockKey;
        ActivationKeyCard.Visibility = usesLockKey ? Visibility.Visible : Visibility.Collapsed;
        ActivationShortcutCard.Visibility = usesLockKey ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateAccelerationState()
    {
        var isEnabled = ActiveProfile.AccelerationCurve != AccelerationCurve.None;
        AccelerationDurationCard.IsEnabled = isEnabled;
        InitialSpeedCard.IsEnabled = isEnabled;
    }

    private void ValidateBindings()
    {
        var profile = ActiveProfile;
        var duplicateGroups = profile.Bindings
            .Where(binding => binding.Value != VirtualKeys.None)
            .GroupBy(binding => binding.Value)
            .Where(group => group.Count() > 1)
            .Select(group => VirtualKeyCatalog.GetName(group.Key))
            .ToList();

        if (profile.AlternateSpeedKey != VirtualKeys.None && profile.Bindings.ContainsValue(profile.AlternateSpeedKey))
        {
            duplicateGroups.Add(VirtualKeyCatalog.GetName(profile.AlternateSpeedKey));
        }

        if (profile.ActivationMode == ActivationMode.LockKey && profile.Bindings.ContainsValue(profile.ActivationKey))
        {
            duplicateGroups.Add(VirtualKeyCatalog.GetName(profile.ActivationKey));
        }

        if (profile.ActivationMode == ActivationMode.Shortcut &&
            profile.Bindings.ContainsValue(profile.ActivationShortcut.VirtualKey))
        {
            duplicateGroups.Add(VirtualKeyCatalog.GetName(profile.ActivationShortcut.VirtualKey));
        }

        if (controller.Settings.MouseHintsEnabled &&
            profile.ActivationMode == ActivationMode.Shortcut &&
            profile.ActivationShortcut == controller.Settings.MouseHintsShortcut)
        {
            duplicateGroups.Add(controller.Settings.MouseHintsShortcut.ToString());
        }

        duplicateGroups = duplicateGroups.Distinct(StringComparer.Ordinal).ToList();
        ValidationInfo.IsOpen = duplicateGroups.Count > 0;
        ValidationInfo.Message = duplicateGroups.Count == 0
            ? string.Empty
            : $"Each key should have one purpose. Conflicts: {string.Join(", ", duplicateGroups)}.";
    }

    private string GetUniqueProfileName(string baseName)
    {
        var candidate = baseName;
        var suffix = 2;
        while (controller.Settings.Profiles.Any(profile => string.Equals(profile.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    private sealed record ModifierOption(HotkeyModifiers Modifier, string Name);

    private sealed record AccelerationCurveOption(AccelerationCurve Curve, string Name);
}
