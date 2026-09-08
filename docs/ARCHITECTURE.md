# Architecture

Mediance is a native Windows desktop application built with C#, .NET 10, WinUI 3, and Windows App SDK. The code is split so that Windows-specific integration stays at the edge while selection, timing, persistence, and matching rules remain testable without a desktop session.

## Solution map

```text
Mediance.AcrylicProbe
  ├─ presentation and view models
  ├─ Desktop Acrylic window and settings companion
  └─ application composition
          │
          ├──────────────┐
          ▼              ▼
Mediance.Windows     Mediance.Lyrics
  │                      │
  └──────────────┬───────┘
                 ▼
           Mediance.Core
```

### Mediance.Core

Contains platform-independent rules and contracts:

- immutable media session snapshots;
- source selection and Spotify/YouTube Music priority;
- playback timeline interpolation;
- seek and progress calculations;
- LRC parsing and active-line lookup;
- lyrics matching and cleanup;
- settings models and atomic JSON storage;
- window drag and edge-snap geometry.

### Mediance.Windows

Owns operating-system integration:

- Global System Media Transport Controls session discovery;
- media commands and artwork reads;
- Core Audio endpoint and application-session enumeration;
- per-application persisted audio endpoint routing;
- global hotkey registration;
- system tray icon and menu;
- single-instance activation;
- monitor work areas and native window behavior.

The audio-policy COM adapter is isolated here because its ABI is not part of the normal portable application model. It never changes the system-wide default endpoint.

### Mediance.Lyrics

Owns network providers and local manual timing storage:

- LRCLIB LRC;
- Better Lyrics TTML;
- AMLL TTML;
- Apple Music TTML after iTunes catalogue validation;
- validated plain-text fallbacks;
- per-provider deadlines and fallback order;
- bounded memory-only result caching;
- privacy-preserving local manual timing.

Plain text is never presented as synchronized. Source-authored LRC or TTML always wins over a local manual timing.

### Mediance.AcrylicProbe

This is the current desktop host despite its historical project name. It contains the production-facing widget, settings window, animations, localization resources, and view models. The assembly and executable are named `Mediance`.

The project name will be simplified before 1.0 packaging so release users do not see internal prototype terminology.

## Media state flow

Windows media notifications can arrive in bursts. Event handlers only signal a bounded channel; one worker serializes native refreshes and publishes immutable snapshots. A revision check prevents an asynchronous metadata read from replacing a newer state.

```text
Windows GSMTC event
        ↓
coalesced refresh signal
        ↓
serialized native read
        ↓
immutable MediaSnapshot
        ↓
PlayerViewModel on UI dispatcher
```

The widget does not poll Windows for every progress-frame update. `TimelineClock` advances the last native position with a monotonic local clock and reconciles when a real timeline or playback event arrives.

A low-frequency 15-second health pass covers missed notifications and attempts to reconnect if the Windows session manager becomes invalid after sleep, resume, or a source application restart.

## Session selection

Live session IDs identify one native session and are never persisted. Selection follows this order:

1. an explicit live pin;
2. Spotify;
3. a browser music session such as YouTube Music;
4. the Windows current session;
5. a playing or first available fallback.

Normal browser video is not assigned music priority. Sessions with the same application ID remain separate when Windows exposes them separately.

## Artwork

Artwork is read on demand, bounded to 8 MB, decoded at widget size, and discarded when the track changes. A version token prevents a late image from being published for a newer song. Short startup failures are retried, and later snapshots can retry an artwork item that was not ready during the first metadata read.

Artwork is used to derive the optional ambient color. It is not written to disk.

## Lyrics flow

The lyrics view model starts a lookup only when the panel is open. Track changes and panel closure cancel the active request. Each remote source has its own deadline, while the complete chain has a larger deadline so a stalled endpoint cannot block all fallbacks.

Recent results are kept in a bounded in-memory cache. Successful documents live longer than unavailable results; no query or lyrics text is written as part of this cache.

Manual timing uses a versioned atomic file under local application data. Track and lyric identities are one-way fingerprints. A damaged main file is preserved and the last complete backup is restored when possible.

## Settings and window state

The main widget and settings window share one settings view model. Changes apply immediately and are saved with a short debounce. Persistence uses a temporary file followed by an atomic replace, keeping the previous complete version as `.bak`.

Unknown future schema versions are not overwritten. Malformed current files are preserved as `.invalid`; a valid backup is restored before falling back to defaults.

Window positions are stored per monitor identity and clamped to an available work area after a display-layout change.

## Shutdown

The shutdown path removes event subscriptions, cancels lyrics and artwork work, stops media health tasks, unregisters the global hotkey, removes the tray icon, disposes the Acrylic controller, and flushes settings before closing the final window.

## Testing

Portable behavior is covered by xUnit. Native smoke tests exercise window creation, Acrylic activation, topmost changes, monitor enumeration, settings-window lifecycle, layout reflow, hotkey registration, and clean shutdown.

Manual acceptance remains necessary for audible routing, physical pointer feel, system visual policies, multiple real media applications, and hardware/device changes.
