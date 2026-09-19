using Mediance.AcrylicProbe.Localization;
using Mediance.AcrylicProbe.ViewModels;
using Mediance.AcrylicProbe.Windowing;
using Mediance.Core.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using Windows.UI.ViewManagement;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Mediance.AcrylicProbe;

public sealed partial class SettingsWindow : Window
{
    private readonly WidgetFrame _frame;
    private readonly WindowDragBehavior _drag;
    private readonly WindowAppearance _appearance;
    public SettingsViewModel Settings { get; }
    public AudioRoutingViewModel Routing { get; }
    public HotkeyViewModel Hotkey { get; }
    public UpdateViewModel Update { get; }
    public LocalLyricsLibraryViewModel LocalTimings { get; }

    public SettingsWindow(SettingsViewModel settings, AudioRoutingViewModel routing, HotkeyViewModel hotkey,
        UpdateViewModel update, LocalLyricsLibraryViewModel localTimings, AppWindow owner)
    {
        Settings = settings;
        Routing = routing;
        Hotkey = hotkey;
        Update = update;
        LocalTimings = localTimings;
        InitializeComponent();
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Root_KeyDown), true);
        Title = $"Mediance · {TextCatalog.Settings}";
        _frame = new(this, 380, 540);
        _frame.ResizeContent(380, 540);
        _frame.PlaceBeside(owner);
        _drag = new(DragRegion, AppWindow);
        _appearance = new(this, Surface);
        Settings.Changed += Settings_Changed;
        Settings_Changed(this, EventArgs.Empty);
        Closed += Window_Closed;
        Root.Loaded += Root_Loaded;
        _ = Routing.RefreshAsync();
        _ = LocalTimings.RefreshAsync();
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Root_Loaded;
        try
        {
            if (!new UISettings().AnimationsEnabled)
            {
                Surface.Opacity = 1;
                RootTransform.ScaleX = RootTransform.ScaleY = 1;
                RootTransform.TranslateX = 0;
                RootTransform.TranslateY = 0;
                return;
            }
        }
        catch { }
        var storyboard = new Storyboard();
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.12 };
        storyboard.Children.Add(Animate(Surface, "Opacity", 0, 1, 240, ease));
        storyboard.Children.Add(Animate(RootTransform, "ScaleX", 0.95, 1, 440, settle));
        storyboard.Children.Add(Animate(RootTransform, "ScaleY", 0.95, 1, 440, settle));
        storyboard.Children.Add(Animate(RootTransform, "TranslateX", 24, 0, 460, ease));
        storyboard.Children.Add(Animate(RootTransform, "TranslateY", 8, 0, 420, ease));
        storyboard.Begin();
    }

    private static DoubleAnimation Animate(DependencyObject target, string property, double from, double to,
        int milliseconds, EasingFunctionBase easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EasingFunction = easing
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void Settings_Changed(object? sender, EventArgs e)
    {
        _appearance.Apply(Settings.Data);
        _frame.SetTopmost(Settings.AlwaysOnTop);
        if (Root?.IsLoaded == true)
        {
            var pulse = new DoubleAnimation
            {
                From = 0.15,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(pulse, SavePulse);
            Storyboard.SetTargetProperty(pulse, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(pulse);
            storyboard.Begin();
        }
    }

    internal bool IsHiddenFromShellAndActivatable => _frame.IsToolWindow && !_frame.IsNoActivateWindow;

    internal async Task SavePreviewAsync(string file, int page = 0)
    {
        Pages.SelectedIndex = page;
        // Pivot fades the outgoing and incoming pages. Wait for that transition
        // so diagnostics represent the settled UI rather than a dim mid-frame.
        await Task.Delay(450);
        Surface.UpdateLayout();
        await WindowPreview.SaveAsync(Surface, file);
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => Settings.Reset();
    private void SelectShortcut_Click(object sender, RoutedEventArgs e)
    {
        Hotkey.BeginCapture();
        SelectShortcutButton.Focus(FocusState.Programmatic);
    }
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!Hotkey.IsCapturing) return;
        var key = (uint)e.Key;
        if (key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C) { e.Handled = true; return; }
        var modifiers = HotkeyModifiers.None;
        if (IsDown(0x11)) modifiers |= HotkeyModifiers.Control;
        if (IsDown(0x12)) modifiers |= HotkeyModifiers.Alt;
        if (IsDown(0x10)) modifiers |= HotkeyModifiers.Shift;
        if (IsDown(0x5B) || IsDown(0x5C)) modifiers |= HotkeyModifiers.Windows;
        e.Handled = Hotkey.Capture(key, modifiers);
    }
    private static bool IsDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;
    private async void RefreshAudio_Click(object sender, RoutedEventArgs e) => await Routing.RefreshAsync();
    private async void Update_Click(object sender, RoutedEventArgs e) => await Update.ActAsync();
    private async void DeleteTiming_Click(object sender, RoutedEventArgs e) =>
        await LocalTimings.DeleteSelectedAsync();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = NormalizeSearch(SearchBox.Text);
        if (query.Length < 2) { SearchHint.Text = ""; return; }
        var categories = new[]
        {
            (0, TextCatalog.Appearance, "tema gorunum album blur zoom cam seffaflik prism midnight"),
            (1, TextCatalog.Elements, "oge kapak kontrol buton boyut genislik yazi ses cikisi lyrics dugmesi"),
            (2, TextCatalog.LyricsCategory, "lyrics soz senkron zamanlama satir otomatik"),
            (3, TextCatalog.SystemCategory, "sistem windows kisayol shortcut ses audio kapat tepsi guncelleme update")
        };
        var result = categories.FirstOrDefault(value => NormalizeSearch(value.Item3)
            .Contains(query, StringComparison.OrdinalIgnoreCase));
        if (result.Item3 is null)
        {
            SearchHint.Text = TextCatalog.Get("SettingsSearchNone");
            return;
        }
        Pages.SelectedIndex = result.Item1;
        SearchHint.Text = string.Format(TextCatalog.Get("SettingsSearchResult"), result.Item2);
    }
    private static string NormalizeSearch(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == '\u0131' ? 'i' : character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
    private async void ExportTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "Mediance-theme" };
            picker.FileTypeChoices.Add("Mediance theme", [".mediance-theme"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is not null) await FileIO.WriteTextAsync(file, Settings.ExportThemeJson());
        }
        catch (Exception ex) { ProbeLog.Write("ThemeExport", ex); Settings.ReportThemeTransferError(); }
    }
    private async void ImportTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".mediance-theme");
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is not null) Settings.ImportThemeJson(await FileIO.ReadTextAsync(file));
        }
        catch (Exception ex) { ProbeLog.Write("ThemeImport", ex); Settings.ReportThemeTransferError(); }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closed(object sender, WindowEventArgs e)
    {
        Settings.Changed -= Settings_Changed;
        Hotkey.CancelCapture();
        _drag.Dispose();
        _appearance.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
