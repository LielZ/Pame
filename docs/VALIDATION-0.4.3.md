# Pame 0.4.3 validation

- 173 automated tests pass, covering existing behavior plus standalone discovery, title matching, multiple engines, duplicate store exclusion, hidden history preservation, cancellation, scan bounds, untrusted artwork URLs, cache reuse, no-credential artwork downloads, rate limits, XML external-entity rejection and image-path validation.
- Downloaded and parsed the real LaunchBox public metadata archive without credentials: 38,456 Windows games. Retrieved actual cover/background art for Power of Chaos: Joey the Passion and all three art types for an ELDEN RING standalone metadata entry. No games were downloaded or launched for these artwork checks.
- A controlled Desktop folder fixture named YGO Power of Chaos with joey_pc.exe is recognized through the real catalog. Tests also cover ambiguous generic filenames without assigning an invented edition.
- Read-only scans of this PC found the existing YGO Omega installation. Store duplicates, Steam download staging files and EA/SteamVR helpers were excluded after targeted checks.
- Native WPF UI probe verifies controller focus on Find games, recognition/import of the old-game fixture, cached cover/background art, a reachable Back action, no API-key controls and a rendered transparent logo. 1080p captures were visually reviewed. The fixture contains no runnable game; this is discovery/UI validation, not a playthrough.
- Installed 0.4.2 downloaded the public GitHub package, applied it automatically on next startup and relaunched as 0.4.3. Existing shell preferences, favorites and playtime were unchanged; autostart remained enabled and SQLite integrity passed.
- The full installed default-root scan exposed false positives from Windows tools, Chrome, EA cleanup programs and chess-engine helpers. The targeted earlier scan did not cover these registry-supplied roots. This finding is corrected and covered by regression tests in 0.4.4; use that release for standalone discovery.

Known limits, source requirements and scan bounds are documented in [GAME-DISCOVERY.md](GAME-DISCOVERY.md). Physical-controller endurance and arbitrary legacy-game compatibility are not claimed by these tests.
