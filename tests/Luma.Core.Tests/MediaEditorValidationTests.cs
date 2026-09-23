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

    [Fact]
    public async Task Compress_can_replace_the_input_file()
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null)
        {
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var input = Path.Combine(folder, "input.mp4");
        try
        {
            await RunFfmpegAsync(ffmpeg, [
                "-y", "-f", "lavfi", "-i", "color=c=black:s=64x64:d=0.2", "-pix_fmt", "yuv420p", input
            ]);
            var originalLength = new FileInfo(input).Length;

            var result = await new MediaEditor().CompressAsync(input, replace: true);

            Assert.Equal(input, result);
            Assert.True(File.Exists(input));
            Assert.True(new FileInfo(input).Length > 0);
            Assert.DoesNotContain(Directory.EnumerateFiles(folder), path => Path.GetFileName(path).StartsWith(".input-", StringComparison.Ordinal));
            Assert.True(originalLength > 0);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort test cleanup */ }
        }
    }

    [Fact]
    public async Task Cancelled_compress_terminates_ffmpeg_and_removes_partial_output()
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null)
        {
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var input = Path.Combine(folder, "long-input.mp4");
        try
        {
            await RunFfmpegAsync(ffmpeg, [
                "-y", "-f", "lavfi", "-i", "color=c=black:s=1920x1080:d=30", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", input
            ]);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new MediaEditor().CompressAsync(input, replace: true, cancellation.Token));

            Assert.True(File.Exists(input));
            Assert.DoesNotContain(Directory.EnumerateFiles(folder), path => Path.GetFileName(path).StartsWith(".long-input-", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort test cleanup */ }
        }
    }

    private static async Task RunFfmpegAsync(string ffmpeg, IReadOnlyList<string> arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(ffmpeg)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("无法启动 FFmpeg 测试进程。");
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
    }
}
