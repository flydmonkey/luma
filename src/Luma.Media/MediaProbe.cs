using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Luma.Media;

public static class MediaProbe
{
    public static string Describe(string path) => ReadProbe(path, 8000) ?? "";

    public static TimeSpan TryDuration(string path)
    {
        var text = ReadProbe(path, 5000);
        if (text is null)
        {
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

    private static string? ReadProbe(string path, int timeoutMs)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null || !File.Exists(path))
        {
            return null;
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
                return null;
            }

            // Read concurrently so the timeout still applies when ffmpeg stops writing but never exits.
            var text = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return null;
            }

            return text.Wait(1000) ? text.Result : null;
        }
        catch
        {
            return null;
        }
    }

    public static void TryPoster(string mediaPath)
    {
        if (!Luma.Core.Settings.RecordingContainers.IsVideo(mediaPath)
            || mediaPath.EndsWith(".partial.mkv", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(mediaPath))
        {
            return;
        }

        var poster = Luma.Core.Library.LibraryPaths.PreparePoster(mediaPath);
        if (File.Exists(poster) && new FileInfo(poster).Length > 0)
        {
            return;
        }

        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg is null)
        {
            try
            {
                ShellPoster(mediaPath, poster).GetAwaiter().GetResult();
            }
            catch
            {
                // poster is optional
            }
            return;
        }

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

    private static async Task ShellPoster(string mediaPath, string poster)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(mediaPath);
        using var thumb = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 320);
        if (thumb is null || thumb.Type != Windows.Storage.FileProperties.ThumbnailType.Image)
        {
            return;
        }

        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(thumb);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Ignore);
        using var output = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        output.Seek(0);
        await using var target = File.Create(poster);
        await output.AsStreamForRead().CopyToAsync(target);
    }
}
