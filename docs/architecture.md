# Architecture

## Projects

| Project | Responsibility |
|---|---|
| `PowerMouse.Core` | Settings, profiles, key normalization, activation, movement, and button state |
| `PowerMouse.ClickHints` | Detection contracts, target classification, merging, and hint codes |
| `PowerMouse.ClickHints.Windows` | UI Automation, OCR, and capture interfaces |
| `PowerMouse.App` | WinUI settings, hooks, input injection, capture, overlays, startup, tray, and logging |

`InputEngine.Process` converts keyboard transitions into mouse commands and a suppression decision without calling Windows APIs. `PowerMouseRuntime` connects it to the keyboard hook, movement timer, and `MouseInputService`.

`ClickHintsService` coordinates discovery, keyboard filtering, dismissal, and target activation. Providers return target data; `ClickHintsOverlayService` renders the labels. See [Mouse Hints detection](click-hints-detection.md).

## Input lifecycle

- Hook callbacks do bounded work and pass events through on failure.
- Injected keyboard events are ignored to prevent feedback loops.
- Disabling, changing profiles, and exiting release held buttons and movement state.
- Suppressed keys remain suppressed through key-up, even if activation changes. Forwarded key-downs retain their matching forwarded key-ups.
- Lock-key activation synchronizes after key-up with the committed Windows lock state. Synthetic lock-key events carry the hook-bypass tag.
- PowerMouse yields when another application's shortcut capture stops the hook chain.
- Numpad scan-code normalization keeps bindings consistent across Num Lock and Shift states.
- Mouse Hints uses the shared keyboard hook and removes its temporary mouse and foreground hooks on dismissal.

Closing the settings window hides it. Exit disposes command IPC, hints, the pointer indicator, tray icon, input runtime, and single-instance mutex.

## Settings and startup

Settings live at `%LOCALAPPDATA%\PowerMouse\settings.json`. Writes use a temporary file and atomic replacement. Invalid JSON is backed up before defaults are loaded.

`StartupService` uses the current user's Run entry for normal startup. Elevated startup uses a per-user scheduled task with an interactive logon trigger and highest privileges; it replaces the Run entry. No password is stored.

`AdministratorRestartService` starts an elevated replacement with a one-use readiness event. The original instance exits only after acknowledgement; the replacement waits for it to exit before starting input hooks. Cancellation or failed acknowledgement leaves the original running.

`SingleInstanceCommandService` forwards settings, shutdown, and hint-inspection commands through a current-user named pipe. Commands from a normal process may not reach an elevated instance.

Daily logs are grouped by version under `%LOCALAPPDATA%\PowerMouse\Logs`. Logging retains a bounded history and does not block startup if the directory is unavailable.

## Overlays

Both overlays render a complete off-screen GDI bitmap and present it with `UpdateLayeredWindow`. Do not mix this with `SetLayeredWindowAttributes` on the same window.

The pointer indicator has its own message-loop thread. A mouse hook coalesces position updates, and keyboard movement and hint jumps signal it directly. A 16 ms timer handles missed updates and external pointer warps. Placement is clamped to monitor bounds and adjusted for DPI.

The indicator is a composited window, so it can lag the hardware cursor or be covered by protected shell panels.

Hint sessions use generation IDs to discard stale detection results and queued actions. Dismissal monitoring starts before discovery, and captured keys remain suppressed through key-up. Provider cancellation is cooperative; an external UI Automation call can remain blocked after the caller times out.

## Settings UI

Direct children of `SettingsExpander.Items` must be `SettingsCard` controls because the default item style targets that type. Place status messages outside the item list.
