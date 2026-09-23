using Luma.Core.Settings;

namespace Luma.Core.Session;

public enum SessionPhase
{
    Idle = 0,
    Countdown = 1,
    Recording = 2,
    Paused = 3,
    Processing = 4
}

public sealed class CaptureTarget
{
    public CaptureMode Mode { get; init; }
    public int MonitorIndex { get; init; }
    public string? WindowId { get; init; }
    public string? WindowTitle { get; init; }
    public int CropX { get; init; }
    public int CropY { get; init; }
    public int CropWidth { get; init; }
    public int CropHeight { get; init; }

    public bool IsReady => Mode switch
    {
        CaptureMode.Display => true,
        CaptureMode.AudioOnly => true,
        CaptureMode.Region => CropWidth > 0 && CropHeight > 0,
        CaptureMode.Window => !string.IsNullOrWhiteSpace(WindowId),
        CaptureMode.Game => false,
        _ => false
    };
}

public sealed class RecordingRequest
{
    public required string OutputPath { get; init; }
    public required CaptureTarget Target { get; init; }
    public required QualitySettings Quality { get; init; }
    public required bool CaptureSystemAudio { get; init; }
    public required bool CaptureMicrophone { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public OverlaySettings? Overlay { get; init; }
}

public sealed class EngineStatus
{
    public SessionPhase Phase { get; init; }
    public string EncoderName { get; init; } = "";
    public bool UsedHardware { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double EffectiveFps { get; init; }
    public long SkippedFrames { get; init; }
    public TimeSpan EncodedDuration { get; init; }
    public string? Warning { get; init; }
    public bool StopForced { get; init; }
}

public sealed class RecordingResult
{
    public required string OutputPath { get; init; }
    public required TimeSpan Duration { get; init; }
    public required EngineStatus Status { get; init; }
}

public static class SessionStartRules
{
    public static string? Reject(CaptureTarget target)
    {
        if (target.Mode == CaptureMode.Game || IsGameMode(target.Mode.ToString()))
        {
            return "游戏录制已关闭。";
        }

        return target.IsReady ? null : "请先选择录制目标。";
    }

    public static bool IsGameMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        var text = mode.Trim();
        return text.Equals("game", StringComparison.OrdinalIgnoreCase)
            || text.Equals("3", StringComparison.Ordinal);
    }
}

public interface IRecordingEngine
{
    Task StartAsync(RecordingRequest request, CancellationToken token = default);
    Task PauseAsync();
    Task ResumeAsync();
    Task<RecordingResult> StopAsync();
    EngineStatus GetStatus();
}
