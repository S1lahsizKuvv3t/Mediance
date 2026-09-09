using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mediance.Launcher;

internal static partial class Program
{
    private const uint ErrorIcon = 0x00000010;

    [STAThread]
    private static int Main()
    {
        var applicationDirectory = Path.Combine(AppContext.BaseDirectory, "App");
        var applicationPath = Path.Combine(applicationDirectory, "Mediance.exe");
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();

        if (!File.Exists(applicationPath))
        {
            ShowError("Mediance application files could not be found. Extract the complete ZIP archive before opening Mediance.");
            return 1;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                WorkingDirectory = applicationDirectory,
                UseShellExecute = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowError("Mediance could not be started.");
                return 1;
            }

            if (arguments.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
            {
                process.WaitForExit();
                return process.ExitCode;
            }

            return 0;
        }
        catch
        {
            ShowError("Mediance could not be started. Extract the package again and make sure its App folder remains beside Mediance.exe.");
            return 1;
        }
    }

    private static void ShowError(string message)
    {
        _ = MessageBox(IntPtr.Zero, message, "Mediance", ErrorIcon);
    }

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr window, string text, string caption, uint type);
}
