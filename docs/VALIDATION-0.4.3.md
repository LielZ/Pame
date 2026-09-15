# Pame 0.4.3 validation

- 173 automated tests pass, covering existing behavior plus standalone discovery, title matching, multiple engines, duplicate store exclusion, hidden history preservation, cancellation, scan bounds, untrusted artwork URLs, cache reuse, no-credential artwork downloads, rate limits, XML external-entity rejection and image-path validation.
- Downloaded and parsed the real LaunchBox public metadata archive without credentials: 38,456 Windows games. Retrieved actual cover/background art for Power of Chaos: Joey the Passion and all three art types for an ELDEN RING standalone metadata entry. No games were downloaded or launched for these artwork checks.
- A controlled Desktop folder fixture named YGO Power of Chaos with joey_pc.exe is recognized through the real catalog. Tests also cover ambiguous generic filenames without assigning an invented edition.
- Read-only scans of this PC found the existing YGO Omega installation. Store duplicates, Steam download staging files and EA/SteamVR helpers were excluded after targeted checks.
- Native WPF UI probe verifies controller focus on Find games, recognition/import of the old-game fixture, cached cover/background art, a reachable Back action, no API-key controls and a rendered transparent logo. 1080p captures were visually reviewed. The fixture contains no runnable game; this is discovery/UI validation, not a playthrough.
- Full per-user installer and installed-version upgrade verification use the existing GitHub updater. See the 0.4.2 validation for its first installed upgrade; final 0.4.3 application checks are recorded after publishing.

Known limits, source requirements and scan bounds are documented in [GAME-DISCOVERY.md](GAME-DISCOVERY.md). Physical-controller endurance and arbitrary legacy-game compatibility are not claimed by these tests.
