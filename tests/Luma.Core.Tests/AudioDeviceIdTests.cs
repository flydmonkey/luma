using Luma.Core.Session;

namespace Luma.Core.Tests;

public sealed class AudioDeviceIdTests
{
    private const string Endpoint = "{0.0.1.00000000}.{8c1f2a3e-1111-2222-3333-444455556666}";
    private const string Swd = @"\\?\SWD#MMDEVAPI#" + Endpoint + "#{2eef81be-33fa-4800-9670-1cd474972c3f}";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public void Empty_choice_is_the_default_device(string? id)
    {
        Assert.Equal("default", AudioDeviceId.ToWasapi(id));
        Assert.True(AudioDeviceId.IsDefault(id));
    }

    [Fact]
    public void Device_interface_path_becomes_the_endpoint_id()
    {
        Assert.Equal(Endpoint, AudioDeviceId.ToWasapi(Swd));
        Assert.Equal(Endpoint, AudioDeviceId.ToWasapi(Endpoint));
    }

    [Fact]
    public void Contains_matches_either_form()
    {
        Assert.True(AudioDeviceId.Contains([Swd], Endpoint));
        Assert.True(AudioDeviceId.Contains([Endpoint], Swd));
        Assert.False(AudioDeviceId.Contains([Swd], "{0.0.1.00000000}.{other}"));
    }
}
