# Pame 0.4.4 validation

- 184 automated tests pass, including the existing 173 tests and regressions for explicit scan roots inside excluded ancestors, renamed text/DLL files, common application-name collisions, corroborated single-word game names and preservation of user history when rejecting old automatic candidates.
- Repeated a read-only scan using all real default roots, including registry installation locations: 8,348 visited folders, one candidate (the existing YGO Omega installation), no Windows tools, Chrome, EA cleanup duplicates or chess-engine helpers. Two bounded/skipped locations were reported as a partial scan; this is not a claim that every directory on the PC was read.
- Retrieved YGO Omega's actual cover, background and clear logo from the free public catalog without credentials. Classic Power of Chaos matching remains covered by tests against a controlled header-only fixture and the real LaunchBox catalog.
- The native WPF discovery probe passed again after the detector changes: classic-game import, cover/background download, transparent-logo rendering, controller focus and reachable artwork actions. The temporary executable fixture is removed at the end of the probe.

Source research, controller UI captures and broader feature validation are in [GAME-DISCOVERY.md](GAME-DISCOVERY.md) and [VALIDATION-0.4.3.md](VALIDATION-0.4.3.md).
