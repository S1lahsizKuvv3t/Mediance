using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Mediance.Core.Settings;
using Microsoft.UI.Xaml;
using Windows.System;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed class UpdateViewModel(HttpClient client) : INotifyPropertyChanged
{
    private const string ReleasesEndpoint = "https://api.github.com/repos/S1lahsizKuvv3t/Mediance/releases";
    private bool _busy;
    private bool _updateAvailable;
    private string _status = Localization.TextCatalog.Get("UpdateNotChecked");
    private Uri? _releaseUri;

    public bool IsBusy => _busy;
    public bool UpdateAvailable => _updateAvailable;
    public Visibility UpdateVisibility => _updateAvailable ? Visibility.Visible : Visibility.Collapsed;
    public string Status => _status;
    public string ActionLabel => Localization.TextCatalog.Get(_updateAvailable ? "OpenRelease" : "CheckUpdates");
    public bool CanAct => !_busy;
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task CheckAsync(bool reportCurrent = true)
    {
        if (_busy) return;
        _busy = true;
        Raise();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
            request.Headers.UserAgent.ParseAdd("Mediance-UpdateChecker/0.9");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var currentText = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            if (!ReleaseVersion.TryParse(currentText, out var current))
                current = new(0, 0, 0, null);
            ReleaseVersion? newest = null;
            Uri? newestUri = null;
            string? newestTag = null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                var tag = item.GetProperty("tag_name").GetString();
                var url = item.GetProperty("html_url").GetString();
                if (!ReleaseVersion.TryParse(tag, out var candidate) ||
                    !Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
                if (newest is null || candidate.CompareTo(newest) > 0)
                {
                    newest = candidate;
                    newestUri = uri;
                    newestTag = tag;
                }
            }
            _updateAvailable = newest is not null && newest.CompareTo(current) > 0;
            _releaseUri = _updateAvailable ? newestUri : null;
            _status = _updateAvailable
                ? string.Format(Localization.TextCatalog.Get("UpdateAvailable"), newestTag)
                : reportCurrent ? Localization.TextCatalog.Get("UpToDate") : "";
        }
        catch (Exception ex)
        {
            ProbeLog.Write($"UpdateCheck: {ex.GetType().Name} (0x{ex.HResult:X8})");
            if (reportCurrent) _status = Localization.TextCatalog.Get("UpdateCheckFailed");
        }
        finally
        {
            _busy = false;
            Raise();
        }
    }

    public async Task ActAsync()
    {
        if (_updateAvailable && _releaseUri is not null)
            await Launcher.LaunchUriAsync(_releaseUri);
        else
            await CheckAsync();
    }

    private void Raise() => PropertyChanged?.Invoke(this, new(null));
}
