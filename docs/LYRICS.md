# Lyrics — Phase 0D and Phase 6

The Phase 0D prototype is implemented. Every network source is isolated behind `ILyricsProvider` / `ILyricsService` and composed by `FallbackLyricsProvider`.

Implemented:

- exact title/artist lookup with optional album and rounded duration through LRCLIB's `/api/get` endpoint;
- a conservative `/api/search` fallback when exact lookup returns 404, scored by title (40%), artist (35%), duration (20%) and album (5%);
- a confidence threshold and ambiguity margin; uncertain matches remain unavailable instead of showing another song's lyrics;
- a 20-second complete lookup deadline, separate 3–4 second source deadlines and cancellation when the selected track changes or the lyrics panel closes;
- synchronized LRC parsing, including multiple timestamps, millisecond fractions and signed offsets;
- binary-search active-line selection that immediately follows seeks;
- persisted two-line (current/next) or three-line (previous/current/next) synchronized layouts, instrumental and unavailable states;
- plain-text-only results are never shown as synchronized lyrics and no estimated timestamps are fabricated;
- when every synchronized source misses but an identity-validated plain lyric is available, the panel offers manual timing: playback seeks to the beginning and each line is marked with the main button or Space;
- restart and cancel controls keep incomplete attempts out of storage. The final mark saves immediately and switches to the normal synchronized renderer;
- user-timed results expose `Synchronize again`; cancelling a redo restores the previous working timing, while completing it atomically replaces that entry;
- completed timing persists atomically under local app data and reloads across application restarts. The versioned file is capped at 500 entries and stores only SHA-256 track, lyric and per-line fingerprints plus duration/timing metadata; it contains no title, artist or lyric text;
- if the timing file is malformed after an interrupted write, the damaged copy is preserved and the last complete `.bak` file is restored automatically; invalid entries in a readable file are skipped individually;
- stored timings require the stable normalized title/artist identity, compatible duration, matching line count and either the full lyric fingerprint or at least 85% matching positional line fingerprints. This prevents provider punctuation or formatting changes from losing a valid timing;
- source-authored LRC/TTML always wins over a stored manual timing;
- the runtime provider chain queries LRCLIB LRC, Better Lyrics TTML, AMLL TTML and Apple Music TTML; each provider is isolated so an HTTP failure advances to the next source;
- Apple Music candidates are validated through the public iTunes song/artist catalogue. A TTML document marked as untimed, or one without timed paragraphs, is rejected;
- LRCLIB exact plain-text hits continue into catalogue search so a timestamped duplicate wins, and a plain result from any provider is held only as a fallback while later synchronized providers are checked;
- a 320 ms directional slide/crossfade between timed lines; reverse seeks animate in the opposite direction and interrupted transitions settle cleanly;
- fixed-height slots so line length does not make either selected layout jump in height;
- click-to-seek on each visible previous/current/next line using that LRC line's timestamp;
- mouse-wheel browsing that pauses automatic line following, keeps the three-line layout and restarts a 3.5-second idle timer;
- animated auto-snap to the actual playback line after that timer expires;
- playback interpolation compensates for the age of Windows' timeline snapshot before applying local monotonic time;
- a persisted lyrics timing control from 2 seconds late to 2 seconds early, with a 0.5-second-early default;
- an explicit `Şarkı sözleri` button in the widget plus a show/hide setting for that button;
- a diagnostic probe that reports only result type and timing counts, never lyric text.
- identity validation uses title, artist, album and duration where a catalogue exposes those fields; uncertain matches remain unavailable;
- bracketed structural labels such as `[Nakarat]`, `[Verse]`, `[Köprü]` and their numbered/credited forms are removed centrally before either flowing or plain display, while vocal ad-libs remain;
- provider failure isolation, so a blocked or unavailable source advances to the next source without affecting media controls;
- a bounded memory-only result cache: successful documents live for four hours and unavailable results for 45 seconds, making repeated panel opens instant without persisting queries or lyrics;

Verified against the real services on 2026-09-08:

- `The Chain — Fleetwood Mac`: synchronized, 43 timed lines;
- `Numb — Linkin Park`: synchronized, 36 timed lines;
- `Blinding Lights — The Weeknd`: synchronized, 40 timed lines;
- `Gidersin Araya — Cash Flow`: exact lookup missed because Spotify reported about 146 seconds while the matching LRCLIB record reported 150; scored fallback selected the exact title/artist/album record and returned 60 synchronized lines;
- an unavailable result returns cleanly without blocking media controls.
- `58 — KAVAK`: synchronized, 33 timed lines;
- `TSS — KAVAK` with its Spotify album/duration metadata: synchronized, 45 timed lines;
- `60 — KAVAK, BAKAN`: synchronized through Apple Music TTML, 43 timed lines;
- the reported `fıs? — KAVAK, BAKAN` probe has no source-authored timestamps, but its identity-validated plain lyrics now open the manual timing workflow;
- SyncLRC was evaluated as another aggregator, but its public endpoints returned HTTP 403/429 to application requests on 2026-09-08, so it was not added as an unreliable runtime dependency.

API references: [LRCLIB](https://lrclib.net/docs), [Better Lyrics](https://lyrics-api-docs.boidu.dev/), [AMLL TTML API](https://github.com/amll-dev/amll-ttml-api), [iTunes Search API](https://performance-partners.apple.com/search-api) and [Lyrically Apple Music endpoint](https://lyrics.paxsenix.org/docs).

Phase 6 still adds deeper metadata cleanup, per-track offsets and further graceful-failure polish. HTTP failures and individual source stalls never delay media controls.

Lyrics are requested only when the user opens the panel. Do not commit lyrics or listening-history fixtures and do not write lyric text or query metadata to diagnostic logs. Unit tests use short synthetic timestamped lines written for the tests.
