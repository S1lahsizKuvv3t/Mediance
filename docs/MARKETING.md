# Mediance marketing kit

The files under `assets/marketing` are rendered from the real WinUI application. They are intended for the GitHub README, release pages, social posts, and store drafts.

## Ready-to-use assets

| File | Recommended use |
|---|---|
| `Mediance-feature-tour.gif` | README hero, release post, short product preview |
| `Mediance-screenshot-sheet.png` | One-image overview for posts and issue discussions |
| `widget.png` | Standard widget |
| `widget-album.png` | Album artwork theme |
| `widget-lyrics.png` | Synchronized lyrics |
| `widget-micro-idle.png` | Micro mode at its idle opacity |
| `widget-micro-hover.png` | Micro mode while hovered |
| `widget-minimal.png` | Minimal layout |
| `settings-*.png` | Appearance, elements, lyrics, and system settings |

Regenerate the pack from the repository root after a visible UI change:

```powershell
.\scripts\build-marketing-pack.ps1
```

The script first runs the native preview smoke path, renders only Mediance's own XAML surface, and then creates the GIF and contact sheet. It does not capture the desktop or move the pointer. Pass `-Python <path>` when Pillow is installed in a non-default Python environment.

## Product copy

**Short:**

Mediance is a compact Windows 11 media companion with synchronized lyrics, per-app audio routing, flexible layouts, and a low-profile desktop widget.

**Release-page paragraph:**

Control the media session you actually care about, follow synchronized lyrics, and send that app to another output device without changing the Windows default. Mediance stays out of the taskbar and Alt+Tab, supports compact and lyrics-focused layouts, and keeps automatic lyric analysis on the device.

## Live video shot list

Record at 60 fps in a clean Windows 11 desktop session:

1. Reveal Mediance with its global shortcut.
2. Change tracks and hold for the artwork transition.
3. Open lyrics and let two line changes complete.
4. Scrub the progress bar once.
5. Change the per-app output device.
6. Switch to Album theme.
7. Enter Micro mode, move the pointer away, then hover it again.
8. Reopen the full widget and show the settings search.

Keep the finished cut between 18 and 25 seconds. Leave roughly one second after each action so the motion remains readable. Export a 1080p H.264 MP4 for release pages and a shorter GIF only where video is unavailable.
