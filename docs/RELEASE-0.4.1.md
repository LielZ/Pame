<p align="center">
  <img src="https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/pame-banner.svg" alt="Pame — Your PC. Your console." width="100%">
</p>

<p align="center">
  <strong><a href="https://github.com/LielZ/Pame/releases/download/v0.4.1/Pame-Setup-0.4.1-x64.exe">Download Pame 0.4.1 for Windows</a></strong>
  &nbsp; · &nbsp;
  <a href="https://github.com/LielZ/Pame/releases/download/v0.4.1/SHA256SUMS.txt">SHA-256 checksum</a>
  &nbsp; · &nbsp;
  <a href="https://github.com/LielZ/Pame/blob/main/docs/USER_GUIDE.md">User guide</a>
</p>

<p align="center"><sub>PUBLIC ALPHA &nbsp; / &nbsp; WINDOWS 10 2004+ &nbsp; / &nbsp; x64</sub></p>

Your PC games, stores, media and system controls in a fullscreen interface made for a controller. Pame puts your library in one place, makes the transition into a game feel like a console, and brings you home when you're done.

**DualSense support goes beyond navigation:** configurable idle disconnect, battery percentages, custom light colors and on-controller **Player 1, 2, 3 and 4** indicators.

![Pame 0.4.1 home screen with a featured game, recently played games and store shortcuts](https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/home.png)

<p align="center"><sub>Captured directly from Pame 0.4.1. Games shown are not included.</sub></p>

## What's new in 0.4.1

- **Now Playing in the Quick menu.** Control Pame browser media and compatible Windows apps, with title, source, playback state, progress and supported transport controls.
- **Switch sources with the right stick.** Move left or right to choose a source. Square on PlayStation, X on Xbox or Y on Switch toggles playback.
- **Keep your media playing.** Pressing Play in the Quick menu resumes a hidden browser source, including while Pame is minimized during a game.
- **Return to a clean screen.** Background recovery keeps reopened app windows hidden, including delayed secondary windows, while their processes remain alive.
- **The project is one click away.** Settings → Pame on GitHub opens the repository, releases and feedback inside Pame browser.

This release also includes the embedded browser with tabs and favorites, controller connection and battery overlays, and hold-to-drag mouse control introduced in 0.4.0.

## DualSense and controller control

- **Automatic idle disconnect, on your terms.** Choose a timeout or **Never** for supported Sony Bluetooth controllers. Pame's idle-disconnect timer is disabled during gameplay.
- **Battery percentages and charging status.** See available readings in Home, Controllers and the Quick menu. Low-battery alerts start at **15%** by default; choose your threshold in Settings.
- **Custom light colors.** Pick a DualSense light color and save a preference for each controller.
- **Player 1 / 2 / 3 / 4 lights.** Assign Pame player slots and show the corresponding DualSense player-light pattern on each controller. Connection and disconnection notices name the player.

Battery readings can be approximate, and available battery/lighting features depend on the connected controller. [Controller setup guide](https://github.com/LielZ/Pame/blob/main/docs/USER_GUIDE.md#dualsense-and-controller-management).

## A closer look

![Pame game details with artwork, play controls and game information](https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/game-details.png)

<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/stores.png" alt="Pame store launcher page" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/controllers.png" alt="Pame controller management page with four player slots" width="100%"></td>
  </tr>
  <tr>
    <td><strong>Your stores, together.</strong></td>
    <td><strong>DualSense battery, colors and Player 1–4 lights.</strong></td>
  </tr>
</table>

## Install or update

1. Download **[Pame-Setup-0.4.1-x64.exe](https://github.com/LielZ/Pame/releases/download/v0.4.1/Pame-Setup-0.4.1-x64.exe)** and run it.
2. Open Pame from the desktop or Start menu, then connect your controller.
3. Hold **PS / Guide / Home** to open the Quick menu.

Requires **Windows 10 version 2004 or newer, x64**. The installer includes .NET and installs WebView2 if needed. Existing users can install over their current version; library, settings and local browser data are preserved. Main setup runs for your Windows account. Optional Windows service access is off by default and requires an administrator confirmation during setup.

This is an **unsigned alpha release**. See the [known limitations](https://github.com/LielZ/Pame/blob/v0.4.1/KNOWN_LIMITATIONS.md) for current compatibility and constraints.

## Validation

**116 automated tests passed.** Native integration probes covered multiple browser tabs, an iframe, Windows media transport controls, background playback progression, source selection, window recovery and project-menu navigation. Read the [validation details](https://github.com/LielZ/Pame/blob/v0.4.1/docs/VALIDATION-0.4.1.md).

**[Explore Pame](https://github.com/LielZ/Pame#readme)** · **[Full changelog](https://github.com/LielZ/Pame/blob/v0.4.1/CHANGELOG.md)** · **[Report a problem](https://github.com/LielZ/Pame/issues)**
