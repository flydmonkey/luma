using Luma.Core.Session;

namespace Luma.Core.Tests;

public sealed class QualityCeilingTests
{
    [Fact]
    public void Full_hd_source_stops_at_1080p()
    {
        var choices = QualitySettings.ChoicesFitting(1920, 1080);
        Assert.Equal([QualityLevel.Sd, QualityLevel.Hd], choices);
        Assert.Equal(QualityLevel.Hd, QualitySettings.Clamp(QualityLevel.FourK, 1920, 1080));
        Assert.Equal(QualityLevel.Sd, QualitySettings.Clamp(QualityLevel.Sd, 1920, 1080));
    }

    [Fact]
    public void Four_k_source_keeps_every_preset()
    {
        Assert.Equal(QualitySettings.Ascending, QualitySettings.ChoicesFitting(3840, 2160));
        Assert.Equal(QualityLevel.FourK, QualitySettings.Clamp(QualityLevel.FourK, 3840, 2160));
    }

    [Fact]
    public void Source_smaller_than_720p_has_no_preset()
    {
        Assert.Empty(QualitySettings.ChoicesFitting(800, 600));
    }
}
