# Changelog

Notable user-visible changes are recorded here. Mediance follows semantic versioning once public release tags begin.

## [Unreleased]

### Added

- Album theme uses the current artwork as a smoothly animated widget background.
- Plain-only lyrics can now be synchronized automatically on-device. Mediance captures only the selected media process, transcribes it with a local Whisper model, aligns the independent transcript to the verified lyric text, rejects low-confidence matches, and keeps accepted timings locally.
- Settings includes a persistent automatic lyrics sync switch. The multilingual model downloads on first use instead of increasing the release archive size.

### Changed

- Crop Album theme artwork from its center to fill the full widget surface at both compact and expanded sizes without a visible square seam.
- The widget no longer takes foreground activation from games and other applications.
- Mediance windows are hidden from the Windows taskbar and Alt+Tab switcher.

### Fixed

- Plain lyrics no longer stay cached for hours after a synchronized provider temporarily misses. The open lyrics panel automatically retries and upgrades to source-authored timing when it becomes available.

## [0.9.0-beta.2] — 2026-09-09

### Changed

- The Windows ZIP now has a clean top level containing only `Mediance.exe`.
- Application runtime files and language folders are grouped under `App`; license and package notes are grouped under `Documentation`.
- A small native launcher starts the self-contained application without requiring a separate .NET installation.

## [0.9.0-beta.1] — 2026-09-09

### Added

- GitHub-facing README files, user walkthrough, FAQ, installation, troubleshooting, privacy, support, architecture, roadmap, and release checklist.
- Contribution, security, conduct, issue, pull-request, and CI templates.

### Changed

- Lyrics lookups now use per-provider deadlines and a bounded memory-only cache.
- Windows media sessions receive a low-frequency recovery pass after missed events or source restarts.
- Artwork can retry when a source publishes its thumbnail after the initial media snapshot.

### Fixed

- Damaged settings and manual timing files can recover the last complete backup.
- Invalid entries in a readable manual-timing file no longer prevent other saved timings from loading.

This is the first public beta of the floating Acrylic widget. It includes universal media controls, synchronized and manually timed lyrics, per-application audio routing, a configurable global shortcut, system tray support, multi-monitor placement, themes, and persistent settings.
