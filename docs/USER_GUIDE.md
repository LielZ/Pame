# Pame user guide

## Start

- Download and run [Pame-Setup-0.4.2-x64.exe](https://github.com/LielZ/Pame/releases/download/v0.4.2/Pame-Setup-0.4.2-x64.exe) from [GitHub Releases](https://github.com/LielZ/Pame/releases). Requires Windows 10 version 2004 or later, x64. The installer includes the .NET runtime; no SDK is needed. If Microsoft WebView2 is missing, setup downloads its runtime for Pame browser.
- Open **Pame** from the desktop or Start menu. It opens fullscreen by default.
- Connect a controller over USB or pair one in **Controllers**. Sign into your stores once if they request authentication.
- **Settings → Launch at sign-in** enables startup for your Windows account.
- **Pame Recovery** in Start opens a window without controller polling. `Ctrl+Alt+Shift+Escape` exits Pame. Fullscreen console mode hides the taskbar and desktop icons. Exit Pame, use Pame Recovery, or open Desktop control to restore Windows. Explorer continues running.
- **Settings → Pame on GitHub** opens the repository, release downloads and feedback inside Pame browser.

The main installer runs for your Windows account. Optional Windows service access has a separate setup-time administrator confirmation and is off by default. The alpha installer is unsigned. Updates preserve your library and settings; download a newer installer from Releases and run it over the existing installation. A SHA-256 checksum file accompanies each published installer.

## Updates

**Settings → Updates** checks GitHub Releases, downloads and verifies the installer, and offers **Restart & update**. Automatic updates are on by default: new releases download when Pame is not running a game and install at the next Pame startup. Pame reopens after installation. Turn automatic updates off for manual control; disable public-alpha releases to use the stable channel. Version 0.4.1 and earlier need the 0.4.2 installer once to gain this feature. [Update behavior and recovery](UPDATER.md).

## Controls

| Controller | Keyboard | Action |
|---|---|---|
| D-pad / left stick | Arrow keys | Move focus |
| A / Cross | Enter | Select |
| B / Circle | Escape | Back |
| X / Square | X or F | Favorite selected game |
| Y / Triangle | Y or F2 | Onscreen search keyboard |
| LB / RB | Page Up / Page Down | Main pages |
| Hold PS / Guide / Home | Ctrl+Alt+P or F1 | Quick menu |
| Start + Back | — | Alternative quick-menu shortcut |
| — | F11 | Fullscreen / windowed |

Mouse and physical keyboard are also supported. Search can be typed using the onscreen keyboard or letter keys. Switch prompts use physical button positions: B selects, A goes back, Y favorites, X searches. Destructive actions open a confirmation with Cancel focused first.

**Settings → Look & sound** controls menu sounds, volume, button images and reduced motion. **Settings → Controller notifications** sets the low-battery threshold (15% by default). Connection notices identify the player.

**Quick menu → Desktop control**, and opening Windows/store settings from Pame, enables the left stick as a mouse and right stick for scrolling. Cross/A is left click; hold it to drag. Circle/B is right click. Square/X opens the Windows keyboard. Hold PS/Guide or use Start + Back to return directly to Pame.

**Browser** stays inside Pame's sidebar. Use the toolbar for tabs, back/forward, address/search, favorites and Mouse mode. In browser mouse mode, Square/X opens Pame's text keyboard, Triangle/Y opens address entry, LB/RB changes tabs, and holding PS/Guide returns to browser controls. The Quick menu opens the browser while a game is running; **Return to game** restores game focus. Hidden media pauses by default; change this in the browser options menu. Website sign-ins and subscriptions belong to the media provider.

## DualSense and controller management

Open **Controllers** to manage the four Pame player slots. Select a connected DualSense to change its player assignment or light color.

| Feature | Where to find it | Behavior |
| :--- | :--- | :--- |
| **Automatic idle disconnect** | Controllers → Idle disconnect | Choose **Never**, **5**, **7**, **10**, **15** or **30 minutes**. The default is 7 minutes. Applies to supported Sony Bluetooth controllers while browsing Pame; Pame disables its timer during gameplay. |
| **Battery percentage and charging** | Home, Controllers and Quick menu | Shows the controller's reported battery level and charging state. Sony readings are approximate and shown with a **~** in the detailed battery text. |
| **Low-battery alerts** | Settings → Controller notifications | Warns at **15% or below** by default. Select another threshold or turn battery alerts off. Connection and disconnection notifications identify the player. |
| **Light color** | Controllers → select controller → Light color | Choose a color and save it for that controller. Game profiles can temporarily apply a different controller color during play. |
| **Player 1 / 2 / 3 / 4** | Controllers → select controller → Change player | Changes the Pame slot and sends its player index to the controller, enabling the corresponding DualSense player-light pattern. Saved assignments are reused when available. |

Battery and lighting features depend on what the connected controller reports. Idle disconnect controls Pame's own Bluetooth-disconnect behavior; it does not repair unrelated Bluetooth signal loss, an empty battery or hardware power faults. Player slots identify controllers in Pame; individual games manage their own player assignments.

## Included

The **Quick menu media player** stays visible while you scroll. Move the **right stick left/right** to choose a media source; **Square on PlayStation / X on Xbox / Y on Switch** toggles that source's playback. Use the D-pad to reach play/pause, previous/next and ±10 seconds where the source supports them. Browser tabs/frames and compatible Windows apps appear independently. Paused sources remain available to resume. Pressing Play in the Quick menu lets that browser source play in the background until paused, closed or navigated away.

- Home, searchable/filterable game library, game detail pages, favorites and playtime sorting.
- Steam libraries on multiple drives, Epic manifests, GOG and Ubisoft registry adapters, XboxGames configuration files and conservative EA/Blizzard/Riot/Rockstar registry discovery.
- Standalone games can be explicitly selected with a controller-operated file browser.
- Local Steam artwork, remote Steam metadata with exact-title matching for other stores, an official Epic artwork entry for discovered Fortnite, and persistent offline caching.
- SQLite library, settings, per-game power/priority/affinity/audio/display/GPU/light preferences, play sessions and interrupted-session recovery.
- Native SDL3 gamepad input, four player slots, capability-aware rumble, battery and Sony player lights / RGB.
- Bluetooth discovery and pairing; explicit repair of a selected, disconnected paired controller; no broad device cleanup.
- CPU, GPU engine utilization, RAM and dedicated GPU memory; PresentMon adapter for game frame timing where ETW permissions permit it. A live dashboard adds history and supported GPU temperature/clock/capacity sensors.
- Quick menu with audio volume/output, connected controllers, game controls, screenshot, Wi-Fi, desktop controls, embedded browser and confirmed power actions.
- Storage management, reversible personal startup entries and individually confirmed optional app removal.
- Controller keyboard for new personal Wi-Fi networks. Windows stores the wireless credentials; Pame does not put them in its own database or logs.
- Optional power plan during play, with restoration and a persistent recovery journal. Settings > Background activity manages selected Copilot/Xbox/Widgets apps, OneDrive, browser/launcher priority and five optional Windows services. A master switch, individual choices, session results and crash recovery are included. Windows services are off by default. An optional installer checkbox provisions service access once; game launches never request administrator access.
- Per-user installer, uninstall entry, desktop/Start shortcuts and recovery shortcut.

## Local data

`%LOCALAPPDATA%\Pame\` holds `pame.db`, `artwork`, `logs` and startup recovery information. The installer preserves this directory on upgrades and uninstall. There is no telemetry, account requirement or cloud upload in Pame. Public metadata requests send a discovered game ID or title to the metadata provider. Store authentication stays inside the store.

## Develop

Requires Windows 10 2004 or later, x64, and .NET 10 SDK. Clone the repository and install the SDK; `scripts/build.ps1` uses `dotnet` from PATH or an optional local SDK at `.tools/dotnet`.

```powershell
git clone https://github.com/LielZ/Pame.git
cd Pame
dotnet build
dotnet test tests/Pame.Tests
dotnet run --project src/Pame.App -- --windowed
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Package
```

The packaging command also requires Inno Setup 6. SDK/bootstrap scripts and research repositories are excluded from the release. Native SDL and PresentMon binaries are pinned in `native/`; their source, licenses and checksums are recorded in [DEPENDENCIES.md](../DEPENDENCIES.md).

Developer modes: `--diagnose <output.json>` exports discovery, `--smoke-ui` renders the six pages and 1080p/1440p/4K references, `--fullscreen` restores the fullscreen preference, `--software-rendering` selects WPF software rendering, and `--safe-mode` opens recovery mode. `PAME_DATA_DIR` can isolate a test database. These modes do not invent library entries.

Graphics acceleration defaults to Automatic and follows the active display adapter. An unused virtual display driver no longer forces software rendering. Settings > Look & sound offers an explicit software compatibility mode, and Pame Recovery also uses software rendering. Text and focus rings remain vector-rendered; artwork is cached by source and decoded size.

Look & sound also selects locally installed sound packs under `%LOCALAPPDATA%\Pame\sounds\`; upgrades preserve these packs. The installer ships the original Pame sounds. The optional community PlayStation pack used during development is documented in [PLAYSTATION_SOUND_SOURCE.json](PLAYSTATION_SOUND_SOURCE.json) and is not bundled with the installer.

## Validation

See [docs/VALIDATION-0.4.1.md](VALIDATION-0.4.1.md) for this update, [docs/VALIDATION-0.4.0.md](VALIDATION-0.4.0.md) for the browser/controller update, [docs/VALIDATION-0.3.3.md](VALIDATION-0.3.3.md) for background app/service cycles and [docs/VALIDATION.md](VALIDATION.md) for the original baseline. Native integration probes exercise media controls, window recovery, pointer input, embedded navigation, notifications and game transitions. The prior Fortnite wallpaper/return fix was confirmed by the user; broader game and hardware coverage remains ongoing.

## Feedback and third-party components

Open an [issue](https://github.com/LielZ/Pame/issues) with your Pame version, Windows version, controller type, the affected store/game, and steps to reproduce. Remove personal data before sharing logs or screenshots.

Dependency licenses, asset origins and trademarks are listed in [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) and [DEPENDENCIES.md](../DEPENDENCIES.md). Store and controller brands belong to their owners; Pame is an independent project.
