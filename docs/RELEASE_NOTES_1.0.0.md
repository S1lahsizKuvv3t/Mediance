# Mediance 1.0.0

The first stable portable release brings the desktop widget, taskbar capsule and saved lyrics into one package. Download the Windows ZIP from [Releases](https://github.com/S1lahsizKuvv3t/Mediance/releases/tag/v1.0.0), extract it completely and launch the top-level `Mediance.exe`.

## Saved lyrics that stay saved

Completed automatic and manual synchronizations now include the lyric text as well as every timestamp. Mediance checks the local library before making a provider request. Reopening a saved track works without another lookup or audio analysis, including after restarting the application. Source-synchronized lyrics are saved too.

Existing timing-only records are preserved. They need one successful text lookup to become complete offline entries; their original timings are retained. Different artists and recordings with substantially different durations are kept separate. Saved entries do not expire or get silently removed after a fixed song count.

## A capsule that fits the taskbar

The capsule uses the taskbar's monitor DPI and centers within the reserved taskbar band. Opening Start cannot send it above the taskbar. If Windows temporarily removes the taskbar, the capsule hides until it returns. The play/pause button fits inside its surface, and hover no longer enlarges content beyond the rounded window.

## Included since Beta 3

- Lyrics lookup for Spotify episodes, including guarded title-only searches when artist metadata is missing.
- Concurrent provider lookup and a retry for temporary misses.
- An optional artwork-based capsule with play/pause and a context menu.
- Album, Micro, artwork-and-controls and vertical lyrics layouts.
- Searchable settings, on-device automatic synchronization and manual timing as a fallback.
- Media-state recovery after stale notifications, source restarts and sleep.

## Compatibility and limits

Windows 11 24H2 or newer, x64. The application runtimes are included. This is a portable ZIP; an installer and code signing are planned separately. SmartScreen may still report an unknown publisher.

Automatic alignment cannot guarantee accurate timing for every recording. Mediance retains its confidence gate instead of presenting an uncertain result as synchronized. Full-screen applications hide the capsule. Third-party taskbar replacements and the full accessibility, device and sleep/wake matrix remain areas for additional testing.

Public screenshots are rendered from the real WinUI interface using original demo metadata, artwork and lyrics. No user's listening state is included.
