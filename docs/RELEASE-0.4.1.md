# Pame 0.4.1 · Public alpha

Pame turns a Windows gaming PC into a controller-friendly console experience: a fullscreen game library, store launching, game artwork during transitions, automatic return after play, an embedded browser and a Quick menu for system and media controls.

## Download and install

- **[Download Pame-Setup-0.4.1-x64.exe](https://github.com/LielZ/Pame/releases/download/v0.4.1/Pame-Setup-0.4.1-x64.exe)**
- [SHA-256 checksums](https://github.com/LielZ/Pame/releases/download/v0.4.1/SHA256SUMS.txt)
- [Source and setup guide](https://github.com/LielZ/Pame#readme)

Windows 10 version 2004 or newer, x64. The installer includes .NET and installs WebView2 if needed. Existing Pame users can run this installer over their current version; library, settings and local browser data are preserved. Main setup runs for your user account. Optional Windows service access requires an administrator confirmation during setup and is off by default.

This is an unsigned alpha release. Read the [known limitations](https://github.com/LielZ/Pame/blob/v0.4.1/KNOWN_LIMITATIONS.md) before using it as your main gaming interface.

## In this release

- Quick menu Now Playing card for Pame browser media and compatible Windows apps, with title, source, playback state, progress and supported controls.
- Right stick left/right selects a media source. Square on PlayStation, X on Xbox or Y on Switch plays/pauses that source.
- Explicit Play in the Quick menu resumes a hidden browser source, including while Pame is minimized during a game.
- Background recovery hides reopened app windows, including delayed secondary windows, while keeping their processes alive.
- Settings → Pame on GitHub opens the project, release downloads and feedback inside Pame browser.
- Includes the embedded browser with tabs/favorites, controller connection/battery overlays and hold-to-drag pointer control from 0.4.0.

116 automated tests pass. Native integration probes cover multiple browser tabs, an iframe, Windows media transport controls, actual background playback progression, source selection, window recovery and project-menu navigation. See [validation details](https://github.com/LielZ/Pame/blob/v0.4.1/docs/VALIDATION-0.4.1.md).

**בעברית:** גרסת אלפא ציבורית של Pame — חוויית קונסולה למחשב Windows. ההתקנה זמינה בקישור למעלה. נוסף נגן ב־Quick menu עם מעבר בין מקורות בג'ויסטיק הימני; יישומים שהוחזרו לאחר משחק נשארים ברקע עם חלונות מוסתרים; וקישורי GitHub והורדות נפתחים בדפדפן המובנה.

[Report a problem or suggest a feature](https://github.com/LielZ/Pame/issues).
