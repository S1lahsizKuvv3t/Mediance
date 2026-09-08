using Mediance.Core.Settings;
using Mediance.Core.Windowing;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class WindowAndSettingsTests
{
    [Fact]
    public void ClickDoesNotLatchWindowToPointer()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(100, 100), new(50, 50));
        drag.End(1);
        Assert.Null(drag.Move(1, false, new(300, 300)));
        Assert.False(drag.IsActive);
    }

    [Fact]
    public void HeldPointerMovesFromOriginalWindowPositionAndStopsAtRelease()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(100, 100), new(50, 50));
        Assert.Equal(new PixelPoint(70, 80), drag.Move(1, true, new(120, 130)));
        Assert.Equal(new PixelPoint(90, 90), drag.Move(1, true, new(140, 140)));
        drag.End(1);
        Assert.Null(drag.Move(1, true, new(160, 160)));
    }

    [Fact]
    public void MissedReleaseEventIsRecoveredWhenMoveReportsButtonUp()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(100, 100), new(50, 50));
        Assert.Null(drag.Move(1, false, new(200, 200)));
        Assert.False(drag.IsActive);
    }

    [Fact]
    public void LostCaptureCancelsMovement()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(100, 100), new(50, 50));
        drag.Cancel();
        Assert.Null(drag.Move(1, true, new(200, 200)));
    }

    [Fact]
    public void MinorClickJitterAndUnrelatedPointerDoNotMoveWindow()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(100, 100), new(50, 50));
        Assert.Null(drag.Move(1, true, new(102, 102)));
        Assert.Null(drag.Move(2, true, new(200, 200)));
        drag.End(2);
        Assert.True(drag.IsActive);
    }

    [Fact]
    public void DragSupportsNegativeMonitorCoordinatesAndASecondGesture()
    {
        var drag = new DragGesture();
        drag.Begin(1, new(-100, 100), new(-150, 50));
        Assert.Equal(new PixelPoint(-250, 80), drag.Move(1, true, new(-200, 130)));
        drag.End(1);
        drag.Begin(1, new(-200, 130), new(-250, 80));
        Assert.Equal(new PixelPoint(-260, 70), drag.Move(1, true, new(-210, 120)));
    }

    [Fact]
    public void EdgeSnapAlignsEachNearbyWorkAreaEdgeAndLeavesDistantPositionsAlone()
    {
        var area = new PixelRect(-1920, 0, 1920, 1040);
        var size = new PixelSize(520, 240);
        Assert.Equal(new PixelPoint(-1920, 300), EdgeSnap.Apply(new(-1905, 300), size, area));
        Assert.Equal(new PixelPoint(-520, 300), EdgeSnap.Apply(new(-530, 300), size, area));
        Assert.Equal(new PixelPoint(-1000, 0), EdgeSnap.Apply(new(-1000, 17), size, area));
        Assert.Equal(new PixelPoint(-1000, 800), EdgeSnap.Apply(new(-1000, 785), size, area));
        Assert.Equal(new PixelPoint(-1000, 300), EdgeSnap.Apply(new(-1000, 300), size, area));
    }

    [Fact]
    public void SavedWindowPlacementIsClampedToTheNearestWorkArea()
    {
        var area = new PixelRect(-1920, 0, 1920, 1040);
        var size = new PixelSize(520, 260);
        Assert.Equal(new PixelPoint(-1920, 0), WindowPlacement.Clamp(new(-3000, -400), size, area));
        Assert.Equal(new PixelPoint(-520, 780), WindowPlacement.Clamp(new(300, 1600), size, area));
        Assert.Equal(new PixelPoint(-1200, 300), WindowPlacement.Clamp(new(-1200, 300), size, area));
    }

    [Fact]
    public void SettingsClampBadNumbersWithoutResettingVisibilityPreferences()
    {
        var settings = (new WidgetSettings
        { WindowWidth = 99999, GlassIntensity = -5, TextScale = double.NaN, ShowArtwork = false, ShowControls = false,
          ShowAudioOutput = false, LyricsLeadMilliseconds = 9000, LyricsLineCount = 9, Theme = (ThemePreset)99 }).Normalize();
        Assert.Equal(720, settings.WindowWidth);
        Assert.Equal(420, new WidgetSettings { WindowWidth = 100 }.Normalize().WindowWidth);
        Assert.Equal(10, settings.GlassIntensity);
        Assert.Equal(100, settings.TextScale);
        Assert.False(settings.ShowArtwork);
        Assert.False(settings.ShowControls);
        Assert.False(settings.ShowAudioOutput);
        Assert.Equal(2000, settings.LyricsLeadMilliseconds);
        Assert.Equal(3, settings.LyricsLineCount);
        Assert.Equal(ThemePreset.Midnight, settings.Theme);
        Assert.Equal(2, new WidgetSettings { LyricsLineCount = 2 }.Normalize().LyricsLineCount);
    }

    [Fact]
    public void ShortcutRequiresAModifierAndMainKeyAndInvalidSettingsRecover()
    {
        Assert.True(HotkeyGesture.Default.IsValid);
        Assert.False(new HotkeyGesture(HotkeyModifiers.None, 'M').IsValid);
        Assert.False(new HotkeyGesture(HotkeyModifiers.Control, 0x11).IsValid);
        var settings = new WidgetSettings
        {
            ShortcutModifiers = HotkeyModifiers.None,
            ShortcutVirtualKey = 0
        }.Normalize();
        Assert.Equal(HotkeyGesture.Default.Modifiers, settings.ShortcutModifiers);
        Assert.Equal(HotkeyGesture.Default.VirtualKey, settings.ShortcutVirtualKey);
    }

    [Fact]
    public async Task SettingsSurviveStoreRecreationAndKeepPreviousVersionBackup()
    {
        using var folder = new TestFolder();
        var store = new JsonSettingsStore(folder.File);
        await store.SaveAsync(new() { ShowArtwork = false, ShowAudioOutput = false, ShowProgress = false,
            EnableAmbientGlow = false, EnableWheelVolume = false, IsLocked = true, GlassIntensity = 23, ControlSize = 64,
            CloseToTray = true, LyricsOpen = true, LyricsLineCount = 2, StartWithWindows = true,
            Theme = ThemePreset.Prism, WindowX = -820, WindowY = 140, LastMonitorId = @"\\.\DISPLAY2",
            MonitorPlacements = new Dictionary<string, SavedWindowPlacement> { [@"\\.\DISPLAY2"] = new(-820, 140) },
            LyricsLeadMilliseconds = 700, ShortcutModifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            ShortcutVirtualKey = 'L' });
        var saved = await new JsonSettingsStore(folder.File).LoadAsync();
        Assert.False(saved.ShowArtwork);
        Assert.False(saved.ShowAudioOutput);
        Assert.False(saved.ShowProgress);
        Assert.False(saved.EnableAmbientGlow);
        Assert.False(saved.EnableWheelVolume);
        Assert.True(saved.CloseToTray);
        Assert.True(saved.LyricsOpen);
        Assert.Equal(2, saved.LyricsLineCount);
        Assert.True(saved.StartWithWindows);
        Assert.Equal(ThemePreset.Prism, saved.Theme);
        Assert.Equal(@"\\.\DISPLAY2", saved.LastMonitorId);
        Assert.Equal(new SavedWindowPlacement(-820, 140), saved.MonitorPlacements[@"\\.\DISPLAY2"]);
        Assert.Equal(-820, saved.WindowX);
        Assert.Equal(140, saved.WindowY);
        Assert.True(saved.IsLocked);
        Assert.Equal(23, saved.GlassIntensity);
        Assert.Equal(64, saved.ControlSize);
        Assert.Equal(700, saved.LyricsLeadMilliseconds);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, saved.ShortcutModifiers);
        Assert.Equal((uint)'L', saved.ShortcutVirtualKey);
        await store.SaveAsync(saved with { ShowControls = false });
        var prior = await new JsonSettingsStore(folder.File + ".bak").LoadAsync();
        Assert.True(prior.ShowControls);
        Assert.False((await store.LoadAsync()).ShowControls);
    }

    [Fact]
    public async Task MalformedSettingsArePreservedAndDefaultsRecover()
    {
        using var folder = new TestFolder();
        await File.WriteAllTextAsync(folder.File, "{broken");
        var settings = await new JsonSettingsStore(folder.File).LoadAsync();
        Assert.True(settings.ShowArtwork);
        Assert.Equal("{broken", await File.ReadAllTextAsync(folder.File + ".invalid"));
    }

    [Fact]
    public async Task MalformedSettingsRecoverTheLastCompleteBackup()
    {
        using var folder = new TestFolder();
        var store = new JsonSettingsStore(folder.File);
        await store.SaveAsync(new() { ShowArtwork = false, GlassIntensity = 24 });
        await store.SaveAsync(new() { ShowArtwork = true, GlassIntensity = 72 });
        await File.WriteAllTextAsync(folder.File, "{interrupted");

        var recovered = await new JsonSettingsStore(folder.File).LoadAsync();

        Assert.False(recovered.ShowArtwork);
        Assert.Equal(24, recovered.GlassIntensity);
        Assert.Equal("{interrupted", await File.ReadAllTextAsync(folder.File + ".invalid"));
        Assert.False((await new JsonSettingsStore(folder.File).LoadAsync()).ShowArtwork);
    }

    [Fact]
    public async Task NewerSettingsSchemaIsNeverOverwritten()
    {
        using var folder = new TestFolder();
        const string future = "{\"schemaVersion\":99,\"futureSetting\":true}";
        await File.WriteAllTextAsync(folder.File, future);
        var store = new JsonSettingsStore(folder.File);
        await Assert.ThrowsAsync<UnsupportedSettingsVersionException>(() => store.LoadAsync());
        await Assert.ThrowsAsync<UnsupportedSettingsVersionException>(() => store.SaveAsync(new()));
        Assert.Equal(future, await File.ReadAllTextAsync(folder.File));
    }

    [Fact]
    public async Task MissingSettingsUseDefaultsAndCancelledSaveKeepsExistingFile()
    {
        using var folder = new TestFolder();
        var store = new JsonSettingsStore(folder.File);
        Assert.Equal(new WidgetSettings(), await store.LoadAsync());
        await store.SaveAsync(new() { ShowTitle = false });
        using var token = new CancellationTokenSource();
        token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new(), token.Token));
        Assert.False((await store.LoadAsync()).ShowTitle);
    }

    [Fact]
    public async Task SerializedSavesLeaveACompleteValidDocument()
    {
        using var folder = new TestFolder();
        var store = new JsonSettingsStore(folder.File);
        await Task.WhenAll(Enumerable.Range(1, 10).Select(i => store.SaveAsync(new() { GlassIntensity = i * 8 })));
        var saved = await store.LoadAsync();
        Assert.InRange(saved.GlassIntensity, 10, 80);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(folder.File)!, "*.tmp"));
    }

    private sealed class TestFolder : IDisposable
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "settings-test-data");
        private readonly string _directory;
        public string File => Path.Combine(_directory, "settings.json");
        public TestFolder()
        {
            _directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }
        public void Dispose()
        {
            var full = Path.GetFullPath(_directory);
            if (!full.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup escaped its directory.");
            Directory.Delete(full, true);
        }
    }
}
