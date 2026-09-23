using Luma.Core.Session;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class RecordingSessionTests
{
    [Fact]
    public async Task Idle_session_rejects_pause_resume_and_stop_without_calling_engine()
    {
        var engine = new FakeRecordingEngine();
        var session = new RecordingSession(engine);

        await Assert.ThrowsAsync<InvalidOperationException>(session.PauseAsync);
        await Assert.ThrowsAsync<InvalidOperationException>(session.ResumeAsync);
        await Assert.ThrowsAsync<InvalidOperationException>(session.StopAsync);

        Assert.Equal(0, engine.PauseCalls);
        Assert.Equal(0, engine.ResumeCalls);
        Assert.Equal(0, engine.StopCalls);
        Assert.Equal(SessionPhase.Idle, session.Phase);
    }

    [Fact]
    public async Task Recording_lifecycle_allows_only_valid_transitions()
    {
        var engine = new FakeRecordingEngine();
        var session = new RecordingSession(engine);

        await session.StartAsync(Request());
        Assert.Equal(SessionPhase.Recording, session.Phase);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(Request()));
        await Assert.ThrowsAsync<InvalidOperationException>(session.ResumeAsync);

        await session.PauseAsync();
        Assert.Equal(SessionPhase.Paused, session.Phase);
        await Assert.ThrowsAsync<InvalidOperationException>(session.PauseAsync);

        await session.ResumeAsync();
        engine.AllowStop.SetResult();
        var result = await session.StopAsync();

        Assert.Equal("recording.mp4", result.OutputPath);
        Assert.Equal(SessionPhase.Idle, session.Phase);
        Assert.Equal(1, engine.StartCalls);
        Assert.Equal(1, engine.PauseCalls);
        Assert.Equal(1, engine.ResumeCalls);
        Assert.Equal(1, engine.StopCalls);
    }

    [Fact]
    public async Task Concurrent_stop_calls_are_serialized_and_second_call_is_rejected()
    {
        var engine = new FakeRecordingEngine();
        var session = new RecordingSession(engine);
        await session.StartAsync(Request());

        var first = session.StopAsync();
        await engine.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = session.StopAsync();
        engine.AllowStop.SetResult();

        await first;
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Equal(1, engine.StopCalls);
    }

    private static RecordingRequest Request() => new()
    {
        OutputPath = "recording.mp4",
        Target = new CaptureTarget { Mode = CaptureMode.Display },
        Quality = QualitySettings.FromLevel(QualityLevel.Sd),
        CaptureSystemAudio = false,
        CaptureMicrophone = false
    };

    private sealed class FakeRecordingEngine : IRecordingEngine
    {
        private SessionPhase _phase = SessionPhase.Idle;
        public int StartCalls { get; private set; }
        public int PauseCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public int StopCalls { get; private set; }
        public TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(RecordingRequest request, CancellationToken token = default)
        {
            StartCalls++;
            _phase = SessionPhase.Recording;
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            PauseCalls++;
            _phase = SessionPhase.Paused;
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            ResumeCalls++;
            _phase = SessionPhase.Recording;
            return Task.CompletedTask;
        }

        public async Task<RecordingResult> StopAsync()
        {
            StopCalls++;
            StopEntered.TrySetResult();
            await AllowStop.Task;
            _phase = SessionPhase.Idle;
            return new RecordingResult
            {
                OutputPath = "recording.mp4",
                Duration = TimeSpan.FromSeconds(1),
                Status = GetStatus()
            };
        }

        public EngineStatus GetStatus() => new() { Phase = _phase };
    }
}
