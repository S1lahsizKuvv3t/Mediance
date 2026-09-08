using System.ComponentModel;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed class ToggleSetting(string label, Func<bool> read, Action<bool> write, Func<bool>? enabled = null) : INotifyPropertyChanged
{
    public string Label => label;
    public bool IsOn { get => read(); set => write(value); }
    public bool IsEnabled => enabled?.Invoke() ?? true;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Refresh() => PropertyChanged?.Invoke(this, new(null));
}
