namespace Luma.Core.Session;

public static class StopFileRules
{
    public const string ForcedWarning = "OBS 正常停止超时，已强制结束；成片已通过校验";

    public static bool IsUsable(long bytes, TimeSpan fileDuration, TimeSpan recordedDuration)
    {
        if (bytes <= 0 || fileDuration <= TimeSpan.Zero)
        {
            return false;
        }

        if (recordedDuration <= TimeSpan.Zero)
        {
            return true;
        }

        var tolerance = Math.Max(1.5, recordedDuration.TotalSeconds * 0.005);
        return Math.Abs(fileDuration.TotalSeconds - recordedDuration.TotalSeconds) <= tolerance;
    }

    public static string WireState(SessionPhase phase) => phase switch
    {
        SessionPhase.Recording => "recording",
        SessionPhase.Paused => "paused",
        SessionPhase.Processing => "stopping",
        SessionPhase.Countdown => "recording",
        _ => "idle"
    };
}
