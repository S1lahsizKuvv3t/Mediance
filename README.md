<p align="center">
  <img src="assets/brand/Mediance-mark-master.png" width="128" alt="Mediance logo">
</p>

<h1 align="center">Mediance</h1>

<p align="center">
  A compact media companion for Windows 11.<br>
  Control playback, follow synchronized lyrics, and route an app to a different audio device without leaving your desktop.
</p>

<p align="center">
  <a href="README.tr.md">Türkçe</a> ·
  <a href="docs/INSTALLATION.md">Install</a> ·
  <a href="docs/WALKTHROUGH.md">Walkthrough</a> ·
  <a href="docs/FAQ.md">FAQ</a>
</p>

> The first public beta is available from [GitHub Releases](https://github.com/S1lahsizKuvv3t/Mediance/releases/tag/v0.9.0-beta.1). Signed installers and automatic updates are still on the release checklist.

## What it does

Mediance sits on the desktop as a small Acrylic widget. It reads the media sessions already exposed by Windows, so it can work with Spotify, YouTube Music, browsers, and other compatible players without asking for an account or a browser extension.

- Play, pause, skip, and seek from one floating window.
- Prefer Spotify and YouTube Music over an unrelated browser video.
- Show synchronized lyrics with smooth line transitions and an adjustable timing offset.
- Create and keep local timings when only plain lyrics are available.
- Choose a different output device for the selected media app without changing the system-wide default.
- Hide individual parts of the widget and tune its width, artwork, text, controls, glass density, and theme.
- Pin the widget above other windows, lock its position, snap it to either monitor, or hide it in the system tray.
- Show or hide it with a configurable global shortcut.

Mediance does not keep listening history or send telemetry. Lyrics are fetched only after the lyrics panel is opened. See the [privacy notes](docs/PRIVACY.md) for the exact data flow.

## Requirements

- Windows 11 24H2 or newer
- x64 processor
- A media application that publishes a Windows media session

The release build is self-contained. Users do not need to install the .NET SDK or Windows App SDK separately.

## Install a beta build

1. Download `Mediance-<version>-win-x64.zip` from GitHub Releases.
2. Extract the whole archive to a normal folder.
3. Run `Mediance.exe` from that folder.

Do not move only the EXE out of the extracted folder; the adjacent runtime files are part of the application. Until releases are code-signed, Windows SmartScreen may show an unknown publisher warning.

The full instructions, update notes, and clean removal steps are in [Installation](docs/INSTALLATION.md).

## Using Mediance

Start music in a supported app and open Mediance. The widget follows the preferred session automatically. Drag it from any empty part of the glass surface, use the footer lock when it is in place, and open `settings` to change the layout.

The audio output selector affects the selected application only. Choosing **Default** returns that app to the Windows default device. Browser routing applies to the browser process rather than one individual tab.

Open `lyrics` to request lyrics for the current track. Source-authored synchronized lyrics always take priority. When only verified plain lyrics are available, Mediance offers an optional timing mode that stores the completed timestamps locally.

The [walkthrough](docs/WALKTHROUGH.md) covers every control and setting. Common questions are collected in the [FAQ](docs/FAQ.md).

## Build from source

You need Windows 11 and the .NET 10 SDK.

```powershell
git clone <your-fork-url>
cd Mediance
dotnet restore Mediance.slnx
dotnet build Mediance.slnx --configuration Release
dotnet test tests/Mediance.Core.Tests --configuration Release
```

The repository also contains a development helper:

```powershell
.\scripts\dev.ps1 build
.\scripts\dev.ps1 test
.\scripts\dev.ps1 glass-test
```

Build output is written under `artifacts/` by the helper and is intentionally excluded from Git.

## Project layout

- `src/Mediance.Core` — portable media, settings, timing, and selection logic.
- `src/Mediance.Windows` — Windows media sessions, audio routing, and native window integration.
- `src/Mediance.Lyrics` — synchronized and plain lyrics providers, caching, and local timing storage.
- `prototypes/Mediance.AcrylicProbe` — the current WinUI 3 desktop application.
- `tests/Mediance.Core.Tests` — unit and reliability tests.
- `docs` — user guides and technical notes.

More detail is available in [Architecture](docs/ARCHITECTURE.md).

## Current status

The current build passes 85 automated tests and the native window smoke test. Spotify playback controls and per-app output routing have also been checked on real hardware. The remaining work before a signed 1.0 release is tracked in the [roadmap](docs/ROADMAP.md).

Please use the issue templates for reproducible bugs and feature requests. For security reports, follow [SECURITY.md](SECURITY.md) rather than opening a public issue.

Repository owners can follow the one-time [GitHub setup checklist](docs/GITHUB_SETUP.md) before the first push.

## License

Copyright © 2026 Mediance. All rights reserved. No permission to redistribute modified builds or reuse the source is granted by the current license. This can be changed before the public launch if the project moves to an open-source license.

Third-party components and references are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
