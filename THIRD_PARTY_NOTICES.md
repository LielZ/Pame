# Third-party notices

Pame contains the following third-party binaries or API declarations. Full license text is included in the `licenses/` directory and copied into the installed application.

- **Microsoft.Web.WebView2 1.0.4191.47**, Microsoft Corporation. Unmodified SDK assemblies/native loader for the embedded browser, under Microsoft's SDK license in `licenses/WebView2-LICENSE.txt`, with `licenses/WebView2-NOTICE.txt`. The Evergreen runtime is distributed by Microsoft through its signed bootstrapper, subject to Microsoft's runtime terms; see https://developer.microsoft.com/en-us/microsoft-edge/webview2 and `docs/WEBVIEW2-SOURCE.json`.

- **SDL 3.4.16**, https://github.com/libsdl-org/SDL — zlib license. Copyright (C) 1997–2026 Sam Lantinga and contributors. Unmodified official x64 binary for input, device capabilities and controller reports. See `licenses/SDL.txt`.
- **PresentMon 2.3.1**, https://github.com/GameTechDev/PresentMon — MIT. Copyright (C) 2017–2024 Intel Corporation. Unmodified console binary used through its command line; no injected code. See `licenses/PresentMon.txt`.
- **NAudio 2.2.1**, https://github.com/naudio/NAudio — MIT. Copyright 2020 Mark Heath. Binary NuGet dependencies for Windows audio. See `licenses/NAudio.txt`.
- **Microsoft.Data.Sqlite / EF Core**, https://github.com/dotnet/efcore — MIT, .NET Foundation and contributors. Binary NuGet dependency. See `licenses/EntityFrameworkCore.txt`.
- **SQLitePCLRaw 2.1.13**, https://github.com/ericsink/SQLitePCL.raw — Apache License 2.0, Eric Sink and contributors. Binary provider and SQLite library. See `licenses/SQLitePCLRaw.txt`. SQLite itself is in the public domain: https://www.sqlite.org/copyright.html.
- **.NET, WPF and Windows support libraries**, https://github.com/dotnet — MIT and associated notices. Self-contained runtime and NuGet/projection assemblies. See `licenses/dotnet.txt` and `licenses/dotnet-third-party.txt`; runtime distribution notices may also accompany the published runtime.
- **AudioSwitch IPolicyConfig declaration**, https://github.com/sirWest/AudioSwitch — Apache License 2.0. API declaration adapted in `src/Pame.Windows/SystemServices.cs`: namespace, marshaling annotations, implementation and invoked methods differ from upstream. Used only for default endpoint selection. See `licenses/AudioSwitch.txt`.
- **LibreHardwareMonitorLib 0.9.6**, MPL-2.0, unmodified binary. Source is available at https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701 . See `licenses/LibreHardwareMonitor.txt`. Pame uses CPU/GPU groups and does not install its optional hardware access driver.
- **BlackSharp.Core 1.0.7**, **DiskInfoToolkit 1.1.2**, **RAMSPDToolkit-NDD 1.4.2**: unmodified MPL-2.0 transitive libraries. Their corresponding source is available at https://github.com/Blacktempel/BlackSharp/tree/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0 , https://github.com/Blacktempel/DiskInfoToolkit/tree/25319eae5781e75bcf141e844ceab2afe94d40ea and https://github.com/Blacktempel/RAMSPDToolkit/tree/3b47b960e0830fef344624ad5e389675d5f0a1ce . See the matching `licenses/BlackSharp.txt`, `DiskInfoToolkit.txt`, `RAMSPDToolkit.txt` files. No modifications were made to these libraries.
- **HidSharp 2.6.4**: retained upstream license in `licenses/HidSharp.txt`; https://software.seekye.com/hidsharp .
- **Mono.Posix.NETStandard 1.0.0**: Mono MIT and associated notices, `licenses/Mono.txt`; https://github.com/mono/mono .
- **ManagedNativeWifi 3.0.2**, MIT, emoacht; https://github.com/emoacht/ManagedNativeWifi/tree/06e455402d2d8cdcdb00456c45486819852ba830 . See `licenses/ManagedNativeWifi.txt`.
- **Inter 4.1**, Rasmus Andersson and contributors, SIL OFL 1.1. See `licenses/Inter.txt`; https://github.com/rsms/inter/releases/tag/v4.1 . Four unmodified font weights are bundled.
- **Lucide 0.468.0**, ISC and retained Feather MIT notices. See `licenses/Lucide.txt`; https://github.com/lucide-icons/lucide/tree/0.468.0 . Selected icons are rasterized and recolored.
- **Simple Icons 11.15.0**, CC0 artwork; logos remain trademarks of their respective owners. See `licenses/SimpleIcons.txt`; https://github.com/simple-icons/simple-icons/tree/11.15.0 . Selected logos are rasterized in white.
- **Kenney Input Prompts 1.5A**, CC0. See `licenses/KenneyInputPrompts.txt`; https://kenney.nl/assets/input-prompts . Selected PNGs are bundled under descriptive filenames.

Bundled navigation WAV files are original Pame-generated sounds. At the user's request, community PS5 recordings from https://github.com/TheBenny/es-theme-RG351V-RetroBenny-PS5 (commit 05faa0af693bdb576155d3293b60e7b6760ad93b) were installed separately in this PC's local Pame data folder. Sony sound rights are retained; those recordings are not included in the redistributable installer. Their mapping and source are recorded in `docs/PLAYSTATION_SOUND_SOURCE.json`.

## Reference-only material

PlayniteExtensions (MIT), Microsoft API documentation, and SDL/PresentMon source were inspected for architecture and interoperability. Research clones are excluded from distribution. No Playnite, DS4Windows, HidHide or ViGEm application/driver is bundled. LibreHardwareMonitor's library is now included as described above.

## Artwork and trademarks

Game titles, game/store logos, screenshots and artwork belong to their respective owners. Pame downloads metadata/artwork only for locally discovered products and caches it in the user's data directory. The installer does not include game artwork or the user's library. The optional Fortnite metadata entry points to official Epic store imagery; it does not create a game installation. Pame is not affiliated with Valve, Epic, EA, Sony, Microsoft or Nintendo.

## Development tools

xUnit, Microsoft.NET.Test.Sdk and Inno Setup are used for tests/packaging, with their own licenses. Their repositories and development installations are not part of the app. Inno's generated installer engine is used under Inno Setup's distribution terms.
