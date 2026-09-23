namespace Luma.Core.Os;

public sealed class SingleInstanceGate : IDisposable
{
    public const string MutexName = @"Local\Luma.SingleInstance";
    public const string EventName = @"Local\Luma.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly Thread? _watch;
    private bool _disposed;

    public bool IsOwner { get; }

    public event Action? Activated;

    public SingleInstanceGate()
    {
        _mutex = new Mutex(true, MutexName, out var created);
        IsOwner = created;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        if (IsOwner)
        {
            _watch = new Thread(Watch)
            {
                IsBackground = true,
                Name = "Luma.SingleInstance"
            };
            _watch.Start();
        }
    }

    public void SignalActivate()
    {
        _activate.Set();
    }

    private void Watch()
    {
        while (!_disposed)
        {
            if (_activate.WaitOne(500))
            {
                Activated?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activate.Set();
        if (IsOwner)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activate.Dispose();
    }
}
