# Contributing

Search existing issues before opening a report or proposal. For bugs, include reproduction steps, the Windows version, and relevant display, keyboard, and application details.

## Development

Install the .NET SDK specified in [global.json](global.json) on Windows, then run:

```powershell
./scripts/build.ps1 -Platform x64
```

The build script restores dependencies, runs tests, and publishes a self-contained app. See [Testing](docs/testing.md) for individual suites and desktop checks.

For local use:

```powershell
./scripts/install-dev.ps1
```

Run the same command to update. It preserves settings and keeps one previous installation for recovery. Close an elevated instance from the tray before updating.

Useful installer options:

| Option | Purpose |
|---|---|
| `-Configuration Debug` | Install a debug build |
| `-SkipTests` | Build without running tests |
| `-UsePublishedBuild` | Install the existing `artifacts/PowerMouse-<platform>` build |
| `-NoStart` | Install without launching |

## Code

- Keep input state and settings logic in `PowerMouse.Core`.
- Keep hint detection separate from rendering and input injection.
- Keep hook callbacks short and pass input through on failure.
- Release held inputs when disabling, switching profiles, or exiting.
- Avoid logging on movement and key-repeat paths.

The [architecture guide](docs/architecture.md) describes the project boundaries.

## Pull requests

Keep changes focused. Explain the behavior changed, include regression tests where appropriate, and note the automated and desktop checks you ran. Update documentation when controls or behavior change.

Do not commit build output, local settings, logs, or diagnostics. Review attachments for private information.

Contributions are licensed under the [MIT License](LICENSE).
