# Media feasibility — Phase 0A

Uses `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, collection/current-session notifications and session metadata/playback/timeline events.

Selection policy:

1. Keep the explicitly pinned session while present, including while paused.
2. Otherwise prefer a Spotify session while one exists.
3. If Spotify is absent, prefer a YouTube Music/browser music session. The GSMTC playback type distinguishes browser music from ordinary YouTube video.
4. If neither preferred music source exists, follow the valid Windows current session.
5. If Windows reports no current session, prefer a playing session, then the first session, then idle.
6. A disappeared pin is cleared. Separate sessions from the same source remain independently selectable.

Within multiple preferred candidates, the Windows current candidate wins, followed by a playing candidate and then the first candidate. A regular browser video never receives music-source priority.

`GetCurrentSession()` and `GetSessions()` returned different native objects for the same Spotify source on this machine. Managed equality and canonical IUnknown identity both differed; therefore the original `current=none` output did not prove that Windows returned null. The adapter now tries exact equality first, then matches by source application ID only if exactly one listed session has that ID. Multiple same-source sessions remain separate, and an ambiguous match displays `current=unresolved` with the current source. The selected fallback/pin remains visible independently. Actual null from Windows still displays `current=none`.

Commands read native capabilities again immediately before execution. Unsupported commands, invalid seek positions, rejected requests, timeouts and native failures have separate outcomes. `Try*` success is an acknowledgement; manual testing must verify that the media actually changes.

Artwork is read on demand, limited to 8 MiB and never decoded in the console. A changed event revision discards the read. Production will add display-resolution decoding and caching.

## Manual matrix

For Spotify, Chrome/YouTube and Edge, run the interactive probe and verify discovery, current source, title/artist/album, artwork, play/pause, previous/next, seek and source removal. Record unsupported capabilities as such. Start a second source, pin the first and ensure it remains selected. Close the pinned source and verify follow/idle recovery.

`scripts/dev.ps1 watch -Seconds 30` observes events without issuing commands. Clean exit after the timeout checks event unsubscription. A stable watch without changes does not prove delivery of change events.

## Verified sources

- [GSMTC session manager](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager?view=winrt-26100)
- [Session methods and events](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession?view=winrt-26100)
- [Capability properties](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionplaybackcontrols?view=winrt-26100)
- [Timeline properties](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties?view=winrt-26100)
