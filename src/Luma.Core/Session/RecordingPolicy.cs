namespace Luma.Core.Session;

public static class SegmentGate
{
    public static bool Due(bool enabled, TimeSpan elapsed, long bytes, int minutes, int maxMegabytes)
    {
        if (!enabled)
        {
            return false;
        }

        if (minutes > 0 && elapsed >= TimeSpan.FromMinutes(minutes))
        {
            return true;
        }

        return maxMegabytes > 0 && bytes >= maxMegabytes * 1024L * 1024L;
    }
}

public static class EncoderNotice
{
    public static bool FellBackToSoftware(bool hardwareRequested, CaptureMode mode, EngineStatus status) =>
        hardwareRequested
        && mode != CaptureMode.AudioOnly
        && status.Phase is SessionPhase.Recording or SessionPhase.Paused
        && !status.UsedHardware;
}

public static class RecordingChrome
{
    public static bool ShowBar(bool enabled, bool fromLan, bool silentMode) =>
        enabled && !(fromLan && silentMode);
}
