# Release checklist

## Repository

- [ ] Public files contain no local paths, credentials, personal information, or internal work notes.
- [ ] `git status --ignored` confirms build outputs, local tools, logs, settings, and backups are excluded.
- [ ] README screenshots match the release build.
- [ ] License and third-party notices have been reviewed.
- [ ] Changelog contains the final version and date.

## Build and validation

- [ ] Clean restore succeeds on Windows.
- [ ] Release solution build has no warnings or errors.
- [ ] All automated tests pass.
- [ ] Native window smoke test passes.
- [ ] ZIP is tested on a Windows 11 machine without the development environment.
- [ ] Spotify and YouTube Music playback controls are checked.
- [ ] Per-app audio routing is checked with at least two output devices.
- [ ] Lyrics, manual timing, settings persistence, tray, startup, and hotkey are checked.
- [ ] Sleep/resume, source restart, device removal, and multiple-monitor scenarios are checked.

## Package

- [ ] Version is consistent in the application, archive name, tag, and changelog.
- [ ] Package contains the complete self-contained `release_win-x64` output.
- [ ] Package does not contain logs, settings, tests, source files, or signing keys.
- [ ] Executable and installer are signed when signing is available.
- [ ] SHA-256 checksum is generated after the final archive is created.

## GitHub release

- [ ] Tag uses the form `v0.9.0-beta.1` or `v1.0.0`.
- [ ] Release notes describe changes, installation, known issues, and upgrade behavior.
- [ ] ZIP and checksum file are attached.
- [ ] The published archive is downloaded once and verified independently.
- [ ] Relevant issues are closed or linked to the next milestone.
