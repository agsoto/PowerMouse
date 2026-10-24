# Releasing PowerMouse

The release process follows [Timeline Timer](https://github.com/agsoto/timeline-timer/blob/main/RELEASING.md): build a draft GitHub release, verify its installers and checksums, publish it, then submit a separate WinGet pull request. PowerMouse uses an Inno Setup installer for its self-contained WinUI application. Stable releases target **Windows 11 x64** only.

## Prepare locally

1. Set `Version`, `AssemblyVersion`, and `FileVersion` in `Directory.Build.props` (for example `1.0.0`, `1.0.0.0`, and `1.0.0.0`). Add the matching section in `CHANGELOG.md` and update version examples in `README.md`.
2. Install the pinned compiler and prepare the release:

   ```powershell
   ./scripts/install-release-tools.ps1
   ./scripts/prepare-release.ps1
   ./scripts/test-release.ps1
   ```

   The compiler comes from the official Inno Setup 6.7.3 release, verified by SHA-256 and publisher signature, and installs under `.tools/inno-setup`. Alternatively pass `-CompilerPath` to `prepare-release.ps1`. WinGet must be installed to run `test-release.ps1`.

   Preparation runs all three test suites, publishes into a fresh directory, and writes these files under `artifacts/release/<version>/`:

   - `PowerMouse-<version>-win-x64-setup.exe` and its `.sha256` file.
   - `PowerMouse-<version>-win-x64.zip` and its `.sha256` file.
   - `SHA256SUMS.txt`, covering the installer and ZIP.
   - `winget/manifests/a/AGSoto/PowerMouse/<version>/`, containing three WinGet manifests generated from the actual installer hash.

   Native interactive tests move the pointer and require an unlocked desktop. Use `-SkipInteractiveTests` for CI. `-SkipTests` is for local packaging iteration only; release CI always runs the tests.

3. Test the installer in a disposable Windows 11 x64 account. The following command refuses to run if existing PowerMouse files, data, processes, shortcuts, or startup registrations are present:

   ```powershell
   ./scripts/test-installer.ps1 -InstallerPath ./artifacts/release/1.0.0/PowerMouse-1.0.0-win-x64-setup.exe -AllowCurrentUserChanges
   ```

   Run from an administrator PowerShell in that disposable account to test elevated task cleanup. It checks silent install, registered metadata, the shortcut, reinstall, startup cleanup, and settings preservation. It leaves logs in `installer-test/` and a settings sentinel in the disposable account. Also perform a real application launch and the relevant [desktop checks](docs/testing.md). The lifecycle script does not exercise the application's UI or simulate an upgrade from an older version.

## Create and publish the GitHub release

1. Review and commit the release changes, then push the default branch (`master` in this repository) and a matching tag:

   ```powershell
   git push origin master
   git tag v1.0.0
   git push origin v1.0.0
   ```

   Alternatively run the **Release** workflow manually from the default branch. It derives the tag from `Directory.Build.props` and targets the exact workflow commit. A tag/version mismatch or an existing tag pointing elsewhere stops the workflow.

2. Wait for **Build** and **Release** to pass. Release CI builds x64, validates manifests with WinGet, and tests installation/reinstallation/uninstallation in the disposable runner. ARM64 remains build-only in the regular workflow. Pull requests affecting packaging run the same release checks without creating a GitHub release.
3. Open the generated **draft**, review its notes, download its installer and ZIP, and verify them against `SHA256SUMS.txt`. Smoke-test the downloaded installer on Windows 11 x64. CI's Windows Server runner does not replace that desktop check.
4. Publish the draft. Release automation deliberately stops at draft creation. Reruns can replace draft assets but refuse to modify a published release. If a public binary needs a change, release a new version.

The workflow needs only the repository's normal `GITHUB_TOKEN` with `contents: write` in the draft job. No WinGet token or publishing secret is required. Packages are unsigned; Windows may display an unknown-publisher warning.

## Submit to WinGet after publication

The planned package ID is **AGSoto.PowerMouse**. A generated manifest is preparation, not catalog availability. Use the installer downloaded from the public release, not a separately rebuilt copy:

```powershell
winget search --exact --id AGSoto.PowerMouse --source winget
gh release download v1.0.0 --repo agsoto/PowerMouse --pattern 'PowerMouse-1.0.0-win-x64-setup.exe' --dir artifacts/winget-public/1.0.0
./scripts/new-winget-manifest.ps1 -InstallerPath ./artifacts/winget-public/1.0.0/PowerMouse-1.0.0-win-x64-setup.exe -OutputDirectory ./artifacts/winget-public -VerifyDownload
winget validate --manifest ./artifacts/winget-public/manifests/a/AGSoto/PowerMouse/1.0.0
```

`-VerifyDownload` requires the version-specific public URL to resolve and its downloaded bytes to match the installer. Review the metadata and check for an existing package/version or open submission before creating a duplicate.

In a disposable Windows 11 x64 account, enable local manifests from an administrator terminal and test the public installer through WinGet:

```powershell
winget settings --enable LocalManifestFiles
winget install --manifest ./artifacts/winget-public/manifests/a/AGSoto/PowerMouse/1.0.0 --silent
winget list --exact --id AGSoto.PowerMouse
winget uninstall --exact --id AGSoto.PowerMouse --silent
```

Exit PowerMouse from its tray before upgrading or uninstalling, including elevated instances. The installer uses the current user's `%LOCALAPPDATA%\Programs\PowerMouse`, creates a Start menu shortcut and Windows uninstall entry, and preserves `%LOCALAPPDATA%\PowerMouse` settings/logs. It removes only startup entries pointing at that installation. Startup cleanup errors stop uninstall before deleting application files. Keep the install path unchanged for existing elevated startup tasks.

Install Microsoft's manifest submission tool and submit the reviewed version directory:

```powershell
winget install --exact --id Microsoft.WingetCreate --source winget
wingetcreate submit --prtitle 'New package: AGSoto.PowerMouse version 1.0.0' ./artifacts/winget-public/manifests/a/AGSoto/PowerMouse/1.0.0
```

Authenticate when prompted; do not put tokens in repository files or command examples. The submission opens a pull request to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs). Availability starts only after its automated checks, moderator review, merge, and catalog ingestion. Once available, users can run `winget install --exact --id AGSoto.PowerMouse`.

References: [WinGet manifest documentation](https://learn.microsoft.com/windows/package-manager/package/manifest), [manifest submission](https://github.com/microsoft/winget-create/blob/main/doc/submit.md), [Inno Setup downloads](https://jrsoftware.org/isdl.php).
