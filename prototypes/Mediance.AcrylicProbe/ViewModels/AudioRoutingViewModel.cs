using System.ComponentModel;
using Mediance.Core.Audio;
using Microsoft.UI.Xaml;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed record AudioDeviceChoice(string? Id, string Name);

public sealed class AudioRoutingViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAudioRoutingService _routing;
    private readonly PlayerViewModel _player;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _volumeGate = new(1, 1);
    private CancellationTokenSource? _refreshCancellation;
    private IReadOnlyList<AudioDeviceChoice> _choices = [];
    private AudioDeviceChoice? _selectedChoice;
    private string? _loadedSource;
    private string? _observedSource;
    private string _error = "";
    private bool _busy;
    private bool _synchronizing;
    private bool _disposed;

    public AudioRoutingViewModel(IAudioRoutingService routing, PlayerViewModel player)
    {
        _routing = routing;
        _player = player;
        _observedSource = player.SourceAppId;
        _player.PropertyChanged += Player_PropertyChanged;
    }

    public IReadOnlyList<AudioDeviceChoice> Choices => _choices;
    public AudioDeviceChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (_synchronizing || value is null || ReferenceEquals(value, _selectedChoice)) return;
            _selectedChoice = value;
            PropertyChanged?.Invoke(this, new(nameof(SelectedChoice)));
            _ = ApplyAsync(value);
        }
    }
    public string ApplicationName => _player.Source;
    public string Error => _error;
    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(_error) ? Visibility.Collapsed : Visibility.Visible;
    public string Hint => string.IsNullOrWhiteSpace(_player.SourceAppId)
        ? Localization.TextCatalog.Get("AudioSessionUnavailable")
        : Localization.TextCatalog.Get("AudioScopeHint");
    public bool IsBusy => _busy;
    public bool CanSelect => !_busy && _choices.Count > 0 && !string.IsNullOrWhiteSpace(_loadedSource);
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync()
    {
        if (_disposed) return;
        var source = _player.SourceAppId;
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new();
        var token = _refreshCancellation.Token;
        if (string.IsNullOrWhiteSpace(source))
        {
            _loadedSource = null;
            _choices = [];
            _selectedChoice = null;
            _error = "";
            Raise();
            return;
        }

        SetBusy(true);
        try
        {
            var snapshot = await _routing.InspectAsync(source, token);
            if (token.IsCancellationRequested || _disposed || !string.Equals(source, _player.SourceAppId,
                    StringComparison.OrdinalIgnoreCase)) return;
            _error = "";
            Synchronize(snapshot);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProbeLog.Write("AudioRoutingRefresh", ex);
            _loadedSource = source;
            _choices = [];
            _selectedChoice = null;
            _error = Localization.TextCatalog.Get("AudioRoutingError");
            Raise();
        }
        finally { if (!token.IsCancellationRequested && !_disposed) SetBusy(false); }
    }

    private async Task ApplyAsync(AudioDeviceChoice choice)
    {
        var source = _loadedSource;
        if (_disposed || _busy || string.IsNullOrWhiteSpace(source)) return;
        SetBusy(true);
        _error = "";
        try
        {
            var result = await _routing.SetOutputAsync(source, choice.Id);
            if (_disposed || !string.Equals(source, _player.SourceAppId, StringComparison.OrdinalIgnoreCase)) return;
            _error = result.Succeeded ? "" : result.Error ?? Localization.TextCatalog.Get("AudioRoutingError");
            Synchronize(result.Snapshot);
        }
        catch (Exception ex)
        {
            ProbeLog.Write("AudioRoutingSet", ex);
            _error = Localization.TextCatalog.Get("AudioRoutingError");
        }
        finally { if (!_disposed) { SetBusy(false); Raise(); } }
    }

    public async Task<ApplicationVolumeResult?> AdjustVolumeAsync(int steps)
    {
        if (_disposed || steps == 0 || string.IsNullOrWhiteSpace(_player.SourceAppId)) return null;
        var entered = false;
        try
        {
            await _volumeGate.WaitAsync(_lifetime.Token);
            entered = true;
            var source = _player.SourceAppId;
            if (_disposed || string.IsNullOrWhiteSpace(source)) return null;
            var result = await _routing.ChangeVolumeAsync(source, Math.Clamp(steps, -4, 4) * 0.05f, _lifetime.Token);
            if (_disposed || !string.Equals(source, _player.SourceAppId, StringComparison.OrdinalIgnoreCase)) return null;
            _error = result.Succeeded ? "" : result.Error ?? Localization.TextCatalog.Get("AudioVolumeError");
            Raise();
            return result;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return null; }
        catch (Exception ex)
        {
            ProbeLog.Write("ApplicationVolume", ex);
            _error = Localization.TextCatalog.Get("AudioVolumeError");
            Raise();
            return null;
        }
        finally
        {
            if (entered) _volumeGate.Release();
        }
    }

    private void Synchronize(AudioRoutingSnapshot snapshot)
    {
        _loadedSource = snapshot.Application.IsAvailable ? snapshot.Application.SourceAppId : null;
        var defaultDevice = snapshot.Devices.FirstOrDefault(d => d.IsSystemDefault);
        var defaultName = defaultDevice is null
            ? Localization.TextCatalog.Get("SystemDefault")
            : $"{Localization.TextCatalog.Get("SystemDefault")} · {defaultDevice.Name}";
        var choices = new List<AudioDeviceChoice> { new(null, defaultName) };
        choices.AddRange(snapshot.Devices.Select(d => new AudioDeviceChoice(d.Id, d.Name)));
        var selected = choices.FirstOrDefault(c => string.Equals(c.Id,
            snapshot.Application.PersistedDeviceId, StringComparison.OrdinalIgnoreCase));
        if (selected is null && snapshot.Application.PersistedDeviceId is not null)
        {
            selected = new(snapshot.Application.PersistedDeviceId,
                Localization.TextCatalog.Get("UnavailableDevice"));
            choices.Add(selected);
        }

        _synchronizing = true;
        _choices = choices;
        _selectedChoice = selected ?? choices[0];
        _synchronizing = false;
        Raise();
    }

    private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null and not nameof(PlayerViewModel.SourceAppId)) return;
        var source = _player.SourceAppId;
        if (string.Equals(source, _observedSource, StringComparison.OrdinalIgnoreCase)) return;
        _observedSource = source;
        _ = RefreshAsync();
    }
    private void SetBusy(bool value) { _busy = value; Raise(); }
    private void Raise() => PropertyChanged?.Invoke(this, new(null));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _player.PropertyChanged -= Player_PropertyChanged;
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _lifetime.Dispose();
    }
}
