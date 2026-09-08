using System.Text;
using Mediance.Windows.Audio;

Console.OutputEncoding = Encoding.UTF8;
var source = args.Length > 1 ? args[1] : "Spotify.exe";
var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "inspect";
var service = new WindowsAudioRoutingService();

try
{
    if (mode == "inspect")
    {
        Print(await service.InspectAsync(source));
        return 0;
    }

    if (mode == "set")
    {
        if (args.Length < 3 || !int.TryParse(args[2], out var number))
            throw new ArgumentException("Usage: set <source.exe> <device number>");
        var snapshot = await service.InspectAsync(source);
        if (number < 1 || number > snapshot.Devices.Count)
            throw new ArgumentOutOfRangeException(nameof(number), "Device number is outside the list.");
        var result = await service.SetOutputAsync(source, snapshot.Devices[number - 1].Id);
        Print(result.Snapshot);
        Console.WriteLine(result.Succeeded ? "Route read-back verified." : result.Error);
        return result.Succeeded ? 0 : 2;
    }

    if (mode == "default")
    {
        var result = await service.SetOutputAsync(source, null);
        Print(result.Snapshot);
        Console.WriteLine(result.Succeeded ? "System Default read-back verified." : result.Error);
        return result.Succeeded ? 0 : 2;
    }

    throw new ArgumentException("Use: inspect [source.exe] | set <source.exe> <device number> | default <source.exe>");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Audio routing probe failed: {ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}");
    return 1;
}

static void Print(Mediance.Core.Audio.AudioRoutingSnapshot snapshot)
{
    Console.WriteLine("Mediance · per-application output probe\n");
    Console.WriteLine("Output devices:");
    for (var i = 0; i < snapshot.Devices.Count; i++)
    {
        var device = snapshot.Devices[i];
        Console.WriteLine($"  {i + 1} · {device.Name}{(device.IsSystemDefault ? " · System Default" : "")}");
        Console.WriteLine($"      {device.Id}");
    }

    var app = snapshot.Application;
    Console.WriteLine($"\nApplication: {app.SourceAppId}");
    Console.WriteLine($"  Audio process IDs: {(app.ProcessIds.Count == 0 ? "none" : string.Join(", ", app.ProcessIds))}");
    Console.WriteLine($"  Persisted output: {(app.HasMixedRoutes ? "mixed" : app.PersistedDeviceId ?? "System Default")}");
    Console.WriteLine("Inspect mode is read-only. It never changes an output.");
}
