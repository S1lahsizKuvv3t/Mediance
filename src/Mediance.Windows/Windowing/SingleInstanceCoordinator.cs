namespace Mediance.Windows.Windowing;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string Identity = "A613921B-665E-47D6-B020-480AE1053D54";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _wait;
    public bool IsPrimary { get; }

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(false, $@"Local\Mediance.Singleton.{Identity}", out var created);
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\Mediance.Activate.{Identity}");
        IsPrimary = created;
    }

    public void SignalPrimary() => _activation.Set();

    public void Listen(Action activate) => _wait = ThreadPool.RegisterWaitForSingleObject(
        _activation, (_, _) => activate(), null, Timeout.Infinite, false);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _activation.Dispose();
        _mutex.Dispose();
    }
}
