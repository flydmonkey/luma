namespace Luma.Core.Session;

public readonly record struct ScheduleDecision(bool Start, bool Stop, bool Overlap);

public static class ScheduleGate
{
    public static ScheduleDecision Evaluate(
        TimeOnly now,
        TimeOnly start,
        TimeOnly? end,
        int? durationMinutes,
        bool sessionActive,
        bool startedBySchedule)
    {
        var windowEnd = end;
        if (durationMinutes is > 0)
        {
            var byDuration = start.AddMinutes(durationMinutes.Value);
            windowEnd = windowEnd is { } existing && existing < byDuration ? existing : byDuration;
        }

        var due = now >= start && (windowEnd is null || now <= windowEnd);
        if (!due)
        {
            return new ScheduleDecision(false, startedBySchedule && sessionActive, false);
        }

        if (sessionActive && !startedBySchedule)
        {
            return new ScheduleDecision(false, false, true);
        }

        if (!sessionActive && !startedBySchedule)
        {
            return new ScheduleDecision(true, false, false);
        }

        return default;
    }
}

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

public static class RecordingChrome
{
    public static bool ShowBar(bool enabled, bool fromLan, bool silentMode) =>
        enabled && !(fromLan && silentMode);
}
