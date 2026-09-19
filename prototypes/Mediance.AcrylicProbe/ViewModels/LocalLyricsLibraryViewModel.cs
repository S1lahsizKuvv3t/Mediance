using System.Collections.ObjectModel;
using System.ComponentModel;
using Mediance.Lyrics;

namespace Mediance.AcrylicProbe.ViewModels;

public sealed record LocalTimingItem(string Id, string Label);

public sealed class LocalLyricsLibraryViewModel(LocalLyricsTimingStore store) : INotifyPropertyChanged
{
    private LocalTimingItem? _selected;
    private bool _busy;
    private string _status = "";

    public ObservableCollection<LocalTimingItem> Entries { get; } = [];
    public LocalTimingItem? Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Raise(); }
    }
    public bool IsBusy => _busy;
    public bool CanDelete => !_busy && _selected is not null;
    public string Status => _status;
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        Raise();
        try
        {
            var entries = await store.ListAsync();
            Entries.Clear();
            foreach (var entry in entries)
            {
                var label = string.Format(Localization.TextCatalog.Get("LocalTimingEntry"),
                    entry.LineCount, entry.UpdatedUtc.ToLocalTime());
                Entries.Add(new(entry.Id, label));
            }
            Selected = Entries.FirstOrDefault();
            _status = Entries.Count == 0
                ? Localization.TextCatalog.Get("NoLocalTimings")
                : string.Format(Localization.TextCatalog.Get("LocalTimingCount"), Entries.Count);
        }
        catch (Exception ex)
        {
            ProbeLog.Write($"TimingList: {ex.GetType().Name} (0x{ex.HResult:X8})");
            _status = Localization.TextCatalog.Get("LocalTimingError");
        }
        finally { _busy = false; Raise(); }
    }

    public async Task DeleteSelectedAsync()
    {
        if (_busy || _selected is null) return;
        _busy = true;
        Raise();
        try
        {
            if (await store.DeleteAsync(_selected.Id))
            {
                Entries.Remove(_selected);
                Selected = Entries.FirstOrDefault();
                _status = Localization.TextCatalog.Get("LocalTimingDeleted");
            }
        }
        catch (Exception ex)
        {
            ProbeLog.Write($"TimingDelete: {ex.GetType().Name} (0x{ex.HResult:X8})");
            _status = Localization.TextCatalog.Get("LocalTimingError");
        }
        finally { _busy = false; Raise(); }
    }

    private void Raise() => PropertyChanged?.Invoke(this, new(null));
}
