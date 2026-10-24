# Changelog

## 1.0.0

First stable release for Windows 11 x64. Mouse Hints remains experimental.

### Added

- Configurable acceleration curves, diagonal-speed normalization, and scroll steps.
- Restart as administrator and optional elevated startup at sign-in.
- An independent Mouse Hints toggle and local detection diagnostics.
- Grouped settings, updated icons, and an Open logs folder action.
- Local install and uninstall scripts that preserve settings by default.

### Improved

- Pointer tracking and overlay rendering during movement and hint discovery.
- Hint detection for toolbars, menus, editable rows, and custom text controls.
- Retention of toolbar targets when a later UI Automation scan times out.
- Lock-key activation synchronization and input release across state changes.
- Hint cancellation, repeated activation, and stale-result handling.
- Single-instance commands and local diagnostic logging.

### Known limitations

- The pointer indicator can be covered by Start and other Windows shell panels.
- Mouse Hints coverage depends on the target app; OCR can label noninteractive text.
- Release archives are unsigned. Windows 10 and ARM64 builds are outside the stable support scope.
