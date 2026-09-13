# Architecture

Pame uses .NET 10 and WPF with a 1600×900 logical layout scaled to the window. It runs as the signed-in user. A privileged service is not installed because current implemented operations do not require one. Unsupported elevated features remain unavailable.

## Modules

- **Pame.Core**: canonical game/store/settings models, independent bounded Valve KeyValues parser, merge/sort/filter policies, input interpretation, player allocation, path safety and SQLite persistence.
- **Pame.Windows**: store discovery, metadata cache, SDL controller adapter, WinRT Bluetooth discovery/pairing, Win32 process sessions, PDH/GetSystemTimes memory and GPU monitoring, Core Audio, power plans and PresentMon process adapter.
- **Pame.App**: WPF pages, explicit spatial focus traversal, modal focus containment, onscreen keyboard, quick menu, diagnostics and recovery shortcuts.
- **Pame.Tests**: manifest, database, input, safety and real-machine discovery tests, including a process-local SDL virtual gamepad.
- **Pame.TestGame / Pame.Diagnostics**: separate test executables. A short-lived launcher starts an actual child window; native integration tests measure that child lifecycle. Not shipped in the installer.

## Threading and lifecycle

SDL is initialized and pumped on WPF's main thread. Connected pads are polled at 16 ms; with no pads, polling drops to 250 ms. Enumeration and battery are refreshed every two seconds. Discovery, process inspection and metrics run off the UI thread. Metadata uses asynchronous HTTP and is fetched sequentially to avoid request bursts. Artwork is decoded on a worker before page construction, frozen for cross-thread use, and cached by full path plus decode width. Hero decoding follows physical window width up to 3840 pixels and never exceeds the source resolution. WPF chooses the active adapter automatically; software rendering is an explicit compatibility/recovery option.

Navigation audio uses a bounded nonblocking queue and a dedicated worker. Finite 16-bit PCM WAV files retain source sample rate/channels, apply volume without amplifying quiet recordings, cap peak amplitude and fade edges. Windows PlaySound starts each short file asynchronously on the worker. A new navigation event replaces the current cue immediately, without waiting for its tail. A one-entry channel coalesces pending requests if preparation is busy. Muting and disposal stop the current cue. There is no continuous stream, custom audio callback or overlapping mixer. Prepared files are cached by source modification time, cue and volume. Local sound packs live outside the installer. Focus rings use vector borders rather than effects that rasterize their content.

Launching uses store protocols where available. The session monitor identifies executables inside the discovered game's install root, excludes known installer/crash/anti-cheat helper names, and watches all matching processes. It tolerates intermediary process transitions and a four-second gap before completing a session. The gap is excluded from measured playtime. Store login and updates remain owned by the store.

Session heartbeats are persisted each second. The end operation is idempotent and transactional. After an interrupted app run, only the last recorded heartbeat contributes to playtime. Downtime is never counted. Imported Steam playtime is a one-time baseline; later local time is recorded separately.

## System safety

Game profiles can temporarily change power, priority, affinity, default audio, refresh rate and the executable's Windows GPU preference. Each integration journals its original values before mutation and restores on game end or next start. Process restoration validates PID, start time and exact executable to reject reused PIDs. The GPU preference preserves other value options. Controller light profiles restore the user's normal colors. HDR and system overclocking are not implemented.

Startup maintenance changes only explicitly selected current-user Run entries, preserving original commands in SQLite. Optional app removal uses an explicit allowlist and a confirmation for each current-user package. Gaming mode does not stop background programs or remove components automatically.

LibreHardwareMonitor polls CPU/GPU groups every four seconds on a worker. CPU/RAM and GPU counter workers are independent so GPU enumeration does not block CPU samples. Unknown sensors remain unknown. The live dashboard retains a minute of in-memory history.

Native Wi-Fi profiles use exact SSID bytes and escaped XML. New profiles have unique names, and failed connections remove only the newly created profile. Passwords are passed to Windows and not persisted in Pame. The controller text keyboard keeps text only in transient UI state.

Desktop controller input is opt-in and uses SendInput/SetCursorPos as the signed-in user. It is suspended while the quick menu is active and disabled when returning home or starting a tracked game. Protected/elevated desktop surfaces may reject input.

Bluetooth repair operates on the selected exact Windows association endpoint and rejects connected or unpaired targets. It does not remove arbitrary PnP instances. The low-level disconnect path uses the Sony controller's own Bluetooth serial/address.

Manifest paths are normalized and containment checked. Steam IDs must be numeric. Epic IDs are URI escaped. No metadata field becomes an executable command. Process launches do not use a command shell. Metadata image hosts and download sizes are bounded.

## Overlay

The quick menu is a separate topmost WPF window; it does not inject into games, hook rendering APIs or install drivers. Guide is monitored using SDL's background input, with Start+Back and Ctrl+Alt+P fallbacks. The quick menu changes focus while open. True exclusive fullscreen and system-reserved Guide inputs may limit availability.

## Packaging and updates

Inno Setup installs a self-contained x64 build to `%LOCALAPPDATA%\Programs\Pame`, adds uninstall metadata and optional shortcuts/startup. The application data directory is separate and retained. There is no self-update downloader. A future updater must use authenticated, signed release metadata and a verified package, with process coordination and database backups before migrations.


## Console desktop session

DesktopShellService enumerates only Explorer-owned taskbar and desktop-icon surfaces. It records handle, PID, process start time and class before hiding them; restoration validates identity to reject reused handles/PIDs. IDesktopWallpaper captures per-monitor images, position, color, slideshow paths/options and background mode before game artwork is applied. Original files are not deleted or renamed.

An atomic JSON recovery journal is written before changes. A separate instance of Pame in guardian mode acknowledges readiness before any shell surface is hidden, waits for the exact owner process, then restores the journal after unexpected termination. Normal exit uses the same restoration path. Guardian processes also exit when their journal is removed/replaced. File reads permit atomic replacement; a per-data-directory mutex serializes recovery. Desktop control and windowed/recovery mode restore the shell explicitly.

The cleanup catalog separates content/promotions, communication, utilities, cloud storage, developer tools and gaming extras. Nothing is selected automatically. Removal revalidates exact installed package identities, executes only chosen current-user packages sequentially and records success/failure for each. It does not stop services, uninstall drivers or remove provisioned packages for future users.
