# Updating Pame

Pame 0.4.2 adds **Settings → Updates**. Automatic updates are on by default: check GitHub Releases after startup, then every six hours while Pame is open; download an eligible release; apply it the next time Pame starts. Choose **Restart & update** to install a downloaded release immediately. Turn automatic updates off for manual checks, downloads and installation.

The public-alpha channel is on by default for this alpha release. Disable **Include public alpha releases** to accept only releases GitHub marks as stable. Checks use the published releases list, including complete prereleases when enabled; draft releases, older/equal versions and incomplete packages are ignored. No GitHub login or token is required.

## During play

Pame defers automatic checks/downloads while it is launching or tracking a game. Starting a game cancels an in-progress update download and removes its partial file; a later attempt downloads it again from the beginning. Installation is blocked during games and app-removal operations. Pame never terminates a game to install an update. The UI remains responsive during network activity and checksum verification.

## Installation and recovery

Updates live under `%LOCALAPPDATA%\Pame\updates`. Both release assets must have GitHub SHA-256 digests. The downloaded checksum file must match its own digest; the installer's size and digest must match both GitHub metadata and its entry in `SHA256SUMS.txt`. Only HTTPS GitHub release URLs for `LielZ/Pame` and GitHub's asset CDN redirects are accepted. A changed or incomplete installer cannot be marked ready.

A separate helper verifies the installer again, locks it against modification and waits for Pame and its recovery processes to exit. The installer runs for the current Windows user with no forced application closure or automatic reboot. It keeps the installation location, library, settings, browser profile, sounds and sign-in preference. Automatic updates do not reinstall or elevate the optional service-access add-on. An update mutex prevents the new Pame from opening midway through installation.

Pending state is consumed before handoff, preventing automatic retry loops after a failed installation. The helper records the result, releases its mutex and reopens Pame. A failure is reported in Updates, with an installer log in the update directory. Installer failures do not provide a full transactional rollback: a damaged installation may require running the published installer again. Safe mode skips automatic updates. In-app installation requires an installed copy; development builds can check and download.

SHA-256 checks protect download integrity; the release account remains the trust source. The alpha installer is unsigned. The optional Windows service-access component currently has a separate lifecycle and is not silently upgraded with elevated privileges.

## Publishing a compatible update

1. Increase the three-part version in `src/Pame.App/Pame.App.csproj` and `packaging/Pame.iss`, including `VersionInfoVersion`. Add release notes and validation.
2. Run `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Package`. This tests, packages and creates `dist/Pame-Setup-X.Y.Z-x64.exe` plus `dist/SHA256SUMS.txt`.
3. Commit and push the source, then run `powershell -ExecutionPolicy Bypass -File scripts/publish-release.ps1 -NotesFile docs/RELEASE-X.Y.Z.md -Preview` for a public alpha; omit `-Preview` for a stable release. This requires the maintainer's authenticated GitHub CLI.

The publishing script uploads both assets to a draft, compares GitHub's reported SHA-256 digests with the local files, then publishes. Existing releases are not overwritten. Release tags must use `vX.Y.Z`, with numeric version ordering. The feed considers the 100 most recent releases.

Protocol references: [GitHub Releases API](https://docs.github.com/en/rest/releases/releases), [Inno Setup command-line options](https://jrsoftware.org/ishelp/topic_setupcmdline.htm), [installer exit codes](https://jrsoftware.org/ishelp/topic_setupexitcodes.htm).
