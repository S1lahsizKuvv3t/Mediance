# Beta quality gate

Mediance uses one repeatable beta gate instead of relying on a single successful desktop session.

Run the automated portion from the repository root:

```powershell
.\scripts\beta-check.ps1
```

The command builds the release configuration, runs the unit and reliability suite, executes the native WinUI smoke test, inventories the local media/model state, and writes Markdown plus JSON evidence under `artifacts/beta-check/<timestamp>`.

## Lyrics acceptance set

Open the lyrics panel and play 10-15 varied songs before rerunning the gate. Include:

- at least three source-authored synchronized tracks;
- at least four plain-lyrics tracks that require automatic alignment;
- a track started near its middle;
- a track changed while capture is active;
- Turkish and English vocals;
- one intentionally unsupported or instrumental track.

Mediance logs only attempt number, confidence, anchor count, line count, and outcome. The report never includes title, artist, lyric text, transcript, or audio. The gate stays **PENDING** until it finds the requested number of successful anonymous samples.

## Source and recovery matrix

For each available source, verify metadata, artwork, play/pause, previous/next, seek, audio output routing, and stale-state recovery:

| Source | Required check |
|---|---|
| Spotify desktop | Full control and automatic lyrics |
| YouTube Music in Chrome or Opera | Preferred-session selection and browser process routing |
| Regular browser video | Does not displace an active preferred music source |
| Local player | Metadata/control fallback when preferred sources are closed |

Then perform these recovery checks:

1. Close and reopen the selected source while Mediance remains open.
2. Put Windows to sleep during playback and verify recovery after resume.
3. Move and snap Mediance on every connected monitor, including mixed DPI setups.
4. Change songs during an automatic-sync capture and verify the old result is discarded.

These checks require the real hardware or app state. The script records them as **PENDING**, never as successful by inference.

## Soak run

For a four-hour background stability check:

```powershell
.\scripts\beta-check.ps1 -SkipBuild -SkipSmoke -SoakMinutes 240
```

The controlled soak starts the built app in background mode, checks that its process remains alive, and stops that exact build at the end. Keep normal playback and source changes going during the run for meaningful coverage.
