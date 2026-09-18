using System.Collections.ObjectModel;
using System.ComponentModel;
using Mediance.AcrylicProbe.Localization;
using Mediance.Lyrics;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed class LyricsTimingItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Detail { get; init; }
}

public sealed class LyricsSyncCenterViewModel(LocalLyricsTimingStore store) : INotifyPropertyChanged
{
    private bool _busy;
    private string _status = "";

    public ObservableCollection<LyricsTimingItem> Timings { get; } = [];
    public bool IsBusy { get => _busy; private set { _busy = value; Raise(); } }
    public string Status { get => _status; private set { _status = value; Raise(); } }
    public bool HasTimings => Timings.Count > 0;
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var entries = await store.ListAsync();
            Timings.Clear();
            foreach (var entry in entries)
            {
                var shortId = entry.Id.Split(':')[0][..8];
                Timings.Add(new()
                {
                    Id = entry.Id,
                    Label = string.Format(TextCatalog.Get("LyricsTimingPrivateLabel"), shortId),
                    Detail = string.Format(TextCatalog.Get("LyricsTimingDetail"), entry.LineCount,
                        entry.UpdatedUtc.ToLocalTime().ToString("g"))
                });
            }
            Status = entries.Count == 0 ? TextCatalog.Get("LyricsTimingEmpty") : "";
            Raise();
        }
        catch (Exception ex)
        {
            ProbeLog.Write("LyricsSyncCenter", ex);
            Status = TextCatalog.Get("LyricsTimingListError");
        }
        finally { IsBusy = false; }
    }

    public async Task DeleteAsync(string id)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(id)) return;
        IsBusy = true;
        try
        {
            await store.DeleteAsync(id);
            var item = Timings.FirstOrDefault(value => value.Id == id);
            if (item is not null) Timings.Remove(item);
            Status = Timings.Count == 0 ? TextCatalog.Get("LyricsTimingEmpty") : TextCatalog.Get("LyricsTimingDeleted");
            Raise();
        }
        catch (Exception ex)
        {
            ProbeLog.Write("LyricsTimingDelete", ex);
            Status = TextCatalog.Get("LyricsTimingDeleteError");
        }
        finally { IsBusy = false; }
    }

    private void Raise() => PropertyChanged?.Invoke(this, new(null));
}
