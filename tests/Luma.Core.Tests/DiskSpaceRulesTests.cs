using Luma.Core.Session;
using Xunit;

namespace Luma.Core.Tests;

public class DiskSpaceRulesTests
{
    private const long Gb = 1L << 30;
    private const long Mb = 1L << 20;

    [Fact]
    public void Refuses_to_start_with_less_than_500_mb()
    {
        Assert.Equal(DiskVerdict.Full, DiskSpaceRules.BeforeStart(400 * Mb, 8000, 1));
    }

    [Fact]
    public void Warns_when_fewer_than_ten_minutes_fit()
    {
        // 8000 + 160 kbps is about 61 MB a minute, so 550 MB lasts roughly nine minutes.
        Assert.Equal(DiskVerdict.Low, DiskSpaceRules.BeforeStart(550 * Mb, 8000, 1));
        Assert.Equal(DiskVerdict.Ok, DiskSpaceRules.BeforeStart(20 * Gb, 8000, 1));
    }

    [Fact]
    public void Stops_a_running_recording_below_300_mb()
    {
        Assert.Equal(DiskVerdict.Full, DiskSpaceRules.WhileRecording(250 * Mb, 8000, 3));
        Assert.Equal(DiskVerdict.Low, DiskSpaceRules.WhileRecording(400 * Mb, 8000, 3));
    }

    [Fact]
    public void Audio_only_recordings_last_much_longer()
    {
        var remaining = DiskSpaceRules.Remaining(1 * Gb, 0, 1);
        Assert.True(remaining > TimeSpan.FromHours(10));
    }

    [Fact]
    public void Reads_free_space_for_an_existing_folder_and_its_missing_children()
    {
        var temp = Path.GetTempPath();
        Assert.True(DiskSpaceRules.FreeBytes(temp) > 0);
        Assert.True(DiskSpaceRules.FreeBytes(Path.Combine(temp, Guid.NewGuid().ToString("N"), "later")) > 0);
    }

    [Fact]
    public void Quality_target_is_sharper_for_higher_levels()
    {
        Assert.True(QualitySettings.QualityTarget(QualityLevel.FourK) < QualitySettings.QualityTarget(QualityLevel.Sd));
        Assert.Equal(23, QualitySettings.QualityTarget(QualityLevel.Hd));
    }
}
