using Luma.Core.Session;

namespace Luma.Obs;

public sealed class HostStatus
{
    public bool Ok { get; set; }
    public int Phase { get; set; }
    public string EncoderName { get; set; } = "";
    public bool UsedHardware { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double EffectiveFps { get; set; }
    public long SkippedFrames { get; set; }
    public double EncodedDurationSeconds { get; set; }
    public string OutputPath { get; set; } = "";
    public string Error { get; set; } = "";
    public string Warning { get; set; } = "";
    public bool StopForced { get; set; }

    public EngineStatus ToEngineStatus() => new()
    {
        Phase = (SessionPhase)Phase,
        EncoderName = EncoderName,
        UsedHardware = UsedHardware,
        Width = Width,
        Height = Height,
        EffectiveFps = EffectiveFps,
        SkippedFrames = SkippedFrames,
        EncodedDuration = TimeSpan.FromSeconds(Math.Max(0, EncodedDurationSeconds)),
        Warning = string.IsNullOrWhiteSpace(Warning)
            ? (string.IsNullOrWhiteSpace(Error) ? null : Error)
            : Warning,
        StopForced = StopForced
    };
}
