# Roadmap

The current feature set is intentionally close to the planned 1.0 scope. Work before the first stable release is mostly about packaging, compatibility, and recovery rather than adding another large panel to the widget.

## 0.9 public beta

- Publish a complete self-contained x64 ZIP.
- Add clean screenshots and a short desktop demo.
- Run the Windows build and test workflow on every change.
- Test installation on computers without development tools.
- Collect reproducible reports for Spotify, YouTube Music, Chrome, and Edge.
- Check Bluetooth disconnect/reconnect and audio-device removal behavior.
- Run a multi-hour media and lyrics soak test.

## 1.0 stable

- Replace the internal `AcrylicProbe` project name with the final application project name.
- Produce a versioned installer with clean install, update, and uninstall behavior.
- Sign executables and installers.
- Complete Windows 11 scaling checks at 100%, 125%, 150%, and 200%.
- Complete keyboard navigation, screen-reader labels, high contrast, reduced motion, and transparency-off checks.
- Add a privacy-safe diagnostics bundle for support requests.
- Freeze the settings and manual-timing migration contract.
- Publish final release notes, checksums, privacy text, and third-party notices.

## After 1.0

These ideas are useful, but they should not delay a stable first release:

- import and export for settings and manual timings;
- a local manual-timing manager;
- per-track lyric offset overrides;
- packaged-app/AUMID audio routing;
- more explicit media-session selection;
- docking and optional auto-hide;
- automatic update checks with a clear user choice;
- additional localizations.

The roadmap can change based on beta feedback. Features that weaken privacy, change the system-wide output device, or require media-account passwords are outside the current product direction.
