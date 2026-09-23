using Luma.Media;

namespace Luma.Core.Tests;

public sealed class MediaEditorValidationTests
{
    [Fact]
    public async Task Trim_rejects_inverted_range()
    {
        var editor = new MediaEditor();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            editor.TrimAsync("unused.mp4", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4)));
        Assert.Contains("结束时间", error.Message);
    }
}
