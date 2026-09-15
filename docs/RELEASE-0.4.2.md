<p align="center">
  <img src="https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/pame-banner.svg" alt="Pame — Your PC. Your console." width="100%">
</p>

<p align="center">
  <strong><a href="https://github.com/LielZ/Pame/releases/download/v0.4.2/Pame-Setup-0.4.2-x64.exe">Download Pame 0.4.2 for Windows</a></strong>
  &nbsp; · &nbsp;
  <a href="https://github.com/LielZ/Pame/releases/download/v0.4.2/SHA256SUMS.txt">SHA-256 checksum</a>
  &nbsp; · &nbsp;
  <a href="https://github.com/LielZ/Pame/blob/main/docs/USER_GUIDE.md">User guide</a>
</p>

<p align="center"><sub>PUBLIC ALPHA &nbsp; / &nbsp; WINDOWS 10 2004+ &nbsp; / &nbsp; x64</sub></p>

## Pame now keeps itself up to date

Get new Pame releases without leaving your console interface. **Settings → Updates** checks GitHub, downloads the installer, verifies it and handles the restart for you.

![Pame Updates with automatic updates, alpha channel and controller-friendly actions](https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/updates.png)

- **Automatic by default.** New versions download in the background and install the next time Pame opens.
- **Ready when you are.** Choose **Restart & update** to install a downloaded release immediately. Pame closes cleanly and reopens after installation.
- **Your games come first.** Checks and downloads wait while Pame is launching or tracking a game. Starting a game cancels an active download. Installation is blocked during play.
- **Verified downloads.** The installer must match its release version, size and SHA-256 checksum, with matching GitHub asset digests.
- **Your setup stays yours.** The library, preferences, browser profile, sounds and Windows sign-in setting are preserved. The update does not force-close games, reboot the PC or trigger the optional service-access installer.
- **Choose your channel.** Keep public alpha updates enabled, switch to stable releases only, or turn automatic updates off and use manual controls.

## Your PC. Your console.

Pame brings your game library, stores, browser, media and system controls into a fullscreen interface designed for a controller. DualSense features include configurable idle disconnect, battery percentages and low-battery alerts, custom light colors and **Player 1 / 2 / 3 / 4** light indicators. Pame's idle timer is disabled during gameplay; available battery/lighting features depend on the controller.

![Pame home with featured game, recent games and store shortcuts, captured in 0.4.1](https://raw.githubusercontent.com/LielZ/Pame/main/docs/images/home.png)

## Install or update

**Already on 0.4.1 or earlier?** Run the **[0.4.2 installer](https://github.com/LielZ/Pame/releases/download/v0.4.2/Pame-Setup-0.4.2-x64.exe)** once to add the updater. Later compatible releases can update from inside Pame.

Requires Windows 10 version 2004 or newer, x64. The installer includes .NET and installs Microsoft WebView2 if needed. Main setup runs for your Windows account. Optional service access is separate and off by default.

This is an unsigned alpha. Interrupted downloads restart from the beginning; failed installations report their result without an automatic restart loop. A failed installer may require running setup again. See [updater details](https://github.com/LielZ/Pame/blob/main/docs/UPDATER.md) and [known limitations](https://github.com/LielZ/Pame/blob/main/KNOWN_LIMITATIONS.md).

**143 automated tests passed**, including 27 updater cases. Native UI checks cover controller focus, saved choices, blocked actions during game preparation and a fully reachable menu. [Validation](https://github.com/LielZ/Pame/blob/main/docs/VALIDATION-0.4.2.md).

**[Explore Pame](https://github.com/LielZ/Pame#readme)** · **[Full changelog](https://github.com/LielZ/Pame/blob/main/CHANGELOG.md)** · **[Report a problem](https://github.com/LielZ/Pame/issues)**
