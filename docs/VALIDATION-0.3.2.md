# Pame 0.3.2 validation

13 September 2026, on the existing Windows 11 / RX 9070 / wireless DualSense machine.

## Game wallpaper

The actual Fortnite JPEG existed and decoded successfully, but IDesktopWallpaper.SetWallpaper rejected its path with HRESULT 0x80070002. Reproduction: `artifacts/fortnite-before-032/desktop-cycle.json`; reordered API calls failed at the same stage in `artifacts/fortnite-order-032`.

Further testing showed that the path, not just the encoding, matters on this machine: byte-identical BMPs worked from the workspace and failed from LocalAppData/Pame. The app now decodes the image into an opaque 24-bit BMP in the user's Roaming/Microsoft/Windows/Themes/Pame cache, with a separate hashed directory for each Pame data folder. That Windows theme-cache path passed the native application/restoration cycle. The original artwork is unchanged. A further launch test exposed that Windows can return success before the desktop handler applies the new image. The implementation therefore checks the monitor wallpaper paths until the new file has been applied, with a bounded timeout, before launching the game.

## Existing windows and return to Pame

The targeted transition probe uses the same LaunchGameAsync path as the Play buttons. It launches a separate background-window fixture, applies the Fortnite artwork, minimizes the existing windows, launches a real child-process game fixture, waits for its natural exit, and checks Pame's native foreground window handle as well as WPF visibility/activation. Pame briefly retains topmost ordering during return, checks foreground again while store windows settle, and releases that temporary ordering afterwards.

Final report: `artifacts/transition-032-final/transition-report.json`, with explicit start/completion timestamps. All four existing windows were minimized, the game wallpaper was applied before launch, Pame minimized during play, and Pame returned with foreground focus after exit. The original wallpaper was restored. The initial report failed its wallpaper timing check; that failure led to the explicit application check above. The final run passed every condition and its app process closed successfully.

A repeated transition exposed a readiness-file race: the parent could see the guardian's marker before its writer closed the file. The guardian now publishes the marker with an atomic rename after closing it; marker cleanup cannot abort an already-established handshake.

The separate guardian was exercised with Environment.Exit(23), bypassing normal cleanup. It restored the original wallpaper, Explorer surfaces and all three existing application windows, and cleared its journal: `artifacts/crash-windows-032/recovery-check.json`. Window records validate handle, process ID, creation time and class; windows already minimized before launch are not restored.

## Process tracking and background work

The seven new unit cases cover the actual Fortnite client, its launcher, EAC/BattlEye wrappers, the bootstrapper, Unreal's browser subprocess, and executable resolution through PROCESS_QUERY_LIMITED_INFORMATION. All 74 tests passed; Release compilation produced zero warnings/errors. A real Fortnite run confirmed that limited-information querying resolves the protected shipping client's path even when Win32_Process/MainModule inspection cannot. The first real run also showed a store-wrapper handoff on exit; FortniteLauncher is now excluded explicitly.

This update does not stop, suspend or disable background applications or Windows services. Existing Gaming mode changes the power plan; opt-in game profiles can change the game's priority, affinity, audio output, refresh rate and GPU preference, with existing restoration journals.

## Installed build: real Fortnite session

The final installer completed successfully, preserved the existing database, and installed version 0.3.2.0 with binaries matching the published build (`artifacts/install-032.json`). Its SHA-256 is ED464DE4E207C9FC1E663F87A9CE05660F539E49F50DD087FB0A84C878016DFB.

The normal installed app launched Fortnite at 11:27:29 UTC. Before the launch request, Windows reported the Fortnite wallpaper applied from the Roaming theme cache and all three existing application windows minimized. The tracker attached to the actual shipping client, PID 7944. Its exit was recorded at 11:29:52 UTC, followed by restored optimization settings and a settled return at 11:29:53 with foreground=true, visible=true and minimized=false. No UI automation activated Pame during that return. A subsequent Steam launch began at 11:30:15; Pame was appropriately minimized again, so a later read-only capture cannot establish what the user saw during the earlier return. The user subsequently confirmed that it now works well before requesting research into reversible background-process management.

API references: [SetWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-setwallpaper), [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow).
