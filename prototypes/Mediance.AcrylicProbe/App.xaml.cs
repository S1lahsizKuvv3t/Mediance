using Microsoft.UI.Xaml;
using Mediance.Windows.Windowing;

namespace Mediance.AcrylicProbe;

public partial class App : Application
{
    private MainWindow? _window;
    private SingleInstanceCoordinator? _singleInstance;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => ProbeLog.Write("Unhandled", args.Exception);
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ProbeLog.Write("Launch");
        _singleInstance = new();
        if (!_singleInstance.IsPrimary)
        {
            if (!Environment.GetCommandLineArgs().Contains("--background")) _singleInstance.SignalPrimary();
            _singleInstance.Dispose();
            _singleInstance = null;
            Exit();
            return;
        }
        var previousForeground = NativeWindowFeatures.ForegroundWindow();
        _window = new MainWindow();
        _singleInstance.Listen(() => _window.DispatcherQueue.TryEnqueue(_window.ShowWithoutActivation));
        _window.Closed += (_, _) => { _singleInstance?.Dispose(); _singleInstance = null; };
        // WinUI needs one activation to fully initialize XAML bindings. Restore the
        // previously active application immediately; later widget clicks use
        // WS_EX_NOACTIVATE and never take foreground ownership.
        _window.Activate();
        NativeWindowFeatures.RestoreForegroundWindow(previousForeground);
        if (Environment.GetCommandLineArgs().Contains("--background")) _window.HideForBackgroundStartup();
        else _window.ShowWithoutActivation();
    }
}
