# Game discovery and free artwork

Pame 0.4.3 finds extracted portable games and standalone Windows installations in addition to store-managed games. No account, subscription or API key is required for its metadata sources.

## From the couch

- **Games > Find games > Scan this PC** searches fixed drives and lets you review the results before importing them.
- **Scan a folder** targets a location or saves it for automatic discovery. Removable drives can be selected here.
- **Automatic discovery** checks common game folders, Windows installation locations, Desktop, Documents, Downloads and saved folders when the library refreshes. Desktop, Downloads and saved folders are watched for new executables and moved folders; changes trigger a debounced refresh when no Pame-managed game is active.
- **Choose a game executable** handles games that cannot be recognized automatically.
- **Game details > Manage game > Artwork & game title** lets you search either source, select the correct edition, rename a game or open its source page in Pame browser.
- Removing a standalone game hides it from future scans. Restore it through **Find games > Restore hidden games**. Game files are never removed by these controls.

An extracted `Desktop/YGO Power of Chaos/joey_pc.exe` can be matched to **Yu-Gi-Oh! Power of Chaos: Joey the Passion** by the catalog and filename words. A family folder with `game.exe` is shown as an edition that needs review: Pame does not invent which release it is. Arbitrary filenames with no recognizable folder, version information or engine data can still need manual selection. ZIP/RAR/7z files, installers and emulator ROMs are not directly imported as playable Windows games.

## Sources researched and chosen

| Source | Access | Use in Pame |
| --- | --- | --- |
| [LaunchBox Games Database](https://gamesdb.launchbox-app.com/) | Public downloadable catalog; no login or key | Primary catalog for classic and modern Windows games, alternate names, metadata, front covers, backgrounds/screenshots and clear logos. |
| [Steam library artwork](https://partner.steamgames.com/doc/store/assets/libraryassets) | Public artwork and store endpoints used without a key | Additional images and metadata where the title has a matching Steam release, regardless of where the local game came from. |
| Local game folders | Offline | `cover.png/jpg/jpeg`, `hero.png/jpg/jpeg`, `logo.png/jpg/jpeg` in the game folder or its `artwork` subfolder. |
| [SteamGridDB API v2](https://www.steamgriddb.com/api/v2) | Personal bearer API key | Researched, not integrated: Pame requires no key setup. |
| [IGDB](https://api-docs.igdb.com/) | Twitch developer credentials and OAuth | Researched, not integrated. |
| [RAWG](https://rawg.io/apidocs) | API key, plan limits and attribution | Researched, not integrated. |

LaunchBox's developer explicitly [publishes the metadata archive for external scrapers](https://forums.launchbox-app.com/topic/30123-launchbox-games-db-external-scraper/) and [explains how its image filenames are associated with database IDs](https://forums.launchbox-app.com/topic/54163-is-there-a-public-way-to-get-images-from-the-launchbox-games-database/). Pame consumes that public data directly and links back to the game's source page; LaunchBox software does not need to be installed. Artwork belongs to its respective owners. Availability through a database does not make images public domain.

The September 15, 2026 check downloaded a 107,689,581-byte archive and built an index containing **38,456 Windows games**. The initial download is about 103 MiB; Pame streams the XML on a worker thread and keeps a roughly 36 MiB Windows-only index. The archive is removed after indexing. Later searches use the local index. **Settings > Artwork & metadata > Refresh the free game catalog** updates it.

## Detection and safeguards

Detection reads executable version resources and checks for game content: Unity, Unreal packaged games, Godot sidecar packages, Ren'Py, GameMaker, RPG Maker, and game-runtime/content combinations. Catalog matching also recognizes older games that do not use these engines. It never executes a discovered file during scanning or artwork lookup.

Store game folders are excluded from standalone scanning. Helper applications, crash reporters, installers, Windows folders, developer dependency directories, store download staging folders and directory junctions are skipped. Full scans stop after two minutes or 100,000 visited folders; automatic scans use a 30,000-folder cap. Each directory is limited to 4,096 entries and traversal to 24 levels. Results indicate limits and skipped locations; selecting a narrower folder continues the search.

Automatic artwork matching accepts one exact normalized title or a known catalog ID. User-selected matches are preserved. No game is automatically launched. Files, saves, favorites and playtime remain independent of artwork matches. Multiple standalone executables in the same folder can remain separate entries.

Only the public catalog and image IDs are needed for LaunchBox matching. Steam receives game-title searches and application IDs when used as the additional source. Local file paths and game files are never sent to artwork providers. Downloads use HTTPS and restricted source hosts, size limits, image signatures, rate-limit backoff and atomic cache writes. XML external entities are disabled; archive entries are read as streams, not extracted to arbitrary paths. Failed refreshes retain the old catalog and existing art.

## Limits

There is no guaranteed detection of every Windows game. Very generic executable names, old installers, unusual engines, nonstandard folder layouts and missing dependencies may require manual setup. Detecting a game does not certify compatibility or bypass its DRM.

Some games have only a low-resolution cover or screenshots, and no separate transparent logo. Pame uses available art and keeps a text title when no logo exists. Steam's public store/CDN endpoints are not a promised stable metadata service. Offline mode uses already cached metadata and images; first-time art downloads require internet access.
