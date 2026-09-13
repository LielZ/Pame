# Pame 0.4.0 validation — 2026-09-13

This release adds the embedded browser, controller notifications, search layout correction and desktop hold/drag behavior. It remains an alpha; the limits below are part of the result.

## Automated and native checks

- `dotnet test Pame.slnx -c Release`: 110 passed, zero failures. New coverage includes south/east mouse button state transitions, release on leaving pointer mode, player identity on disconnect, inclusive low-battery threshold/no repeated alerts, unknown/disabled battery alerts, default 15%, and permitted browser address/search handling.
- Release publish completed without warnings or errors.
- `artifacts/polish-browser-040-r2/polish-browser-report.json`: all checks passed on this Windows PC. Search text has enough measured height; visually checked at 1920×1080. Native product SendInput generated exactly one down and one up, remained held between samples, and delivered two drag moves. A real native click inside the embedded Chromium page changed its button; a clicked target=_blank link opened an internal tab. Pame text keyboard insertion, tab creation/closure, mute/resume and Quick menu bottom reachability passed.
- Screenshots in that directory show the search, browser home, local web fixture, fully loaded YouTube home, player 2 connect/disconnect/15% battery cards, battery threshold settings, desktop control bar and Quick menu. Notification display did not change foreground ownership.
- `artifacts/browser-transition-040-r2/transition-report.json`: passed using a separate native game fixture. Desktop handoff enabled pointer mode, displayed the bar and returned directly to Pame. While the fixture ran, Quick menu opened Pame browser and Return to game restored the actual game window's foreground focus. A controller notice appeared without taking focus. Four existing windows were minimized, the game wallpaper was applied, Pame returned automatically on game exit, and the previous wallpaper was restored.
- The first transition probe exposed a focus failure when returning from the browser. Game resume now uses the same native activation helper as the shell return. The repeated complete transition passed after the correction.

The test databases are isolated copies under artifacts, with no changes to the user's game library or actual selected background-activity settings. No real game was terminated, no package was removed, and no Windows service changes were used for these probes.

## Browser deployment

The per-user 0.4.0 installer completed successfully with no restart. `artifacts/upgrade-040/install-report.json` confirms that the pre-upgrade database hash was unchanged by setup, installed app/Windows library hashes match the published build, and the existing service remained running under the same process ID (13984). `artifacts/installed-polish-browser-040/polish-browser-report.json` repeats the full native input/browser/notification probe using the installed executable; every check passed.

- SDK: Microsoft.Web.WebView2 1.0.4191.47; WPF WebView2CompositionControl keeps Pame overlays above web content.
- Runtime on this machine: 152.0.4191.66. Browser profile is local to Pame at `%LOCALAPPDATA%/Pame/BrowserProfile`, separate from Edge.
- The bundled Evergreen bootstrapper is signed by Microsoft with a valid Authenticode signature. Version and SHA-256 are recorded in WEBVIEW2-SOURCE.json. Build verifies its signature before packaging.
- Setup checks both current-user and machine runtime registrations. It invokes the normal-user bootstrapper only if the runtime is absent. This PC already has the runtime, so upgrade does not need that installation. A clean Windows installation without WebView2 has not been tested.
- The existing optional 0.3.3 service-access add-on remains compatible and does not need reinstalling for this UI/browser update. The main app and game-launch path remain unelevated.

## Limits and follow-up coverage

No physical controller was connected for the final pointer/browser probes; they exercise the native input path and SDL-independent button state, not a long physical DualSense session. Synthetic notices exercise multiple player numbers and low battery. Real low-battery hardware, multi-monitor transitions, other controllers and long play sessions need wider coverage.

The browser is Chromium through WebView2, not an independently maintained Chromium fork. YouTube page loading is verified; paid-provider login, DRM video playback, browser extensions and every media website are not certified. Up to eight tabs and 24 displayed favorites are supported. Hidden media is paused/muted by default; browser options can preserve current-tab playback when hidden. New website permissions and downloads require an in-Pame choice; external app protocols do not escape the embedded browser.

Topmost notifications can display over normal and borderless game windows. True exclusive fullscreen and Windows secure desktop can cover them. Normal-user pointer input cannot control protected/elevated Windows dialogs, and Pame does not bypass those restrictions.
