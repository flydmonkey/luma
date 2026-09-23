using Luma.Core.Localization;
using Luma.Core.Session;

namespace Luma.Core.Tests;

public sealed class RecordingPolicyTests
{
    [Fact]
    public void Ten_minute_segment_splits_and_keeps_the_first_file_playing()
    {
        Assert.False(SegmentGate.Due(true, TimeSpan.FromMinutes(9), 0, 10, 0));
        Assert.True(SegmentGate.Due(true, TimeSpan.FromMinutes(10), 0, 10, 0));
        Assert.True(SegmentGate.Due(true, TimeSpan.FromMinutes(1), 20L * 1024 * 1024, 0, 20));
        Assert.False(SegmentGate.Due(false, TimeSpan.FromMinutes(30), 0, 10, 0));
    }

    [Fact]
    public void Schedule_skips_when_a_session_is_already_active()
    {
        var start = new TimeOnly(9, 0);
        var during = ScheduleGate.Evaluate(new TimeOnly(9, 5), start, new TimeOnly(10, 0), null, sessionActive: true, startedBySchedule: false);
        Assert.True(during.Overlap);
        Assert.False(during.Start);

        var idle = ScheduleGate.Evaluate(new TimeOnly(9, 5), start, new TimeOnly(10, 0), 10, sessionActive: false, startedBySchedule: false);
        Assert.True(idle.Start);

        var ended = ScheduleGate.Evaluate(new TimeOnly(9, 11), start, null, 10, sessionActive: true, startedBySchedule: true);
        Assert.True(ended.Stop);
    }

    [Fact]
    public void Silent_lan_start_hides_the_recording_bar()
    {
        Assert.False(RecordingChrome.ShowBar(enabled: true, fromLan: true, silentMode: true));
        Assert.True(RecordingChrome.ShowBar(enabled: true, fromLan: false, silentMode: true));
        Assert.True(RecordingChrome.ShowBar(enabled: true, fromLan: true, silentMode: false));
    }

    [Fact]
    public void Unsupported_system_language_falls_back_to_english()
    {
        Assert.Equal(UiLanguages.English, UiLanguages.ResolveEffective(UiLanguages.System, "fr-FR"));
        Assert.Equal(UiLanguages.English, UiLanguages.ResolveEffective(UiLanguages.System, "de-DE"));
        Assert.Equal(UiLanguages.SimplifiedChinese, UiLanguages.ResolveEffective(UiLanguages.System, "zh-CN"));
    }
}
