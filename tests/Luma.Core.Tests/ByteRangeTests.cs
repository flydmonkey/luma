using Luma.Core.Lan;

namespace Luma.Core.Tests;

public sealed class ByteRangeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("items=0-10")]
    [InlineData("bytes=0-10,20-30")]
    [InlineData("bytes=abc-")]
    [InlineData("bytes=10-5")]
    public void Missing_or_unsupported_header_sends_the_whole_file(string? header)
    {
        var range = ByteRange.Parse(header, 100);
        Assert.Equal(ByteRangeKind.Full, range.Kind);
        Assert.Equal(0, range.Start);
        Assert.Equal(99, range.End);
    }

    [Theory]
    [InlineData("bytes=0-", 0, 99)]
    [InlineData("bytes=10-19", 10, 19)]
    [InlineData("bytes=90-500", 90, 99)]
    [InlineData("bytes=-30", 70, 99)]
    [InlineData("bytes=-500", 0, 99)]
    public void Valid_range_is_partial(string header, long start, long end)
    {
        var range = ByteRange.Parse(header, 100);
        Assert.Equal(ByteRangeKind.Partial, range.Kind);
        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
        Assert.Equal(end - start + 1, range.Length);
    }

    [Theory]
    [InlineData("bytes=100-")]
    [InlineData("bytes=-0")]
    public void Range_outside_the_file_is_unsatisfiable(string header)
    {
        Assert.Equal(ByteRangeKind.Unsatisfiable, ByteRange.Parse(header, 100).Kind);
    }
}
