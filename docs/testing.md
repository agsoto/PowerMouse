# Testing

## Automated tests

Run on Windows:

```powershell
dotnet test tests/PowerMouse.Core.Tests/PowerMouse.Core.Tests.csproj
dotnet test tests/PowerMouse.ClickHints.Tests/PowerMouse.ClickHints.Tests.csproj
dotnet test tests/PowerMouse.Windows.Tests/PowerMouse.Windows.Tests.csproj
```

The build script also runs these suites. Native tests marked `InteractiveDesktop` move the pointer and require an unlocked desktop; hosted CI excludes them.

## Desktop checks

Use a disposable profile for settings changes. Test the affected behavior on the Windows versions and architectures you intend to support.

### Installation and settings

- Launch with a clean account, then open a second copy; only one instance should remain.
- Close and reopen settings from the tray. Test every section's expand/collapse behavior with mouse and keyboard.
- Add, duplicate, rename, switch, and delete profiles. Confirm settings persist after restart.
- Update with `install-dev.ps1`; settings and the startup path should survive. Check the `PowerMouse.previous` backup.
- Uninstall on a test account. Binaries, shortcuts, and startup entries should be removed; data remains unless `-PurgeData` is used.
- Verify light, dark, and contrast themes, increased text size, keyboard navigation, and Narrator labels and states.

### Startup and administrator mode

- Cancel an administrator restart; the original instance should remain usable.
- Approve it with the same account; one elevated instance should reopen settings. Test movement, clicks, and scrolling in an elevated app.
- With Always run as administrator off, exit and relaunch normally; the new instance should be unelevated.
- Enable both startup options, then sign out/in. One elevated instance should start without a prompt or settings window.
- Disable Always run as administrator; normal startup should replace the task. Disable Start with Windows; both startup registrations should be absent.
- With Always run as administrator on, test manual-launch approval and cancellation, plus `--dont-elevate`.
- Verify normal `--shutdown` handling. Close elevated instances from the tray when a normal updater cannot stop them.

### Input

- Test lock-key and shortcut activation, including held shortcut keys and profile changes.
- Test all directions with Num Lock on/off and Shift held; opposite directions should cancel.
- Check normal/alternate speeds, acceleration curves, diagonal normalization, and scrolling.
- Hold each mouse-button binding while moving. Disable, switch profile, and exit while dragging; no button should remain held.
- With another remapper active, toggle PowerMouse between key-down and key-up. Check shortcut capture and ordinary typing afterward.

### Overlays and Mouse Hints

- Check pointer tracking during physical movement, keyboard movement, hint jumps, and settings activity.
- Test display edges, mixed DPI, negative monitor coordinates, and display reconnection.
- The indicator may be covered by Start; confirm it returns after the panel closes.
- Check hints in Explorer, a browser, and apps with different UI frameworks. Include text-only menus and icon buttons.
- Test automatic click on/off, the action modifier, prefix filtering, and Backspace.
- Dismiss with Escape, mouse buttons, window switching, and the shortcut. Rapidly dismiss/reopen during discovery; stale labels must not return.
- Hold hint keys and type during discovery; captured input must not leak into the target app.
- Disable hints while open; the overlay should close, its shortcut should pass through, and dependent settings should be disabled.
- Check hint alignment at different scales and ensure overlays do not steal focus or appear in Alt+Tab.

### System transitions

After lock/unlock, sleep/resume, layout changes, and display reconnection, check activation, movement, buttons, scrolling, and hints for stuck input or stale overlays. Test elevated apps and remote sessions where relevant.
