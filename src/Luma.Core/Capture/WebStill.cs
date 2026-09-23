using System.Drawing;

namespace Luma.Core.Capture;

public sealed record WebStillMonitor(int X, int Y, int Width, int Height);

public sealed record WebStillWindow(string Id, string Title, int X, int Y, int Width, int Height);

public sealed class WebStillPayload
{
    public int OriginX { get; init; }
    public int OriginY { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Png { get; init; } = "";
    public IReadOnlyList<WebStillMonitor> Monitors { get; init; } = [];
    public IReadOnlyList<WebStillWindow> Windows { get; init; } = [];
}

public static class WebStill
{
    public static WebStillPayload Capture(
        IReadOnlyList<(int X, int Y, int Width, int Height)> monitors,
        IReadOnlyList<(string Id, string Title, int X, int Y, int Width, int Height)> windows)
    {
        using var frame = StillShot.CaptureDesktop(monitors);
        var png = frame.CropPng(frame.OriginX, frame.OriginY, frame.Width, frame.Height);
        return Build(frame.OriginX, frame.OriginY, frame.Width, frame.Height, png, monitors, windows);
    }

    public static WebStillPayload Describe(
        byte[] png,
        IReadOnlyList<(int X, int Y, int Width, int Height)> monitors,
        IReadOnlyList<(string Id, string Title, int X, int Y, int Width, int Height)> windows)
    {
        var bounds = Bounds(monitors);
        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);
        if (bitmap.Width != bounds.Width || bitmap.Height != bounds.Height)
        {
            throw new InvalidOperationException("静止图必须是主机像素。");
        }

        return Build(bounds.OriginX, bounds.OriginY, bounds.Width, bounds.Height, png, monitors, windows);
    }

    private static WebStillPayload Build(
        int originX,
        int originY,
        int width,
        int height,
        byte[] png,
        IReadOnlyList<(int X, int Y, int Width, int Height)> monitors,
        IReadOnlyList<(string Id, string Title, int X, int Y, int Width, int Height)> windows)
    {
        var imageMonitors = monitors
            .Select(monitor => new WebStillMonitor(monitor.X - originX, monitor.Y - originY, monitor.Width, monitor.Height))
            .ToArray();
        var imageWindows = windows
            .Where(window => !string.IsNullOrWhiteSpace(window.Id) && window.Width > 0 && window.Height > 0)
            .Select(window => new WebStillWindow(
                window.Id,
                string.IsNullOrWhiteSpace(window.Title) ? window.Id : window.Title,
                window.X - originX,
                window.Y - originY,
                window.Width,
                window.Height))
            .Where(window => window.X < width && window.Y < height && window.X + window.Width > 0 && window.Y + window.Height > 0)
            .ToArray();

        return new WebStillPayload
        {
            OriginX = originX,
            OriginY = originY,
            Width = width,
            Height = height,
            Png = Convert.ToBase64String(png),
            Monitors = imageMonitors,
            Windows = imageWindows
        };
    }

    private static (int OriginX, int OriginY, int Width, int Height) Bounds(
        IReadOnlyList<(int X, int Y, int Width, int Height)> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("没有可截取的显示器。");
        }

        var originX = monitors.Min(monitor => monitor.X);
        var originY = monitors.Min(monitor => monitor.Y);
        var width = monitors.Max(monitor => monitor.X + monitor.Width) - originX;
        var height = monitors.Max(monitor => monitor.Y + monitor.Height) - originY;
        return (originX, originY, Math.Max(1, width), Math.Max(1, height));
    }
}
