namespace Mediance.AcrylicProbe;

internal static class ProbeLog
{
    internal static void Write(string category, Exception? error = null)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mediance", "prototypes");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "acrylic.log"),
                $"{DateTimeOffset.Now:O} {category} {error?.GetType().Name} {error?.HResult:X8} {error?.Message}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
