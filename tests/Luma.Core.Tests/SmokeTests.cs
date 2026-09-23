namespace Luma.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Core_assembly_loads()
    {
        Assert.NotNull(typeof(Luma.Core.Placeholder).Assembly);
    }
}
