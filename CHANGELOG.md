# Pame 0.4.1

- Add a Now Playing card pinned under the Quick menu header. Show title, artist/source, artwork when Windows provides it, play state, timeline and supported transport controls.
- Show separate Pame browser media elements/tabs/iframes and Windows media sessions. Send each command to the selected source; never broadcast media keys.
- Cycle sources using the right stick horizontally, with a deadzone, neutral gate and controlled repeat. Preserve selected source and keyboard/controller focus during updates. Square/X/Y (west face button) toggles the selected media while the Quick menu is open.
- Allow an explicit Play command in the Quick menu to resume a hidden browser source during a game. Pause/close/navigation ends that override; existing automatic hidden-media behavior remains the default.
- Keep all Quick menu actions reachable beneath the fixed player, and return browser pointer focus when dismissing the Quick menu.
- Keep known browser media resumable without suspending its media pipeline; verify the playback clock advances with the tab hidden and Pame minimized.
- Restore selected background apps with all their windows hidden, including delayed secondary windows, while keeping their processes running. Recover apps concurrently and keep unrelated windows unchanged.
- Add Settings → Pame on GitHub for the public repository, release downloads and feedback, with links opening inside Pame browser.

# Pame 0.4.0

- Fix clipped search text by measuring the entry content with sufficient vertical padding.
- Add non-activating controller connection/disconnection overlays with player numbers, and one low-battery warning per charge cycle. Default threshold is 15%; Settings → Controller notifications can change it or turn battery warnings off.
- Automatically enter controller mouse mode for Windows/store handoffs. Holding the south face button holds the left mouse button for dragging; releasing, disconnecting or leaving mouse mode releases it. A redesigned hint bar explains the controls and direct Guide/PS return.
- Add Pame browser inside the existing sidebar layout, using the Chromium-based Microsoft Edge WebView2 runtime. Includes up to eight tabs, saved media favorites, controller pointer/scroll/keyboard, page navigation, explicit website permission/download choices and Quick menu access during games.
- Pause and mute hidden browser media by default, with a setting to keep the current tab playing when hidden. A Return to game control restores game focus.
- Provision the Evergreen WebView2 runtime during installation only when missing. The main app remains a normal-user process.
- See docs/VALIDATION-0.4.0.md for tests and remaining platform limits.

# Pame 0.3.3

- Add Background activity settings with a master switch, per-item selections, actual session results and manual recovery. App controls are enabled by default; Windows service controls remain off without the optional installation add-on.
- Close selected Copilot/Microsoft 365 Copilot, Xbox app and Widgets packages during games and reopen only previously running apps. Preserve game dependencies; skip Xbox app closure during Xbox games.
- Cleanly exit/restart OneDrive and temporarily reduce browser/other-launcher CPU priority and EcoQoS, preserving original process settings and active stores.
- Temporarily stop selected Windows Search, diagnostics, printing, maps and media-sharing services through a narrowly scoped, optional installed Windows service. Preserve startup settings and originally stopped services; check active dependencies and print queues.
- Add independent crash recovery and durable journals. The optional service add-on requests administrator consent only during installation/update; there is no game-launch elevation path. Failed/skipped operations are visible in Settings.
- Validate real app/service cycles, forced-exit recovery and controller-focused settings. See docs/VALIDATION-0.3.3.md for evidence and limits.

# Pame 0.3.2

- Decode game artwork into a standard bitmap in Windows' theme cache and verify that Windows has applied it before opening the store/game. This handles rejection of this PC's local Pame artwork paths and the asynchronous wallpaper-application gap.
- Minimize existing application windows before launch; newly opened game and anti-cheat windows stay visible. A durable journal restores the previously visible windows when leaving Pame/Console mode or after a crash.
- Restore and focus Pame before waiting for capture/wallpaper cleanup. Native window activation verifies foreground ownership, with bounded retries.
- Query executable paths with limited process access, exclude EAC/BattlEye wrapper and browser-helper executables from session ownership, and retain a stable visible game target. Exit detection allows a two-second process handoff gap. Cleanup failures no longer prevent the exit event.
- All 74 unit tests pass. A native game-transition fixture verifies wallpaper, minimizing four existing windows, automatic return with foreground focus, and wallpaper restoration. A forced-exit guardian test also restores application windows.

See [0.3.2 validation](docs/VALIDATION-0.3.2.md).

# Pame 0.3.1

