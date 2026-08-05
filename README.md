# PowerMouse

PowerMouse is a modern Windows utility for controlling the mouse pointer with the keyboard. It stays available from the notification area and provides independent, profile-based bindings for pointer movement, mouse buttons, scrolling, activation, and alternate speed.

## MVP features

- Eight-direction pointer movement with independently assignable keys
- Left, right, and middle mouse buttons, including press-and-hold dragging
- Scroll-up and scroll-down bindings with keyboard-repeat support
- Normal speed plus a modifier-held alternate speed
- Scroll Lock, Num Lock, or Caps Lock activation
- Configurable global activation shortcut
- Mouse Hints for keyboard selection of controls in the active window
- Named profiles with add, duplicate, rename, delete, and automatic persistence
- Background startup with optional Start with Windows and activate-on-startup behavior
- Optional pointer-adjacent status indicator and status notifications
- Notification-area menu for opening, toggling, and exiting
- Light, dark, high-contrast, keyboard, and screen-reader-friendly WinUI interface

## Requirements

- Windows 10 version 2004 (build 19041) or newer
- x64 or ARM64 processor
- For development: Windows 10 or 11 and the .NET 10 SDK; Windows dependencies restore from NuGet

## Build

Open `PowerMouse.slnx` in Visual Studio, select `x64` or `ARM64`, and build. From PowerShell:

```powershell
./scripts/build.ps1 -Configuration Release -Platform x64
```

The self-contained unpackaged application is written to `artifacts/PowerMouse-x64`. Create a zip release with:

```powershell
./scripts/package.ps1 -Platform x64
```

Extract the complete archive, then run `PowerMouse.exe`. Keep the files together because the application carries its Windows runtime dependencies beside the executable.
These MVP archives are unsigned, so Windows may display an unknown-publisher warning.

## Default controls

| Action | Key |
|---|---|
| Activate or deactivate | Scroll Lock |
| Move | Numeric keypad 1–4 and 6–9 |
| Left button | Numeric keypad 0 |
| Right button | Numeric keypad Enter |
| Middle button | Numeric keypad 5 |
| Scroll up | Numeric keypad Divide |
| Scroll down | Numeric keypad Multiply |
| Alternate speed | Left Shift |
| Show Mouse Hints | Ctrl + Shift + Space |

All bindings and speeds can be changed per profile.

## Application behavior

PowerMouse starts in the notification area without opening its settings page. Click its notification-area icon to open settings, or right-click the icon to toggle emulation or exit. Closing settings keeps PowerMouse running. Settings are stored at `%LOCALAPPDATA%\PowerMouse\settings.json` and are saved automatically.

Mouse Hints uses UI Automation and an OCR fallback to label visible controls in the active window. Type a displayed code to move to its target and optionally click it. Hold the configured action modifier while typing the final key to invert the click setting. Press Escape, click elsewhere, switch windows, or press the shortcut again to dismiss the overlay.

Windows permits synthesized input only into applications running at the same or a lower integrity level. As a result, an unelevated PowerMouse process cannot control an elevated application. Some games and protected applications consume input through paths that do not accept standard Windows synthesized mouse input.

## Development

The solution separates the deterministic input state machine from Windows hooks, input injection, persistence, and UI. Core behavior can therefore be tested without installing a global hook:

```powershell
dotnet test tests/PowerMouse.Core.Tests/PowerMouse.Core.Tests.csproj
```

See [Architecture](docs/architecture.md) and the [manual test checklist](docs/manual-test-checklist.md) for implementation and release details.

The Mouse Hints semantic-first UI Automation strategy, OCR fallback, performance budgets, and reliability limits are described in [Mouse Hints detection research](docs/click-hints-detection.md).

## License

PowerMouse is licensed under the [MIT License](LICENSE).
