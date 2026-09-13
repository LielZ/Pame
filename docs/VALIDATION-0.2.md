# Pame 0.2 validation

Test date: 13 September 2026. Windows 11 Pro build 26340, Ryzen 7 7800X3D, Radeon RX 9070, 31.15 GiB system memory, primary display 3840×2160 at 60 Hz. The initial 0.2.0 build selected software rendering because an inactive virtual display adapter was installed. This incorrect heuristic was removed in 0.2.1; see [the performance update](VALIDATION-0.2.1.md).

## Automated and native checks

- 52 xUnit tests pass. These cover library parsing/merging, persistence, controller input including a process-local SDL virtual device, process identity guards, cleanup exclusions, desktop pointer deadzone/bounds and escaped Wi-Fi profile XML.
- Release build succeeds with zero warnings/errors. NuGet's configured advisory feed reports no known vulnerable direct/transitive packages at this check.
- Native integration report: `artifacts/integration-v02-final/report.json`.
- CPU/RAM/GPU/VRAM counters populated. RX 9070 temperature was 49°C, clock 111 MHz at that idle sample, capacity 15.92 GiB. Unsupported CPU sensor zeros are filtered to null.
- Display modes enumerated: 3840×2160, 60 Hz. Native CDS_TEST succeeds for the current mode; no different refresh mode is exposed here.
- Four Wi-Fi networks and one saved profile were detected. No credentials or SSIDs are included in the exported count report; no network was switched during tests.
- Two audio endpoints enumerated. A later profile test temporarily switched to the alternate endpoint, then recovered the original output using the journal.
- A real native child-window fixture verified intermediary launcher tracking, seven-to-eight-second sessions and ETW capture around 39–40 FPS / 25–26 ms. Above-normal priority and CPU affinity were applied and restored while the fixture was alive. Fresh service instances verified recovery from the persisted records.
- Windows GPU preference was applied to the test fixture's own executable and then restored to its original registry state. This tests preference persistence/recovery, not physical GPU routing.
- The initial Bluetooth diagnostic found a disconnected paired DualSense. During final self-contained UI validation it connected wirelessly: SDL reported player 1, approximately 95% battery, rumble/RGB/player-light capabilities and successful light-report submission. This is real hardware enumeration; visible LEDs and felt rumble have not yet been confirmed. Pairing repair was a dry run; no device was unpaired.

## Visual and interaction checks

The running WPF app exported 1080p page/dialog references and 1440p/4K home views using the real discovered library (10 games, four installed stores). Sources are under `artifacts/ui-v02/smoke` and the later installed validation directory. Reference renders are application renders, not camera/TV measurements.

Reviewed: Home, Games, Stores, Downloads, Controllers, Settings, game details, search, appearance, performance, storage, startup entries, optional apps, network, profile, text keyboard, all three controller-prompt families and both ends of the quick menu. No fake game/performance/controller data was added to the normal user database. The quick-menu test checks that its final action is entirely inside the scroll viewport after focus moves to it.

Actual native-window screenshots also show the updated Home with loaded fonts, game artwork, store logos, system icons and button images. The desktop automation helper repeatedly rejected click input with `foreground window did not report a process id`; click automation was stopped after a fresh-selection retry. These screenshots and self-tests do not establish a physical-controller playthrough.

Navigation sounds are short original PCM clips with cached playback, mute and independent volume. UI runs produced no audio initialization errors. End-to-end listening on the user's speakers is not certified by a successful audio API call.

## Boundaries

No game/optional-app uninstall, power shutdown/restart, real pairing repair or arbitrary background-app termination was performed. Steam's original real-game test reached sign-in; a complete authenticated real-game launch/return remains unverified. DualSense enumeration and battery were confirmed; a complete physical-controller playthrough remains outstanding. Exclusive fullscreen, anti-cheat overlays, real TV distances/HDR and prolonged multi-device reliability still need hardware testing. See `KNOWN_LIMITATIONS.md` for features outside this release.

## Installed build

The 0.2.0 self-contained installer completed with exit code 0. The normal user database's SHA-256 was identical before and after the upgrade. The installed executable reports version 0.2.0, and the existing current-user startup entry still points to `%LOCALAPPDATA%\Programs\Pame\Pame.exe --startup`. The app was launched fullscreen and an actual native-window screenshot confirmed the loaded UI and connected DualSense/PlayStation prompts.

A 77-second process sample taken shortly after launch, with the wireless controller connected, measured 2.043% total-machine CPU, 276.1 MiB working set and 176.0 MiB private memory. It includes startup/counter warm-up and software rendering; it is not a controlled sustained idle or in-game benchmark. A later 15-second thread sample used much less CPU, so this should not be extrapolated as steady-state overhead.

Release files are under `dist/`. The package contains the four font weights, 77 PNG assets, six WAV cues and dependency licenses. No user database, game artwork cache, test-game binary or standalone kernel driver is included.
