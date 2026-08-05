# Architecture

PowerMouse is split into a platform-neutral core and a thin Windows application layer.

## Projects

### PowerMouse.Core

`PowerMouse.Core` owns the behavior that must remain deterministic and testable:

- profiles and JSON-safe settings models
- virtual-key catalog and numeric-keypad normalization
- activation shortcut state
- held movement and alternate-speed state
- mouse-button down/up pairing
- wheel commands and release-on-disable safety

`InputEngine.Process` consumes keyboard transitions and returns commands plus a suppression decision. It never calls an operating-system API.

### PowerMouse.App

`PowerMouse.App` owns Windows integration:

- `KeyboardHookService` installs `WH_KEYBOARD_LL` on a dedicated message-loop thread
- `MouseInputService` emits batched `SendInput` events
- `PowerMouseRuntime` connects the hook and engine and runs the movement timer
- `TrayIconService` provides notification-area lifecycle controls
- `CursorIndicatorService` owns the small non-activating pointer indicator window
- `ClickHintsService` coordinates discovery, keyboard filtering, dismissal, and point-click activation
- `ClickHintsOverlayService` paints a click-through Fluent label surface across the virtual desktop
- `StartupService` manages the current user's Run registration
- `PowerMouseController` coordinates settings, runtime, and error reporting
- WinUI pages render and edit the current settings

### PowerMouse.ClickHints

`PowerMouse.ClickHints` is a platform-neutral detection pipeline for keyboard-addressable screen targets. It owns:

- immutable element bounds, source, confidence, and interaction models
- staged semantic-first detection with an optional OCR fallback
- target clipping, de-duplication, ordering, and result limits
- UI Automation snapshot classification without a dependency on Windows assemblies
- deterministic, prefix-free hint-code assignment

Detection providers return data rather than drawing overlays or injecting input. This keeps discovery independently testable and allows multiple consumers to choose their own presentation and activation behavior.

### PowerMouse.ClickHints.Windows

`PowerMouse.ClickHints.Windows` adapts Windows UI Automation into the platform-neutral detector. It scopes discovery to a supplied HWND, requests the UIA control view, bulk-caches only classification properties, and releases live automation references before returning. It also adapts the broadly available Windows OCR engine to screen-coordinate text regions while leaving image acquisition behind `IWindowImageCapture`.

OCR is an optional adapter behind `IOcrTextRecognizer`. See [Mouse Hints detection research](click-hints-detection.md) for the source ordering, performance rationale, and known coverage limits.

The low-level hook callback does bounded work and fails open: an unexpected exception passes the keyboard event to the next hook instead of blocking input.

## State and shutdown rules

- Injected keyboard events are ignored to avoid feedback loops.
- Every suppressed mouse-button key-down has a matching synthesized button-up.
- Disabling, changing profiles, or exiting releases held buttons and movement state.
- Keys captured while enabled remain suppressed through their key-up transition, even if activation changes in between.
- Keys forwarded before activation keep their matching key-up forwarded, preventing stale held-key state in downstream hooks.
- Synthetic lock-key events carry the centralized-hook bypass tag so they cannot retrigger cooperating keyboard services.
- If a newer application intentionally captures keyboard input and ends the low-level hook chain, PowerMouse yields until that capture ends instead of reordering global hooks.
- Numeric-keypad scan codes are normalized so bindings keep working across Num Lock and Shift states.
- Mouse Hints shares the centralized keyboard hook, suppresses each captured hint key through key-up, and removes its temporary mouse and foreground hooks whenever the overlay closes.
- Escape, a mouse button, a foreground-window change, the activation shortcut, or a completed hint sequence closes the Mouse Hints overlay.
- Closing the window hides it; explicit Exit tears down the overlay, tray icon, hook, movement timer, and mutex in that order.

## Settings

Settings are stored as versioned JSON under `%LOCALAPPDATA%\PowerMouse`. Writes go to a process-specific temporary file and are moved into place atomically. Malformed JSON is preserved with an `invalid-<timestamp>` suffix before defaults are loaded.
