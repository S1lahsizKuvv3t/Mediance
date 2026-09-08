# Mediance motion language

Mediance uses motion to make state changes easy to follow and to give the glass widget a calm, premium character. Transitions stay short enough for daily use and avoid large travel distances that would compete with album artwork or lyrics.

## Timing and easing

| Motion | Duration | Behavior |
|---|---:|---|
| Button press | 85 ms | Scale to 91% with a quick ease-out |
| Button release | 260 ms | Return with a restrained back ease |
| Volume HUD enter | 140–260 ms | Fade, scale and six-pixel rise |
| Track content change | 280–520 ms | Fade with eight-pixel rise and 2.5% scale recovery |
| Artwork arrival | 260–480 ms | Crossfade with an 8% scale recovery |
| Lyrics line change | 180–380 ms | Directional slide and crossfade |
| Lyrics panel reveal | 220–400 ms | Fade with seven-pixel rise |
| Window reveal | 260–480 ms | Fade, rise and 3.5% scale recovery |
| Ambient color change | 900–1150 ms | Slow color blend with a restrained light pulse |
| Progress interpolation | 180 ms | Short ease between Windows timeline samples |

The widget uses quintic ease-out for spatial motion, sine easing for light and opacity, and a small back ease only when a pressed button returns to rest.

## Behavior rules

- Track and artwork transitions can be interrupted safely when sources change rapidly.
- Lyrics preserve their forward/backward direction. The outgoing group fades first and the incoming group follows after a short delay, preventing unreadable double exposure. Both layers use the same fixed left alignment.
- Seeking remains direct under the pointer; smoothing resumes after release.
- Showing the widget through the global shortcut or tray replays the reveal without taking keyboard focus.
- Windows' system animation preference is respected. When animations are disabled, every element moves directly to its final state.
- Animations do not trigger media commands, network calls or focus changes.

## Marketing capture guidance

The strongest product sequence is: reveal the glass widget, change track to show content and artwork motion, open synchronized lyrics, scrub the timeline, adjust per-app volume, then open settings. Capture at 60 fps and allow roughly one second between actions so each transition is readable.
