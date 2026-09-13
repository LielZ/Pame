# Pame 0.3 validation

## 0.3.1 branding and rapid navigation update

The supplied wordmark is displayed in the sidebar and Quick menu, using a cropped WPF view of the original PNG's transparent canvas. Both supplied PNG files remain byte-identical to the originals. The app icon is encoded at ten Windows ICO sizes (16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixels) and used by the executable and installer. See `docs/BRAND_ASSET_SOURCES.json` for source hashes.

WPF-rendered Home previews at 1920 x 1080 and 3840 x 2160 and the Quick menu preview were inspected with the supplied wordmark. The existing six-page smoke run completed, including modal focus and Quick menu final-action visibility checks. Reports and previews: `artifacts/ui-031/smoke/`. The preview used a separate data folder and removed no Windows apps.

The sound worker now starts finite cues with SND_ASYNC and without SND_NOSTOP, so each navigation request replaces the currently playing cue instead of waiting for its duration. A bounded one-entry channel replaces stale pending requests. The 100 ms move debounce was removed. Existing PCM preparation, volume limits and quiet edges are unchanged. Mute and shutdown call Stop explicitly. API reference: [Microsoft PlaySound](https://learn.microsoft.com/en-us/previous-versions/dd743680(v=vs.85)).

The native `--sound-burst-probe` invokes the application's actual UiSoundService. All eight requests played successfully, with move start gaps of 93–94 ms and request-to-API-return latency of 0–32 ms. Each move file lasts about 290 ms, so this verifies interruption during the preceding cue. Digital mute blocked a subsequent request. These timings measure application/API behavior, not physical output latency. The user listened to this rapid sequence and confirmed it was fast and clean. Report: `artifacts/audio-native-031/sound-burst.json`. All 67 tests passed and Release compilation reported zero warnings/errors.

13 September 2026. Windows 11 Pro build 26340, RX 9070, primary 3840 x 2160 display at 150% DPI, wireless DualSense and the user's current audio output.

## Console desktop

`DesktopProbe` exercised actual Windows APIs, not mocked surfaces. The test found two visible Explorer surfaces (taskbar and desktop icons), hid both, applied a discovered game's cached hero through IDesktopWallpaper, and restored both surfaces and the original wallpaper. Wallpaper position, color and Windows Spotlight background type 3 were preserved.

The forced-exit test called Environment.Exit(23) to bypass normal WPF cleanup. A separate guardian restored the original wallpaper and shell surfaces and removed the recovery journal. Both normal and forced-exit checks passed. Reports: `artifacts/desktop-030/desktop-cycle.json`, `desktop-crash.json`, `desktop-inspect.json`, `crash-verification.json`.

Explorer's process remains alive for file dialogs and store protocols. The implementation handles per-monitor wallpapers and slideshow lists, but the native test covered one monitor and Spotlight's current image/mode. Multi-monitor transitions and long-running slideshow/Spotlight behavior are not certified by this check.

## Playing and Stop

The UI smoke test launched the native child-process fixture, verified shell minimization, returned to its detail page, asserted Playing and a visible Stop button, and requested graceful closure. The game process closed and the shell returned. The session log recorded one second of play in the explicit-stop run; the smoke report's `seconds` field is the fixture's accumulated local playtime across runs.

Home, game cards and Quick menu use the same session state. Starting offers Cancel, Playing offers Stop; the existing game-close dialog provides graceful and confirmed force-close choices. A complete authenticated commercial-game run remains separate from this native fixture test.

## Optional Windows apps

The explicit catalog covers 40 package identities across six groups. Native discovery on this PC returned 31 installed candidates. Selection starts empty, individual/group/all selection is available, and a separate review lists the exact selected apps before removal. Core Windows, drivers, runtimes, Store and gaming dependencies are excluded.

The smoke test selected all 31 candidates, checked selection and logical modal focus, inspected the review and scrolled to its final row. It then cleared the selection. **No package was uninstalled during validation.** Removal is scoped to the signed-in account; it does not deprovision the Windows image or remove other users' apps. Native removal revalidates each exact package identity and reports per-app failures.

References: `artifacts/ui-v02/smoke/optional-apps-1080p.png`, `cleanup-review-1080p.png`, `cleanup-review-bottom-1080p.png`, `playing-stop-1080p.png`, and `report.json`. The final Quick menu action visibility check also passed.

## Sound correction

The user reported noise with both prior sound packs and worse, growing noise during a shared WASAPI experiment. The experiment was stopped immediately and menu sounds were disabled while investigating. The user confirmed the noise stopped with Pame closed and other applications sounded normal.

The final implementation removes the persistent custom audio stream and mixer. A worker prepares short 16-bit PCM WAV files and uses Windows PlaySound synchronously, one finite cue at a time. No device stream remains open between cues. Preparation preserves source sample rate/channels, never amplifies a quiet source, caps peaks, fades edges and bounds cue duration. Late queued requests are discarded. This trades overlapping effects for finite, serialized playback.

Unit tests verify valid PCM output, sample limits, silent edges, bounded duration, digital mute, no quiet-noise amplification and malformed-input rejection. A standalone native PS5 move cue returned success in 0.363 seconds and exited its process. Report/file: `artifacts/audio-native-030/sound-probe.json`, `move-native.wav`. Perceived noise on the physical output is checked through user feedback, not inferred solely from a successful playback API.

All 67 automated tests pass. Release build completed with zero warnings/errors. No Windows app, driver or service was removed during this turn's tests.

## Installed build and physical audio confirmation

The 0.3.0 installer exited successfully. The installed executable reports 0.3.0.0, its Pame.dll hash matches the published build, and the existing database hash was unchanged by installation. Report: `artifacts/install-030.json`. Menu sounds were subsequently re-enabled intentionally with the existing PlayStation pack and volume 40.

The user confirmed that the isolated native cue sounded clean, then confirmed that navigating several menus in the installed application stayed clean and became silent when navigation stopped. This confirms the reported noise fix on this user's current output; it is not a certification of every audio device.

Inspection through the installed executable found zero visible Explorer shell surfaces, two recorded surfaces for restoration, the original wallpaper preserved while no game was active, and both the installed main process and its guardian running. Report: `artifacts/installed-030/console-check.json`.

Windows API references: [IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper), [Windows package management](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/sideload-apps-with-dism-s14?view=windows-11).
