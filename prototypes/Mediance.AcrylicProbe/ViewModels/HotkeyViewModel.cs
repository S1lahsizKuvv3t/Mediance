using System.ComponentModel;
using Mediance.Core.Settings;
using Mediance.Windows.Windowing;
using Windows.System;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed class HotkeyViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GlobalHotkeyRegistration _registration;
    private readonly SettingsViewModel _settings;
    private bool _capturing;
    private bool _saving;
    private bool _disposed;
    private string _status = "";

    public HotkeyViewModel(GlobalHotkeyRegistration registration, SettingsViewModel settings)
    {
        _registration = registration;
        _settings = settings;
        _registration.Pressed += Registration_Pressed;
        _settings.Changed += Settings_Changed;
        ApplySettings();
    }

    public string ShortcutText => Format(new(_settings.Data.ShortcutModifiers, _settings.Data.ShortcutVirtualKey));
    public string SelectButtonText => Localization.TextCatalog.Get(_capturing ? "PressShortcut" : "SelectShortcut");
    public string Status => _status;
    public bool IsCapturing => _capturing;
    public bool IsRegistered => _registration.IsRegistered;
    public event EventHandler? Pressed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void BeginCapture()
    {
        if (_disposed || _capturing) return;
        _capturing = true;
        _registration.Unregister();
        _status = Localization.TextCatalog.Get("HotkeyCaptureHint");
        Raise();
    }

    public bool Capture(uint virtualKey, HotkeyModifiers modifiers)
    {
        if (!_capturing) return false;
        if (virtualKey == (uint)VirtualKey.Escape)
        {
            CancelCapture();
            return true;
        }
        var gesture = new HotkeyGesture(modifiers, virtualKey);
        if (!gesture.IsValid)
        {
            _status = Localization.TextCatalog.Get("HotkeyNeedsCombination");
            Raise();
            return true;
        }

        _capturing = false;
        if (_registration.TryRegister(gesture))
        {
            _saving = true;
            _settings.SetShortcut(gesture);
            _saving = false;
            _status = Localization.TextCatalog.Get("HotkeySaved");
        }
        else
        {
            RestoreSavedRegistration();
            _status = Localization.TextCatalog.Get("HotkeyConflict");
        }
        Raise();
        return true;
    }

    public void CancelCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        RestoreSavedRegistration();
        _status = "";
        Raise();
    }

    private void ApplySettings()
    {
        if (_disposed || _capturing || _saving) return;
        var gesture = new HotkeyGesture(_settings.Data.ShortcutModifiers, _settings.Data.ShortcutVirtualKey);
        if (_registration.TryRegister(gesture))
        {
            _status = "";
        }
        else _status = Localization.TextCatalog.Get("HotkeyConflict");
        Raise();
    }

    private void RestoreSavedRegistration()
    {
        var saved = new HotkeyGesture(_settings.Data.ShortcutModifiers, _settings.Data.ShortcutVirtualKey);
        _registration.TryRegister(saved);
    }

    private void Settings_Changed(object? sender, EventArgs e) => ApplySettings();
    private void Registration_Pressed(object? sender, EventArgs e) => Pressed?.Invoke(this, EventArgs.Empty);
    private void Raise() => PropertyChanged?.Invoke(this, new(null));

    private static string Format(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyName(gesture.VirtualKey));
        return string.Join(" + ", parts);
    }

    private static string KeyName(uint key) => key switch
    {
        >= 'A' and <= 'Z' => ((char)key).ToString(),
        >= '0' and <= '9' => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        _ => ((VirtualKey)key).ToString().Replace("Number", "", StringComparison.Ordinal)
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.Changed -= Settings_Changed;
        _registration.Pressed -= Registration_Pressed;
        _registration.Dispose();
    }
}
