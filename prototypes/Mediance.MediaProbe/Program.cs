using System.Globalization;
using System.Text;
using System.Text.Json;
using Mediance.Core.Media;
using Mediance.Windows.Media;

Console.OutputEncoding = Encoding.UTF8;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
await using var media = new WindowsMediaSessionService();
media.Diagnostic += (_, text) => Console.Error.WriteLine(text);

try
{
    await media.StartAsync(cancellation.Token);
    var mode = args.FirstOrDefault() ?? "interactive";
    if (mode == "snapshot")
    {
        Console.WriteLine(JsonSerializer.Serialize(media.Snapshot, jsonOptions));
        return 0;
    }
    if (mode == "watch")
    {
        Print(media.Snapshot);
        media.SnapshotChanged += (_, snapshot) => Print(snapshot);
        var seconds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 30;
        if (seconds is < 1 or > 3600) throw new ArgumentException("Watch duration must be 1–3600 seconds.");
        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellation.Token);
        return 0;
    }
    if (mode != "interactive") throw new ArgumentException("Use interactive, snapshot or watch [seconds].");

    Console.WriteLine("Mediance · Windows media feasibility probe\n");
    Help();
    Print(media.Snapshot);
    while (!cancellation.IsCancellationRequested)
    {
        Console.Write("mediance> ");
        var input = await Console.In.ReadLineAsync(cancellation.Token);
        if (input is null || input.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase)) break;
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) continue;
        var command = parts[0].ToLowerInvariant();
        try
        {
            if (command == "help") { Help(); continue; }
            if (command == "list") { Print(media.Snapshot); continue; }
            if (command == "follow") { await media.PinAsync(null, cancellation.Token); continue; }
            if (command == "pin")
            {
                if (parts.Length < 2) throw new ArgumentException("Usage: pin <session ID>");
                await media.PinAsync(parts[1], cancellation.Token);
                Console.WriteLine("Session pinned.");
                continue;
            }
            var knownCommands = new Dictionary<string, MediaCommand>
            {
                ["play"] = MediaCommand.Play, ["pause"] = MediaCommand.Pause,
                ["toggle"] = MediaCommand.Toggle, ["previous"] = MediaCommand.Previous,
                ["next"] = MediaCommand.Next, ["seek"] = MediaCommand.Seek
            };
            if (command != "artwork" && !knownCommands.ContainsKey(command))
                throw new ArgumentException("Unknown command. Type help.");
            var selected = media.Snapshot.Selected;
            if (selected is null) { Console.WriteLine("No media session. Start playback in a compatible application."); continue; }
            if (command == "artwork")
            {
                var artwork = await media.ReadArtworkAsync(selected.Id, cancellation.Token);
                Console.WriteLine(artwork is null ? "Artwork unavailable or changed during read." :
                    $"Artwork read: {artwork.Bytes.Length:N0} bytes, {artwork.ContentType}. No file saved.");
                continue;
            }
            TimeSpan? position = null;
            if (command == "seek")
            {
                if (parts.Length < 2 || !double.TryParse(parts[1], CultureInfo.InvariantCulture, out var seconds)
                    || !double.IsFinite(seconds) || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds)
                    throw new ArgumentException("Usage: seek <non-negative seconds, e.g. 30.5>");
                position = TimeSpan.FromSeconds(seconds);
            }
            var result = await media.ExecuteAsync(selected.Id, knownCommands[command], position, cancellation.Token);
            Console.WriteLine(result.Succeeded ? "Source accepted the command. Use list to verify its resulting state." : result.Error);
        }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); }
    }
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return 0; }
catch (Exception ex)
{
    Console.Error.WriteLine($"Media probe failed: {ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}");
    return 1;
}

static void Help() => Console.WriteLine(
    "list | pin <ID> | follow | play | pause | toggle | previous | next | seek <seconds> | artwork | quit\n" +
    "Commands affect the selected application. Starting this probe never changes playback.\n");

static void Print(MediaSnapshot snapshot)
{
    var current = snapshot.CurrentSessionId ?? (snapshot.CurrentSourceAppId is null ? "none" : "unresolved");
    Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {snapshot.Sessions.Count} session(s), " +
        $"current={current}, selected={snapshot.SelectedSessionId ?? "none"}, " +
        $"pinned={snapshot.PinnedSessionId ?? "none"}");
    if (snapshot.CurrentSessionId is null && snapshot.CurrentSourceAppId is not null)
        Console.WriteLine($"  Windows current source: {snapshot.CurrentSourceAppId}; session match unresolved. Use pin <ID> to choose explicitly.");
    foreach (var s in snapshot.Sessions)
    {
        Console.WriteLine($"  {s.Id} · {s.SourceAppId} · {s.Status} · {s.Kind}\n" +
            $"    {s.Track.Title} — {s.Track.Artist}\n" +
            $"    {s.Timeline.Position:c} / {s.Timeline.End:c}, artwork={s.HasArtwork}\n" +
            $"    play={s.Capabilities.CanPlay}, pause={s.Capabilities.CanPause}, toggle={s.Capabilities.CanToggle}, " +
            $"previous={s.Capabilities.CanPrevious}, next={s.Capabilities.CanNext}, seek={s.Capabilities.CanSeek}");
    }
}
