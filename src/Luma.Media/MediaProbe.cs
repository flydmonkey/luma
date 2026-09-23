using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Luma.Media;

public static class MediaProbe
{
    public static string Describe(string path)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null || !File.Exists(path))
        {
            return "";
        }

        try
        {
            var start = new ProcessStartInfo(ffmpeg)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-hide_banner");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start);
            if (process is null)
            {
                return "";
            }

            var text = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(8000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            return text;
        }
        catch
        {
            return "";
        }
    }

    public static TimeSpan TryDuration(string path)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null || !File.Exists(path))
        {
            return TimeSpan.Zero;
        }

        try
        {
            var start = new ProcessStartInfo(ffmpeg)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-hide_banner");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start);
            if (process is null)
            {
                return TimeSpan.Zero;
            }

            var text = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return TimeSpan.Zero;
            }

            var match = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!match.Success)
            {
                return TimeSpan.Zero;
            }

            var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return new TimeSpan(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0) + TimeSpan.FromSeconds(seconds);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    public static void TryPoster(string mediaPath)
    {
        if (!Luma.Core.Settings.RecordingContainers.IsVideo(mediaPath)
            || mediaPath.EndsWith(".partial.mkv", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null || !File.Exists(mediaPath))
        {
            return;
        }

        var poster = Path.ChangeExtension(mediaPath, ".jpg");
        try
        {
            var start = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-y");
            start.ArgumentList.Add("-ss");
            start.ArgumentList.Add("0.4");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(mediaPath);
            start.ArgumentList.Add("-frames:v");
            start.ArgumentList.Add("1");
            start.ArgumentList.Add("-vf");
            start.ArgumentList.Add("scale=320:-1");
            start.ArgumentList.Add(poster);
            using var process = Process.Start(start);
            if (process is not null && !process.WaitForExit(8000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }
        }
        catch
        {
            // poster is optional
        }
    }
}
