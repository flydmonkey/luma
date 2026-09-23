namespace Luma.Obs.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Obs_assembly_loads()
    {
        Assert.NotNull(typeof(Luma.Obs.Placeholder).Assembly);
    }
}
