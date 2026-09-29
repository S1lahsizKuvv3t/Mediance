# Roadmap

Mediance 1.0 is a stable portable release. It contains the desktop widget, Album theme, Micro mode, taskbar capsule, local lyrics library and on-device alignment. Stable does not mean every recording, audio device or Windows configuration has been verified.

## Next reliability work

- Extend real hardware checks across display scaling, taskbar auto-hide and secondary displays.
- Run longer sleep/wake, player-restart and multi-hour listening tests.
- Expand accessibility checks: keyboard navigation, screen readers, high contrast and reduced motion.
- Improve automatic alignment coverage while keeping confidence gates and saved timings intact.
- Make support diagnostics easy to share without including listening metadata or lyrics.

## Distribution

The portable ZIP remains the supported package. An installer will follow when the update rhythm settles. Code signing and a controlled update mechanism need their own release checks; 1.0 does not silently install updates or turn off SmartScreen.

## Possible later features

- Per-track timing offsets.
- Better handling of ambiguous metadata and unusual recordings.
- More audio-device compatibility.

New features should keep the small widget and focused settings easy to use. GitHub Issues is the place for reproducible bugs and concrete feature requests.
