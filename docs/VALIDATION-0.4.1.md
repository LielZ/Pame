# Pame 0.4.1 — Quick menu media

Implementation uses per-source browser media and Windows System Media Transport Controls. The Quick menu pins the selected player above its scroll area. Right-stick horizontal input cycles sources, with a neutral gate, deadzone, 450 ms initial repeat and 250 ms repeated steps. Source selection remains stable as metadata changes. The west face button toggles the selected source while the Quick menu is open.

Browser observations and commands are scoped to the exact tab, frame, document and media element. Navigation, frame destruction and tab closure clear stale entries. Windows controls use the exact session object. No global media keys are sent. Only explicitly selecting Play in the Quick menu overrides automatic pausing for that browser tab. Paused media stays muted; tabs without known media may be suspended. Suspending an unloaded WebView2 composition view caused a reproducible false Playing state with its media clock stuck at zero, so media-bearing tabs now retain their media pipeline. Metadata is kept in memory, not added to the user's library/database.

## Validation

- 116 automated tests pass, including source selection stability, removal and wraparound, stick deadzone, neutral gating, held repeat and direction reversal.
- Native integration probe uses two browser tabs, a cross-origin iframe and a separate Windows MediaPlayer process. Test audio contains only silence, and the native player volume is zero. No commands target the user's unrelated media sessions.
- All fixture data and reports live in isolated artifacts directories. The fixture tests actual browser HTMLMediaElements and Windows media session commands, not static sample cards.
- Visual validation checks the full Quick menu and its last row while the player stays pinned.
- `artifacts/media-probe-041-r6/media-report.json`: all checks passed for browser detection, two tabs, hidden-tab pause and explicit resume, iframe detection, native source identity stability, Windows pause/play/next, right-stick source switching in both directions, focus preservation, selected-source isolation, website next-track handler, seeking forward exactly 10 seconds, reaching the last Quick menu action and removing a closed iframe source. Direct media-clock sampling advanced about 2.34 seconds over the 2.4-second hidden-tab check and also advanced after switching to Home and minimizing Pame.
- `artifacts/recovery-probe-041/recovery-report.json`: real native main and delayed secondary windows were hidden, their process stayed alive, an unrelated fixture window remained visible, and Pame retained foreground ownership. The same process-identity-checked window helper is used by background recovery. No user app was closed for this probe.
- `artifacts/project-probe-041/project-report.json`: the Settings card opens all four project-menu actions; repository and Releases actions navigate within Pame browser, preserving the existing repository tab. The menu screenshot was inspected for clipping and controller-readable text. Public repository availability is verified separately at publication.

## Background recovery and project links

Restarted packages are monitored for 7.5 seconds, hiding every top-level window owned by their exact package processes, not just MainWindowHandle. Window matching rechecks PID, creation time and executable path before hiding. It uses SW_HIDE instead of WM_CLOSE, so window closure does not accidentally terminate the restored app. Apps already running before recovery are left alone. Package activation remains normal-user and uses the existing validated app identities. Independent app restorations run concurrently; journal updates remain serialized, and failures remain pending for recovery. The final check requires the app process to still be present.

Settings → Pame on GitHub contains repository, Releases and issue links, plus a short explanation. Links use Pame's embedded browser and open a new tab when the current one has a page. Installer metadata points to the same public project. Source publication excludes runtime databases, profiles, logs, build outputs and local tooling.

## Sources and scope

- [Windows media session manager](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager): published media sessions from compatible applications.
- [Windows media session controls](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession): playback, track changes and seek capabilities.
- [Microsoft example of media session control](https://devblogs.microsoft.com/oldnewthing/20231108-00/?p=108980).
- [WebView2 frames and script messaging](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/frames).

Unavailable previous/next/seek controls are disabled. Websites using only Web Audio, closed shadow roots or proprietary players may not provide controllable HTML media. Native apps must publish SMTC metadata. A physical DualSense was detected during integration; right-stick routing in the automated probe is sampled input, not a claim that the stick was physically moved.
