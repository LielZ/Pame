# Dependencies and research

## Shipped dependencies

| Component | Version | Use | License / source |
|---|---|---|---|
| .NET Windows Desktop runtime | Resolved by .NET 10 publish; SDK installed here is 10.0.401 with runtime 10.0.12 | Self-contained WPF / CLR | MIT and bundled third-party notices; [dotnet](https://github.com/dotnet) |
| Microsoft.Data.Sqlite | 10.0.9 | SQLite persistence | MIT; [EF Core](https://github.com/dotnet/efcore) |
| Microsoft.Web.WebView2 | 1.0.4191.47 | Embedded Chromium browser with WPF composition | Microsoft SDK license and notices; [NuGet](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47) |
| SQLitePCLRaw | 2.1.13 | Native SQLite bridge/binary | Apache-2.0; SQLite public domain; [source](https://github.com/ericsink/SQLitePCL.raw) |
| NAudio | 2.2.1 | Windows Core Audio enumeration, volume | MIT; [source](https://github.com/naudio/NAudio) |
| System.Management / PerformanceCounter | 10.0.9 | Hardware enumeration / Windows counters | MIT; [dotnet/runtime](https://github.com/dotnet/runtime) |
| Windows SDK .NET projection | .NET target Windows SDK 10.0.19041 | WinRT device discovery/pairing | Microsoft SDK / projection notices |
| SDL3 x64 | 3.4.16 | Gamepads and Sony HID reports | zlib; [release](https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16) |
| PresentMon console x64 | 2.3.1 | Optional frame telemetry | MIT; [release](https://github.com/GameTechDev/PresentMon/releases/tag/v2.3.1) |
| LibreHardwareMonitorLib | 0.9.6 | GPU sensors and hardware identity | MPL-2.0; [source](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701) |
| ManagedNativeWifi | 3.0.2 | Native Windows Wi-Fi scan/profiles | MIT; [source](https://github.com/emoacht/ManagedNativeWifi/tree/06e455402d2d8cdcdb00456c45486819852ba830) |
| Inter | 4.1 | Bundled UI font | SIL OFL 1.1 |
| Kenney Input Prompts | 1.5A | Controller button/device PNGs | CC0 |
| Lucide / Simple Icons | 0.468.0 / 11.15.0 | System icons / store logos | ISC and retained MIT notices / CC0; trademarks retained |

The SQLite bridge is explicitly updated from the initially resolved 2.1.11 because NuGet reported a vulnerability in its native SQLite binary. The final NuGet vulnerability query reports no known vulnerable packages from the configured source; this is a point-in-time check, not a security certification.

Native SHA-256 pins:

```text
SDL3.dll       1F98969319302A100931F4385E5918A0BD53AB07773040682D22E7EDB54858C0
PresentMon.exe 364E5D98D4D134BD54DD25C22ED2CA2F4883F8BC3ED6502BEE0C151E3436D30C
```

## Tooling only

Pame browser uses the separately serviced Evergreen WebView2 runtime. Setup packages Microsoft's signed bootstrapper and invokes it only when neither the current user nor machine has the runtime registered. This machine already had runtime 152.0.4191.66. Deployment reference: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution . Runtime download requires Internet access on a machine without it; the portable app also requires WebView2 to be installed. Pame does not install a separate Edge browser or disable Chromium's security protections. Bootstrapper provenance is in docs/WEBVIEW2-SOURCE.json.

Inno Setup 6.7.3 builds the per-user installer. xUnit 2.9.3, Visual Studio runner 3.1.1 and Microsoft.NET.Test.Sdk 17.14.1 run tests. Test binaries, the SDK and cloned repositories are not in `dist/app`.

## Inspected implementations

| Repository / file | Inspection and decision |
|---|---|
| [SDL](https://github.com/libsdl-org/SDL), inspected commit `26b37f5d1c3518125ff8b269b5bdfcf452870500` | Sony HIDAPI driver, player LED masks, battery/connection capabilities, SDL gamepad and virtual joystick ABI. Use the released unmodified library rather than reimplement HID framing. |
| [PlayniteExtensions](https://github.com/JosefNemec/PlayniteExtensions), `809eab47a2b3be92fad1017c6f4d92cfd7f421c6` | Steam installed-state flags/library layout, Epic manifests, GOG/Ubisoft patterns. Independent adapters and KeyValues parser; no copied application code. |
| [PresentMon](https://github.com/GameTechDev/PresentMon), `f57eb474371c635ff2be620c04ca47400ca1b81a` | Standalone console lifecycle, CSV metrics, ETW privilege requirements. Ship a pinned official console binary. |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Unmodified 0.9.6 library now shipped for GPU sensor/ADL access. CPU values unavailable without supported access are left unknown. Pame does not install a hardware-monitoring driver. |
| [AudioSwitch IPolicyConfig](https://github.com/sirWest/AudioSwitch/blob/master/CoreAudioApi/Interfaces/IPolicyConfig.cs) | Apache-2.0 COM declaration adapted for endpoint switching. Attribution and license retained. |
| [Microsoft pairing docs](https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/pair-devices) and [Bluetooth disconnect IOCTL](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/bthioctl/ni-bthioctl-ioctl_bth_disconnect_device) | WinRT pairing ceremonies and targeted classic Bluetooth disconnect. |

A historical DS4Windows repository URL was unavailable and prompted for credentials; that fetch was stopped. No code from it is used. HidHide, ViGEm, kernel input filters, RTSS hooks and injected overlays are not installed.

The resolved transitive NuGet versions, license metadata and source commits are recorded in [docs/PACKAGE_INVENTORY.json](docs/PACKAGE_INVENTORY.json). LibreHardwareMonitor transitively brings BlackSharp.Core, DiskInfoToolkit, RAMSPDToolkit-NDD, HidSharp and Mono.Posix.NETStandard. Their notices and source-access links are included in the distribution. UI download provenance is recorded in [docs/UI_ASSET_SOURCES.json](docs/UI_ASSET_SOURCES.json) and [docs/INPUT_ASSET_SOURCES.json](docs/INPUT_ASSET_SOURCES.json). Artwork imports require Node with `sharp` available through normal module resolution or `NODE_PATH`; existing bundled assets do not need regeneration to build.
