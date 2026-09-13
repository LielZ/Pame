# Validation on the development PC

Date: 2026-09-13. This report separates verified behavior from implemented but unverified capabilities.

## Machine

- Windows 11 Pro Insider Preview, build 26340.
- AMD Ryzen 7 7800X3D; Radeon RX 9070 and AMD integrated graphics.
- About 31.15 GiB visible RAM.
- MediaTek Bluetooth adapter; one paired, disconnected DualSense record.
- A SudoMaker virtual display adapter is installed. Hardware WPF rendering produced a white client area; the software renderer painted correctly. First-run detection now chooses the working renderer on this machine.

The complete local inventory is in `artifacts/machine-inventory.json` and is not shipped.

## Automated and native checks

**43 automated tests pass**: Steam/Epic parsing, corrupt input, path escape rejection, exclusion of stores/tools, deduplication, sorting, metadata/user-data preservation, delayed metadata versus finished sessions, battery unknown state, deadzone, input edge/repeat, Guide hold, player-slot allocation, SQLite persistence/migrations, idempotent session completion, crash heartbeat recovery and conservative store closure policy.

A native SDL virtual joystick was attached inside the test process. The actual controller adapter enumerated it, assigned a slot and delivered confirm, analog-right and Guide-hold events. This does not substitute for physical hardware validation.

Native integration report: `artifacts/integration/report.json`.

- CPU/RAM/GPU/dedicated-memory sources return live Windows data. Initial unknown values are displayed as dashes.
- Windows Core Audio enumerates two active outputs. Selecting the already-current default endpoint succeeds and leaves it selected.
- WinRT Bluetooth discovery finds the paired, disconnected DualSense with its exact endpoint identity. Repair commands validate in dry run. No device was unpaired or disconnected.
- A real local fixture launches through an intermediary process, opens a WPF window, draws and exits after eight seconds. Pame tracks the child and records approximately eight seconds, excluding its exit grace period.
- PresentMon successfully produced ETW frame intervals for the fixture. These are fixture measurements, not a game's FPS or a gaming benchmark. Incompatible processes/permissions still yield unavailable values.

## UI verification

Local smoke output: `artifacts/ui-validation/smoke/`.

- Home, Games, Stores, Downloads, Controllers, Settings, details, search keyboard and overlay are rendered and inspected.
- Home render sizes: 1920×1080, 2560×1440 and 3840×2160. WPF uses a scalable logical layout and PerMonitorV2 manifest.
- The UI lifecycle test confirms `minimized: true`, `returned: true` and eight seconds of local playtime around the real fixture process.
- Windows UI Automation inspected the actual Pame window and focused buttons; keyboard/controller-equivalent page navigation was exercised. The actual software-rendered window was visually captured after activation.
- The build never inserts fabricated game or controller data into the normal user library. Fixture game records use the isolated validation data directory.

## Library and game test

Eight valid installed games and four installed stores were found. Steam libraries on C: and D: are read; incomplete or absent game folders do not become playable entries. EA registrations and Fortnite's Epic manifest are included. A zero-filled Epic manifest is logged and skipped without interrupting discovery.

Launching Age of Empires II through Pame opened Steam's sign-in window. The game itself could not start without the user signing in. Pame timed out cleanly and did not add unobserved playtime. No real game uninstall, forced game termination, power action, pairing repair or Windows cleanup was used for tests.

## Release checks

The app builds without compiler warnings/errors. The pinned SQLite bridge replaces a vulnerable initially resolved dependency; the final NuGet vulnerability query reports none from the configured source. Packaging uses a self-contained .NET x64 build and a per-user Inno Setup installer. Research, test fixtures, personal library data and game artwork are excluded from the installed payload.

The final installer was run successfully on this PC (exit code 0), then upgraded in place with the library and preferences preserved. The installed executable runs from `%LOCALAPPDATA%\Programs\Pame\Pame.exe`. The uninstall registration, desktop shortcut and Start-menu recovery shortcut were verified. Launch at Windows sign-in is enabled in the current user's Run key and points to that installed executable with `--startup`. The final app was launched with `--fullscreen`.

Recovery mode was launched from the installed executable: it created a window, logged `safe: true`, skipped controller initialization and closed gracefully. The normal app was then restarted. A real reboot was not performed.

The final installed build also generated its six-page visual report using the real user library (`artifacts/installed-smoke.json`). CPU, GPU, RAM and VRAM were populated after counter warm-up. A 20-second idle sample of the normal installed process measured about **0.224% total-machine CPU**, **222.9 MiB working set** and **136.2 MiB private memory**, using software rendering. This short sample is not a long-duration performance benchmark.

The final desktop-automation inspection was blocked by the automation helper's `foreground window did not report a process id` error. Earlier actual-window inspection passed; the final binary was checked through its own WPF renders, process state and logs. Those final renders are not a substitute for a fresh desktop screenshot or physical-controller test.

Not yet verified: physical controller battery/LEDs/rumble/power behavior, actual paired-controller repair, all supported stores on installed hardware, real-game exclusive fullscreen overlays, real TVs/HDR/multi-monitor transitions, reboot startup across crashes and long-duration reliability.
