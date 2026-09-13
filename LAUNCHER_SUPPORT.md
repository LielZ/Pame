# Launcher support

| Store | Discovery | Launch | Uninstall / management |
|---|---|---|---|
| Steam | HKCU install root, current and legacy libraryfolders, installed ACF manifests on all libraries | `steam://rungameid/<id>` | Native `steam://uninstall/<id>`; verify via `steam://validate/<id>` |
| Epic | ProgramData `.item` manifests, incomplete/non-application entries excluded | Escaped catalog namespace:item:app URI | Open Epic; no undocumented deletion command |
| GOG | Both registry views of GOG game keys | Manifest-registered contained executable and arguments | Open GOG / Windows installed apps |
| Ubisoft | Ubisoft Launcher Installs keys | `uplay://launch/<id>/0` | Open Ubisoft |
| EA | Uninstall registrations with EA publisher, installed folder and contained icon executable | Discovered game executable; EA handles DRM | Open EA |
| Battle.net | Conservative Blizzard uninstall registration | Contained game executable | Open Battle.net |
| Rockstar / Riot | Conservative publisher registrations | Contained game executable | Open store |
| Xbox | `XboxGames/<game>/Content/MicrosoftGame.config`; Xbox store package detection | Contained registered executable | Open Xbox / Windows installed apps |
| Standalone | User explicitly selects `.exe` in controller file browser | Selected executable | Remove library record or open Windows installed apps |

Stores are separate records and pages, never game cards. Discovery does not traverse arbitrary EXEs. Steam redistributables, SteamVR, SDKs, installers and launchers are filtered. Missing drives retain history but do not show games as installed until rescanned.

Steam native artwork is preferred, including hashed cache subdirectories and the newer `library_capsule` filename. Steam app details are cached locally; non-Steam products can obtain metadata only from a unique exact normalized title match. That match never changes launch ownership. Fortnite has a locally discovered product-specific official Epic metadata/artwork entry. Other unsupported products use honest empty metadata/artwork states.

Steam local playtime is imported once from the most recently active local account configuration. Accounts are not summed. Pame then tracks its own sessions. Rescans preserve favorites, profiles and history.

Steam manifests expose pending updates and received/total byte values. Pame rereads these files every five seconds while Downloads is open and labels values as received bytes, not measured network throughput. Pause/resume, queues and other stores use an open-store handoff. Stores are not auto-closed because cloud save completion generally cannot be proven.

Verified on this machine: Steam on C: and D:, Epic, EA and Xbox store detection; ten installed games normalized without store/tool records in the 0.2 scan. One corrupt Epic file is skipped with a local log entry. The original Steam launch opened Steam's login screen; full Steam gameplay remains unverified.
