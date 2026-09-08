# Window behavior — Phase 0B / 0E gates

Phase 0B has a native working prototype in `prototypes/Mediance.AcrylicProbe`. Phase 0E adds a registered, configurable global hotkey that hides the widget and restores it without activation.

Phase 0B: create a minimal native WinUI 3 borderless window with DesktopAcrylicBackdrop, explicit drag regions, rounded window appearance and immediate topmost toggle. Validate the backdrop against real desktop/browser content plus transparency-disabled and power-saving fallback before polishing.

Phase 0E: register Ctrl+Alt+M, report conflicts, hide and restore without stealing focus, unregister on exit. Restore monitor/size/mode without requesting activation on media changes.

Later phases still cover richer compact/player states, DPI transitions, left/right docking and auto-hide. Lyrics visibility remains an explicit user choice.

References checked 2026-09-06:

- [System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)
- [Windows App SDK stable downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) lists 2.4.0 stable, released 2026-08-13.

Windows App SDK 2.4.0 restore/build/start succeeded. The native controller reports Active. The media console still does not reference the App SDK.

## Implemented behavior

`WidgetFrame` manages a borderless OverlappedPresenter, content-driven client sizing and current-monitor initial placement. The main row uses a horizontal artwork/metadata/transport layout, with the close button overlaid at the top-right. `WindowDragBehavior` listens on the complete glass surface and begins only from non-interactive space while the left pointer is held. Buttons, toggles, selectors and other controls are excluded by visual-ancestor inspection. Release, cancellation and capture loss all end the gesture; there is no nested native caption loop that can latch the window to the cursor. A persisted position-lock setting blocks new movement and cancels an active drag if enabled. Native DWM/Win32 calls are isolated in `Mediance.Windows.Windowing.NativeWindowFeatures`.

During dragging, `WindowDragBehavior` asks `NativeWindowFeatures.WorkAreaAt` to select the monitor under the pointer through Win32 `MonitorFromPoint`, then applies the pure `EdgeSnap` geometry to that monitor's taskbar-excluded work area. A window edge within 18 physical pixels aligns exactly to that work-area edge; the algorithm supports secondary displays and negative monitor coordinates.

`GlassBackdrop` owns one DesktopAcrylicController, holds input-active material policy for a floating widget, customizes tint/luminosity and disposes on disconnect. It uses real system backdrop composition. `WindowAppearance` places a separate tint layer above that backdrop; the density slider changes this layer across a visible 10–95% range without fading text, artwork or controls. The controller retains OS transparency/power/high-contrast policies; those scenarios still need physical validation. The mat/solid toggle only changes this application.

The footer exposes immediate position-lock and always-on-top toggles, plus the small settings button that opens a single companion settings window beside the widget. It shares the same live `SettingsViewModel`, so appearance, element visibility, lock state and size changes apply immediately. The access button itself remains available even when all optional widget elements are hidden. Settings are normalized and saved atomically as schema-versioned JSON under `%LOCALAPPDATA%/Mediance`; malformed and newer-version files are preserved rather than silently overwritten.

Normal explicit startup calls Activate. Optional Windows startup passes `--background` and leaves the widget hidden in the tray. A named single-instance coordinator makes later launches signal and foreground the existing widget, then closes the secondary process. Native topmost changes and media/property updates do not call Activate. Closing awaits media cleanup before destroying the last window. Alt+F4 also follows the same close path.

`Mediance.Windows.Windowing.SystemTrayIcon` owns one `Shell_NotifyIcon` entry and a window subclass. It restores the icon after Explorer broadcasts `TaskbarCreated`, removes it during shutdown and builds a native popup menu without an external package. Left click toggles the widget. Right click offers show/hide, settings and exit. With `CloseToTray` enabled, the X button and Alt+F4 hide the widget; the tray Exit command remains the explicit full shutdown path.

## Verification

`scripts/dev.ps1 glass-test` exercises native topmost flags, programmatic movement, density application, backdrop switching, visibility reflow, the settings-window singleton/reopen lifecycle and clean close. App-local logs are under `%LOCALAPPDATA%/Mediance/prototypes`. The optional executable argument `--preview-dir=<absolute directory>` with `--smoke-test` renders settled widget and settings XAML views in solid mode for layout review; it does not capture other windows and is not proof of Acrylic appearance.

The monitor smoke enumerates every connected Win32 work area, verifies unique `MONITORINFOEX` device identities and resolves each center through the same `MonitorFromPoint` path used while dragging. Position history is keyed by that device identity, with the earlier global coordinate retained as a safe fallback. It passed with both connected monitors on 2026-09-07.

Automated gesture tests cover click-without-drag, held movement, release, missed release recovery, cancellation, capture loss, jitter, negative monitor coordinates and restored-position clamping. The final per-monitor window position and lyrics-open state are saved on interaction and flushed during shutdown. A real two-process check verified that a secondary launch exits while the primary remains. Manual checks still needed: physical drag feel, compare desktop/browser content behind the glass, switch away without losing the chosen topmost behavior, test OS transparency off/power-saving fallback, keyboard navigation, high contrast and DPI transitions.
