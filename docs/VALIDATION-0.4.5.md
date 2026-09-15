# Pame 0.4.5 validation

- Automated regression coverage includes all nine non-Steam source kinds, an already-fresh legacy artwork cache, original background/logo preservation, missing matches and missing front covers, failed downloads, provider backoff, Steam exclusion, manual artwork choices and rescans retaining the separate fallback image.
- LaunchBox's actual metadata contains `r2_`-prefixed front-cover filenames that the old image-name validator rejected. Those filenames now retain the same GUID/path validation as other images. The catalog uses a v2 index, with the old index usable offline until a rebuild succeeds.
- Read-only real-library checks downloaded portrait covers for Fortnite (1440 × 2160), Battlefield 6 (1440 × 2160), EA SPORTS FC 26 (600 × 900), Split Fiction (1440 × 2160) and YGO Omega (528 × 704), without credentials. Original cover fallback paths, backgrounds and logos stayed identical. All six Steam cards stayed identical.
- The base Battlefield 6 title uses the visually checked standard front cover in its [LaunchBox Windows entry](https://gamesdb.launchbox-app.com/games/details/425895-battlefield-6), rather than the Phantom Edition variant listed earlier in the catalog.

Artwork is separate from games, saves and play history. A missing front cover retains existing artwork; no screenshot or background is selected as a LaunchBox Box - Front image.
