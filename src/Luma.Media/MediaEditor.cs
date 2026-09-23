using System.Diagnostics;

namespace Luma.Media;

public sealed class MediaEditor
{
    public Task<string> CompressAsync(string input, bool replace = false, CancellationToken token = default) =>
        RunAsync(input, replace, ["-y", "-i", input, "-c:v", "libx264", "-crf", "28", "-c:a", "copy"], ".min.mp4", token);

    public Task<string> RepairAsync(string input, CancellationToken token = default) =>
        RunAsync(input, replace: false, ["-y", "-i", input, "-c", "copy"], ".fixed.mp4", token);

    public async Task<string> TrimAsync(string input, TimeSpan start, TimeSpan end, bool replace = false, CancellationToken token = default)
    {
        if (end <= start)
        {
            throw new ArgumentException("结束时间必须晚于开始时间。");
        }

        return await RunAsync(input, replace, [
            "-y", "-ss", Format(start), "-to", Format(end), "-i", input, "-c", "copy"
        ], ".trim.mp4", token).ConfigureAwait(false);
    }

    public Task<string> MergeAsync(IReadOnlyList<string> inputs, bool replace = false, CancellationToken token = default)
    {
        if (inputs.Count < 2)
        {
            throw new ArgumentException("至少需要两个文件才能合并。");
        }

        var list = Path.Combine(Path.GetTempPath(), $"luma-concat-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(list, inputs.Select(path => $"file '{path.Replace("'", "'\\''")}'"));
        return RunAsync(inputs[0], replace, ["-y", "-f", "concat", "-safe", "0", "-i", list, "-c", "copy"], ".merge.mp4", token);
    }

    public Task<string> BurnSubtitlesAsync(string input, string subtitlePath, bool replace = false, CancellationToken token = default)
    {
        var escaped = subtitlePath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal);
        return RunAsync(input, replace, ["-y", "-i", input, "-vf", $"subtitles='{escaped}'", "-c:a", "copy"], ".sub.mp4", token);
    }

    public async Task<string> BurnCaptionTextAsync(string input, string text, bool replace = false, CancellationToken token = default)
    {
        var srt = Path.Combine(Path.GetTempPath(), $"luma-{Guid.NewGuid():N}.srt");
        var body = "1" + Environment.NewLine + "00:00:00,000 --> 00:00:03,000" + Environment.NewLine + text;
        await File.WriteAllTextAsync(srt, body, token).ConfigureAwait(false);
        try
        {
            return await BurnSubtitlesAsync(input, srt, replace, token).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(srt); } catch { }
        }
    }

    public Task<string> MixMusicAsync(string input, string musicPath, double musicVolume, bool replace = false, CancellationToken token = default) =>
        RunAsync(input, replace, [
            "-y", "-i", input, "-i", musicPath,
            "-filter_complex", $"[1:a]volume={musicVolume}[m];[0:a][m]amix=inputs=2:duration=first[a]",
            "-map", "0:v", "-map", "[a]", "-c:v", "copy"
        ], ".music.mp4", token);

    private static async Task<string> RunAsync(string input, bool replace, IReadOnlyList<string> args, string suffix, CancellationToken token)
    {
        var ffmpeg = FfmpegLocator.Find() ?? throw new InvalidOperationException("未找到 FFmpeg。后期任务需要随包或 PATH 中的 ffmpeg.exe。");
        var output = replace ? input : Path.Combine(
            Path.GetDirectoryName(input)!,
            Path.GetFileNameWithoutExtension(input) + suffix);
        if (!replace && File.Exists(output))
        {
            output = Path.Combine(
                Path.GetDirectoryName(input)!,
                $"{Path.GetFileNameWithoutExtension(input)}-{DateTime.Now:yyyyMMddHHmmss}{suffix}");
        }

        var fullArgs = args.ToList();
        if (!fullArgs.Contains(output))
        {
            fullArgs.Add(output);
        }

        var start = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in fullArgs)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 FFmpeg。");
        var stderr = process.StandardError.ReadToEndAsync(token);
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        await process.WaitForExitAsync(token).ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        await stdout.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "FFmpeg 失败。" : error);
        }

        return output;
    }

    private static string Format(TimeSpan value) => value.ToString(@"hh\:mm\:ss");
}
