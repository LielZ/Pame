# Background activity during games: research notes

September 13, 2026. Internet research, code review and read-only measurements on the development PC. No services or applications were stopped and no Windows settings were changed during this research. This document records the proposal before implementation; version 0.3.2 did not perform the actions described here. For the subsequent implementation, see [0.3.3 validation](VALIDATION-0.3.3.md).

**Conclusion:** Pame can offer temporary stops for a narrow list of services, alongside adjustments to applications the user chooses. Decisions should follow measured activity. A service accepting a Stop request does not mean it is unnecessary, and reopening an application does not guarantee recovery of its state, calls or unsaved work. Microsoft recommends identifying resource consumers and reducing unused background applications. [Microsoft performance guidance](https://support.microsoft.com/en-gb/windows/tips-to-improve-pc-performance-in-windows-b3b3ef5b-5953-fb6a-2528-4bbed82fba96).

## Development PC measurements

CPU usage was sampled for approximately five seconds and normalized across 16 logical processors. A later sample collected Private Working Set: private memory resident in RAM, distinct from Private Bytes, which measures allocation rather than necessarily physical memory. These were two separate samples, with no selected game benchmark. They do not establish an FPS improvement.

| Component | State | Private resident memory, MiB | CPU in the short sample |
| :--- | :--- | ---: | ---: |
| Windows Search / SearchIndexer | Running | 33.8 | 0.00%, rounded |
| DiagTrack | Running | 38.8 | 0.00%, rounded |
| Print Spooler | Running | 1.3 | 0.00%, rounded |
| SysMain | Running | 1.5 | 0.00%, rounded |
| MapsBroker | Already stopped | — | — |
| WMPNetworkSvc | Already stopped | — | — |

The first three services held 73.9 MiB combined, excluding Search helper processes. None had running dependent services at the time of inspection. All three accepted Stop requests but did not support Pause. These conditions must be checked again each session. Each had a separate host process during measurement; all memory in a shared svchost process must not be attributed to one service.

Windows reported approximately 18.29 GiB available out of 31.15 GiB accessible to the system. For scale only, steamwebhelper processes held 451.2 MiB combined, Edge approximately 239.6 MiB, and EADesktop with EACefSubProcess approximately 354.9 MiB. These numbers do not predict recoverable RAM, do not include every product dependency, and do not justify closing a store required by the game. The msedgewebview2 process name is shared by multiple products and is insufficient to establish ownership.

Local measurement files, excluded from the public repository: `artifacts/background-research-snapshot.json` and `artifacts/background-research-resident.json`.

## Service candidates and conditions

Microsoft's service catalog describes service responsibilities, but targets Windows IoT Enterprise and dedicated devices. It helps identify dependencies and feature impact; it is neither a performance benchmark nor blanket approval for a regular gaming PC. The following classifications are design recommendations for Pame. [Microsoft service catalog](https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/optimize/services).

| Component | Proposed treatment | Temporary impact / condition |
| :--- | :--- | :--- |
| WSearch | Advanced stop-and-restart option | Index updates and content search may be affected; only when running and causing interference. |
| DiagTrack | Optional, low-priority candidate | Diagnostic collection and transmission stop; the measured potential saving was small. |
| Spooler | Optional when no printing is in progress | Printing, including print-to-PDF uses, is unavailable. Skip if jobs are queued or the queue cannot be checked. |
| MapsBroker | Consider only when running and consuming resources | Downloaded map availability may be affected; already stopped on the sampled PC. |
| WMPNetworkSvc | Consider only without network media streaming | Media library sharing stops; already stopped on the sampled PC. |
| SysMain | Keep running | Intended to improve performance; no measured load justified a stop experiment. |

Windows Search may already pause indexing during Game Mode. Check whether there is indexing work to reduce first. The documentation describes a common condition, not a guarantee that every game pauses indexing. [Windows Search indexing states and performance](https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/windows-search-performance-issues).

## Applications: preserve state where possible

- **OneDrive:** The product provides Pause and Resume controls. These may help when active synchronization is unnecessary for the game. UI documentation does not establish an external API: this research found no documented public contract sufficient for reliable automation from Pame. Resolve that before promising an automatic integration, and preserve access to game files and cloud saves. [Pause and resume OneDrive](https://support.microsoft.com/en-us/onedrive/how-to-pause-and-resume-onedrive-sync).
- **Edge:** Sleeping Tabs preserves tabs while reducing activity and resource use, with exceptions for activity the browser intentionally retains. This is a browser feature, not a basis for externally freezing every browser process. [Edge performance features](https://support.microsoft.com/en-us/edge/learn-about-performance-features-in-microsoft-edge).
- **Selected applications:** Consider lower priority and EcoQoS, restoring the exact previous values afterwards. This can reduce CPU competition; it does not close the application or guarantee released RAM. Exclude audio, controller, recording and broadcasting processes needed by the user. Windows documents reading and writing PowerThrottling state. [GetProcessInformation](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessinformation), [SetProcessInformation](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation), [Quality of Service](https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service).
- **Close and reopen:** Make this a separate, explicit, product-specific option. Do not label it as Pause. Windows, edits, calls and downloads may not return to their previous state; launching an executable alone cannot guarantee restoration. Other stores are candidates only after checking active games, DRM, updates and synchronization.

General thread suspension is unsuitable as the basis for reliable restoration: a thread may hold locks required by other components. Microsoft describes SuspendThread primarily as a debugger tool and warns about deadlocks. [SuspendThread](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-suspendthread).

## Boundaries for the first implementation

Protect Defender and security services; networking, DNS and DHCP; Bluetooth, HID, audio and drivers; RPC, DCOM, WMI, Task Scheduler and Event Log; DWM and Explorer; anti-cheat, Xbox/Gaming Services, and the current game's store and dependencies. Keep Windows Update, BITS, Delivery Optimization and component installers outside the initial stop list. Address disruptive downloads through the product's scheduling or pause mechanisms. Pame and its recovery guardian must remain active.

Do not kill svchost processes, artificially empty RAM, select services solely by memory size, or apply a blanket list of “bloatware” process names. Fewer processes is not a measure of better game performance.

## Proposed implementation and recovery

These steps describe the design at the time of research, before implementation.

1. Provide a controller-operated screen with choices per component: leave running, reduce priority, pause through the product, or stop a service. The initial proposal selects no stop actions by default. Explain impact, availability, last measurement and administrator requirements.
2. Identify the game and its dependencies before acting. Stop services only after detecting the actual game process, avoiding delays to stores, installation and anti-cheat startup.
3. Write an atomic journal before every change: session identity and ownership, previous state, intended action, verified post-action state and errors. For services, record an allowlisted name and original state. For processes, record PID, creation time, path, product identity, original priority and QoS masks.
4. Use Service Control Manager, check CanStop and stable state, and skip services with running dependents. Do not change StartupType or stop dependencies broadly. SCM rejecting a stop because of active dependents is a reason for Pame to skip that action. [Stopping a service through SCM](https://learn.microsoft.com/en-us/windows/win32/services/stopping-a-service).
5. Manage privileged service operations through a narrowly authorized component with fixed service names and actions. Do not elevate the entire UI or put arbitrary commands in a recovery journal. The desktop recovery guardian does not automatically provide sufficient access for protected services.
6. Restore after normal game exit, Stop, launch failure, Pame shutdown and crashes. Return the Pame interface first and finish recovery separately. Start only a service that was previously running and that Pame actually stopped. Respect intervening user changes where observable, and retain failed actions in the journal. After reboot, do not revive old PIDs or closed applications; distinguish a previous session from Windows startup policy.
7. Show what was restored and what failed. Sending a Start request alone is insufficient to report successful recovery. Keep manual retry available for changed permissions or services that are not ready.

Integration points identified in the reviewed code: `GameSessionService.Run` after process detection and in its `finally` block; dedicated recovery alongside `OptimizationService`; process matching equivalent to `SavedProcessState.Matches`; and a separate guardian with suitable permissions for service actions. Pame's existing GamingMode setting selected a power plan; it did not establish that Windows Game Mode was enabled. The inspected GameBar registry values did not conclusively identify that switch's state.

## Measuring whether it helps

Compare several runs of the same game and scenario with controlled temperature and settings: average FPS, 1% lows, frame-time distribution, CPU, available memory and disk load. Test each action separately first, including recovery time and subsequent load. Exercise previously stopped services, active dependencies, denied permissions, stop timeouts, PID reuse, intervening user changes, Pame crashes and recovery failures.

No service-stop experiments or FPS comparisons were performed during this research. Practical performance benefits had not been measured.