- Integrated the user's supplied Pame wordmark in the sidebar and Quick menu, with the supplied app icon in the window, executable, shortcuts and installer. Original PNGs are preserved; the Windows ICO contains ten sizes from 16 to 256 pixels.
- Rapid navigation now interrupts and restarts menu cues immediately through asynchronous Windows PlaySound. Removed the 100 ms navigation debounce and playback-duration wait. Finite PCM preparation and volume limits remain unchanged; muting and shutdown stop the active cue.
- A native burst check played all eight requests, with 93–94 ms between move starts instead of waiting for each 290 ms file. Playback API latency was 0–32 ms on this machine; this does not measure speaker latency. All 67 automated tests pass.

# Pame 0.3.0

- Replaced menu audio streaming with finite 16-bit PCM WAV playback through Windows PlaySound. A worker plays one bounded cue at a time, with quiet edges and no persistent audio stream. Quiet sources are never amplified.
- Added Starting/Playing states, a Stop/Cancel control in Home and game details, and playing badges on cards. Quick menu reflects the running state; Stop uses the existing graceful/force-close flow.
- Added fullscreen Console mode: hide the taskbar and desktop icons, use game artwork as the Windows background while playing, and restore previous state afterwards. Desktop control temporarily restores the Windows interface. A separate guardian restores state if Pame terminates unexpectedly.
- Expanded Windows cleanup to 40 explicit optional package identities. Users choose individual apps, groups or all optional apps and review the exact selection before removal. This PC exposes 31 candidates. No app is removed automatically.
- Fixed initial modal focus on disabled actions and prevented controller confirmation from activating background-page buttons. Cleanup review rows support controller scrolling.

See [validation](docs/VALIDATION-0.3.md).

# Pame 0.2.1

- Restored automatic GPU composition on the active display. An installed but inactive virtual display adapter no longer forces CPU rendering; legacy detection preferences migrate to Auto. Recovery and explicit software compatibility remain available.
- Removed focus effects that rasterized enlarged text and artwork. Focus now uses crisp vector borders, and page motion avoids opacity/scaling of the entire page.
- Fixed artwork cache collisions between thumbnail and hero sizes; decode artwork off the UI thread and use the source resolution for large backgrounds.
- Rebuilt navigation audio around a dedicated worker and reusable output device. Added local sound-pack selection, trimmed capture pre-roll and prevented simultaneous duplicate cues.
- Installed a community PS5 sound pack on the requested machine; original Pame sounds remain in the installer.
- On this RX 9070 at 4K/150% DPI, a repeatable navigation run improved the median WPF composition interval from 67.8 to 16.9 ms. These are composition events, not measured display-present FPS. See [validation](docs/VALIDATION-0.2.1.md).

# Pame 0.2.0

## Interface and controller navigation

- Bundled Inter fonts, consistent sizing, lighter focus rings, short page transitions and a reduced-motion option.
- Real store logos for all nine integrations, Lucide system icons and Kenney PNG button images for Xbox, PlayStation and Switch. Automatic prompts follow the controller; manual selection is available.
- Six original navigation sounds, with independent volume and mute settings.
- Rebuilt quick menu: compact metrics, fixed header/footer, scrolling body and focus-driven scrolling. All default actions fit; long content remains reachable.
- Rebalanced home, store and controller cards. Controller connection/battery changes refresh the controller page without resetting focus when nothing changed.
- Wrapped dialog actions, fixed dialog headings and opaque dialog backgrounds improve readability for long device/game names.

## Functional additions

- Live performance dashboard with CPU/GPU history, RAM/VRAM, game FPS/frame time, and GPU temperature/clock/capacity when the graphics driver exposes them.
- Per-game audio output, refresh rate, Windows GPU preference, CPU affinity and controller light color, alongside existing power and priority profiles. Temporary settings have durable restoration journals; process recovery validates PID, creation time and executable.
- Controller desktop mode: left stick pointer, right stick scrolling, mouse buttons, Windows keyboard and a route back through the quick menu.
- Native Wi-Fi scan, saved-network connection and new personal/open networks using a controller keyboard. Enterprise/security flows unsupported by the adapter remain in Windows.
- Storage capacity bars and largest-game management links.
- Reversible current-user startup-entry management and individual optional Windows app removal. Protected system/gaming components are excluded.
- Steam update byte progress, refreshed from local manifests every five seconds while Downloads is open. Store clients still own queues, pause/resume and bandwidth controls.
- Official download links for missing stores and an explicit graceful/force-close game menu.

See [docs/VALIDATION-0.2.md](docs/VALIDATION-0.2.md) for evidence and [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md) for remaining work. This release does not claim complete production or hardware certification.
