# Mediance - next beta

This beta focuses on reliability, automatic lyric timing, and a cleaner daily-use experience.

## Highlights

- Automatic lyrics can now capture only the selected media application's audio, transcribe it locally, align verified plain lyrics, learn from high-confidence anchors, and save the timing for later playback.
- Fixed an asynchronous process-loopback shutdown bug that could discard a completed capture before transcription.
- Added progress for model download, audio capture, transcription, alignment, and local saving.
- Added Micro, cover-and-controls, and vertical lyrics layouts.
- Reworked settings into focused categories with search.
- Added Album theme blur, zoom, and darkness controls.
- Added a media-state watchdog for stale sessions, app restarts, and wake-from-sleep recovery.
- Added local timing management and an anonymous live lyrics acceptance runner.

## Validation

- 97 automated tests pass.
- Native WinUI smoke coverage passes.
- 12 of 12 source-timed Spotify samples loaded synchronized lyrics.
- 4 of 4 forced automatic-path Spotify samples passed confidence checks and saved local timing.
- The acceptance reports contain no title, artist, lyric text, transcript, or captured audio.

## Known beta limitations

- The first automatic lyric sync downloads a local multilingual model of about 488 MB.
- Automatic timing is intentionally rejected when the audio-to-text match is weak.
- Releases are not yet code-signed, so Windows SmartScreen may show an unknown publisher warning.
- A full installer and automatic updater remain planned for the 1.0 release.
