# Pame 0.2.1 performance and sound validation

13 September 2026, Windows 11 Pro build 26340, Ryzen 7 7800X3D, RX 9070, primary display 3840 x 2160 at 60 Hz and 144 DPI (150%). The SudoMaker Virtual Display Adapter is installed but has no active resolution. It must not decide rendering for the active Radeon display.

## Navigation measurements

`BenchmarkUi.cs` runs the same six-page sequence twice, changes focus between seven game cards three times, and opens/closes the performance dialog. The isolated database contains the discovered library and a lifecycle fixture; the fixture is never added to the normal user library.

| Build / mode | Median composition interval | P95 interval | Slowest synchronous page action |
|---|---:|---:|---:|
| Original 0.2 UI, software | 67.8 ms | 203.4 ms | 45.2 ms |
| Original 0.2 UI, GPU enabled | 16.7 ms | 33.1 ms | See raw report |
| Improved UI, GPU enabled | 16.9 ms | 33.9 ms | 16.9 ms |

Raw reports and reference images: `artifacts/ui-v02/benchmarks/baseline-software-4k.*`, `baseline-hardware-4k.*`, `improved-hardware-4k.*`. These are WPF CompositionTarget event intervals under this navigation workload, not direct present times or a promise of fixed 60 FPS. Total CPU time in these reports includes a software PNG export and must not be presented as idle overhead.

An actual native-window screenshot confirmed that hardware rendering produces the complete fullscreen UI on the active RX 9070. The previous blanket software override was unnecessary on this display. Source review and reference images verify that focus no longer applies a bitmap shadow or scale transform to text. The image cache distinguishes decoded sizes, so a card-sized bitmap cannot replace the full-resolution hero.

## Sound

Five mapped community PS5 recordings were installed in `%LOCALAPPDATA%\Pame\sounds\PlayStation`. The selected pack is PlayStation, enabled at 40% menu volume. Provenance and pinned commit are in `PLAYSTATION_SOUND_SOURCE.json`. They remain local user assets; the installer contains only the six original Pame cues. Missing pack cues fall back to the bundled sounds.

The audio worker decodes/resamples outside the UI thread, keeps one output device, trims capture pre-roll, bounds overlapping voices and discards late requests. Identical-instant click/page actions are debounced. Playback stops after the final cue. No audio initialization or playback error was logged in the navigation runs; perceived sound and speaker latency still require listening on the physical output.

## Regression coverage

The existing 52 tests plus an upgrade regression verify that legacy `SoftwareRendering: true` does not force the new renderer into compatibility mode, while unrelated saved preferences survive. Native game/profile/Wi-Fi/telemetry coverage and remaining hardware limitations are documented in `VALIDATION-0.2.md` and `KNOWN_LIMITATIONS.md`.


## Final installed verification

All 53 tests passed and the release build completed with zero warnings/errors. The final smoke run with GPU acceleration exported all six pages and updated dialogs; the quick menu final-action viewport assertion passed. The wireless DualSense still reported player 1 and approximately 95% battery.

The self-contained 0.2.1 installer completed with exit code 0. The installed Pame.dll matched the published payload, the executable version was 0.2.1.0, and the normal database SHA-256 was unchanged across installation. The normal library retained 10 installed games. PlayStation remained selected at 40% menu volume with five local WAV files; only the six original cues are bundled.

A repeat of the navigation benchmark using the installed release returned Default rendering, tier 2, 144 DPI, median interval 16.668 ms, P95 33.898 ms and slowest synchronous page action 15.356 ms. Focus changes took 0.286-0.497 ms synchronously. Raw results: `artifacts/ui-v02/benchmarks/release-021-hardware-4k.json`. No new audio error was logged. This remains a composition-event benchmark rather than display-present FPS.
