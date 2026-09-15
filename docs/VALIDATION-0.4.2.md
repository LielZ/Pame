# Pame 0.4.2 validation

- 143 automated tests pass, including 27 updater cases covering version ordering, alpha/stable channels, drafts/incomplete assets, repository/redirect restrictions, checksum conflicts, modified cache, cancellation, truncated/oversized responses, persistence and rate limits.
- A native WPF probe opens Settings → Updates, verifies controller-focus placement, persists the automatic-update choice, blocks checks during game preparation, and reaches the Back action without clipping. Both 1080p captures were visually inspected.
- The same probe reads the real public GitHub feed without authentication and finds the existing published release.
- The native installer helper validates the real built installer, acquires its mutex, signals readiness and honours cancellation while the owner stays open. Pame remained on 0.4.1 in this cancellation check.
- The release uses the existing per-user installer. End-to-end installation results are recorded after the public release download and local upgrade check.

No FPS, controller or game-play behavior was changed by this update. The existing hardware/overlay limitations remain documented in [KNOWN_LIMITATIONS.md](../KNOWN_LIMITATIONS.md). SHA-256 verification is not a code-signing certificate; alpha installers remain unsigned.
