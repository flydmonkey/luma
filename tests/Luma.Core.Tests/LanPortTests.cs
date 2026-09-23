using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class LanPortTests
{
    [Theory]
    [InlineData("12345", 12345)]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    public void Accepts_valid_ports(string text, int expected)
    {
        Assert.True(LanPort.TryParse(text, out var port, out var error));
        Assert.Equal(expected, port);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("123456")]
    [InlineData("65536")]
    [InlineData("abc")]
    public void Rejects_out_of_range_ports(string text)
    {
        Assert.False(LanPort.TryParse(text, out _, out var error));
        Assert.Contains("1", error);
        Assert.Contains("65535", error);
    }
}
