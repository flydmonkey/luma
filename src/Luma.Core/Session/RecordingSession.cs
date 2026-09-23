namespace Luma.Core.Session;

public sealed class RecordingSession
{
    private readonly IRecordingEngine _engine;
    private readonly object _gate = new();
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
        lock (_gate)
        {
            if (_phase is SessionPhase.Recording or SessionPhase.Paused or SessionPhase.Countdown)
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
            lock (_gate)
            {
                _phase = SessionPhase.Idle;
            }

            throw;
        }
    }

    public async Task PauseAsync()
    {
        await _engine.PauseAsync().ConfigureAwait(false);
        lock (_gate)
        {
            if (_phase == SessionPhase.Recording)
            {
                _phase = SessionPhase.Paused;
            }
        }
    }

    public async Task ResumeAsync()
    {
        await _engine.ResumeAsync().ConfigureAwait(false);
        lock (_gate)
        {
            if (_phase == SessionPhase.Paused)
            {
                _phase = SessionPhase.Recording;
            }
        }
    }

    public async Task<RecordingResult> StopAsync()
    {
        lock (_gate)
        {
            _phase = SessionPhase.Processing;
        }

        try
        {
            return await _engine.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _phase = SessionPhase.Idle;
            }
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
}
