# Manual test checklist

Run this checklist on both Windows 10 and Windows 11 before publishing a release.

## Installation and lifecycle

- Launch on a clean user account and confirm default settings are created.
- Start a second copy and confirm only one process remains active.
- Close the settings window, reopen it from the tray icon, and exit from the tray menu.
- Confirm normal and Start with Windows launches remain in the notification area without opening settings.
- Verify light, dark, high-contrast, 100%, 150%, and 200% display scaling.

## Activation

- Test Scroll Lock, Num Lock, and Caps Lock activation and confirm the keyboard LED/system state stays synchronized.
- Test a shortcut with Ctrl, Alt, Shift, and Windows modifier combinations.
- Hold the shortcut to confirm key repeat does not toggle repeatedly.
- Change profile while enabled and confirm the new activation method works.

## Movement and buttons

- Test every cardinal and diagonal binding with Num Lock on and off.
- Hold Shift while using numeric-keypad movement and verify bindings remain stable.
- Hold opposite directions and verify the corresponding axis cancels.
- Test alternate speed below and above the normal speed.
- Hold each mouse-button binding, move the pointer, and confirm drag behavior.
- Disable and exit while every mouse button is held; confirm no button remains stuck.
- Hold each wheel key and verify repeated scrolling.

## Mouse Hints

- Change the Mouse Hints shortcut, restart PowerMouse, and confirm the new shortcut persists and works globally.
- Open Win32, WinUI, WPF, Chromium/Electron, Office, and Java applications and confirm visible interactive controls receive labels.
- Test an application with a custom-rendered surface and confirm OCR labels visible text when UI Automation coverage is sparse.
- Enable automatic click, type complete one- and two-letter hint codes, and confirm the target receives one left click.
- Disable automatic click, select a hint, and confirm the pointer moves without clicking.
- In both automatic-click states, hold the configured action modifier while typing the final hint key and confirm the action is inverted.
- Type a prefix and confirm nonmatching hints disappear; press Backspace and confirm the full matching set returns.
- Press Escape, click each mouse button, switch foreground windows, and press the activation shortcut again; confirm each path immediately removes all labels.
- Activate and dismiss repeatedly while discovery is still running and confirm no stale overlay appears.
- Verify labels remain aligned at 100%, 150%, and 200% scaling across mixed-DPI monitors with negative coordinates.
- Confirm the overlay never activates, never appears in Alt+Tab, and only intercepts clicks directly on visible hint badges.

## System boundaries

- With another low-level remapping service active, press a bound key while PowerMouse is off, enable PowerMouse before releasing it, and confirm downstream remappings still work.
- Focus an application's active shortcut-capture control that consumes low-level keyboard events, confirm PowerMouse yields during capture, and verify movement resumes with no stuck input after capture closes.
- Toggle PowerMouse from the tray while using lock-key activation and confirm the synthesized lock-key transition does not trigger downstream shortcuts.
- Test after lock/unlock, sleep/resume, display reconnect, and keyboard-layout changes.
- Test standard desktop, packaged, elevated, Remote Desktop, and secure-desktop transitions.
- Confirm elevated targets are not controlled from an unelevated process and that a clear notification is shown after input failure.
- Exercise multiple monitors with mixed DPI and negative virtual-screen coordinates.

## Accessibility

- Complete all settings with keyboard only.
- Inspect control names and state announcements with Narrator.
- Confirm the interface remains legible with text scaling and contrast themes.
- Verify duplicate binding warnings are announced and identify the conflicting keys.
