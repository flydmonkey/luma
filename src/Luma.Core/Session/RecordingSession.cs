namespace Luma.Core.Session;

public sealed class RecordingSession
{
    private readonly IRecordingEngine _engine;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operation = new(1, 1);
    private SessionPhase _phase = SessionPhase.Idle;

    public RecordingSession(IRecordingEngine engine)
    {
        _engine = engine;
    }

    public SessionPhase Phase
    {
        get
        {
            lock (_gate)
            {
                return _phase;
            }
        }
    }

    public async Task StartAsync(RecordingRequest request, CancellationToken token = default)
    {
        await _operation.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_phase != SessionPhase.Idle)
                {
                    throw new InvalidOperationException("已有录制正在进行。");
                }

                _phase = SessionPhase.Recording;
            }

            try
            {
                await _engine.StartAsync(request, token).ConfigureAwait(false);
            }
            catch
            {
                SetPhase(SessionPhase.Idle);
                throw;
            }
        }
        finally
        {
            _operation.Release();
        }
    }

    public async Task PauseAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            RequirePhase(SessionPhase.Recording, "只有正在录制时才能暂停。");
            await _engine.PauseAsync().ConfigureAwait(false);
            SetPhase(SessionPhase.Paused);
        }
        finally
        {
            _operation.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            RequirePhase(SessionPhase.Paused, "只有已暂停的录制才能恢复。");
            await _engine.ResumeAsync().ConfigureAwait(false);
            SetPhase(SessionPhase.Recording);
        }
        finally
        {
            _operation.Release();
        }
    }

    public async Task<RecordingResult> StopAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            RequirePhase([SessionPhase.Recording, SessionPhase.Paused], "当前没有可以停止的录制。");
            SetPhase(SessionPhase.Processing);

            try
            {
                return await _engine.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                SetPhase(_engine.GetStatus().Phase == SessionPhase.Processing
                    ? SessionPhase.Processing
                    : SessionPhase.Idle);
            }
        }
        finally
        {
            _operation.Release();
        }
    }

    public EngineStatus GetStatus()
    {
        var status = _engine.GetStatus();
        lock (_gate)
        {
            if (_phase is SessionPhase.Recording or SessionPhase.Paused
                && status.Phase is SessionPhase.Recording or SessionPhase.Paused)
            {
                _phase = status.Phase;
            }
        }

        return status;
    }

    private void RequirePhase(SessionPhase expected, string message) => RequirePhase([expected], message);

    private void RequirePhase(SessionPhase[] expected, string message)
    {
        lock (_gate)
        {
            if (!expected.Contains(_phase))
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    private void SetPhase(SessionPhase phase)
    {
        lock (_gate)
        {
            _phase = phase;
        }
    }
}
