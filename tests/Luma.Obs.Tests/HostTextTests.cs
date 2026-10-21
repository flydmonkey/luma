using Luma.Obs;
using Xunit;

namespace Luma.Obs.Tests;

public class HostTextTests
{
    [Fact]
    public void DropsEncoderFallbackBecauseTheAppShowsItsOwnNotice()
    {
        Assert.Equal("", HostText.Localize("硬件编码不可用，已回退 obs_x264。"));
    }

    [Fact]
    public void KeepsKnownPhrasesAndPassesUnknownTextThrough()
    {
        Assert.Equal("系统声采集不可用。 麦克风不可用。 device lost", HostText.Localize("系统声采集不可用。 麦克风不可用。 device lost"));
    }

    [Fact]
    public void MapsTheNoiseSuppressionWarning()
    {
        Assert.Equal("麦克风降噪不可用。", HostText.Localize("麦克风降噪不可用。"));
    }

    [Fact]
    public void LeavesEmptyTextAlone()
    {
        Assert.Null(HostText.Localize(null));
        Assert.Equal("", HostText.Localize(""));
    }
}
