using Microsoft.Win32;

namespace Mediance.Windows.Windowing;

public sealed class StartupRegistration(string executablePath)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mediance";
    public string Command => $"\"{executablePath}\" --background";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
