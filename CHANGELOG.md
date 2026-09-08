# Changelog

Notable user-visible changes are recorded here. Mediance follows semantic versioning once public release tags begin.

## [Unreleased]

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

## [0.9.0-beta.1] — planned

First public beta candidate with the floating Acrylic widget, universal media controls, synchronized and manually timed lyrics, per-application audio routing, configurable global shortcut, system tray, multi-monitor placement, themes, and persistent settings.
