using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Luma.Core.Capture;

public static class StillShot
{
    public const int MinimumEdge = 8;
    public const string FactoryHotkey = "Ctrl+Shift+S";

    public static bool FormsRegion(int width, int height) =>
        width >= MinimumEdge && height >= MinimumEdge;

    public static string FileName(DateTime localTime) =>
        $"Luma-Shot-{localTime:yyyyMMdd-HHmmss}.png";

    public static bool IsSystemSnip(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        return parts.Count == 3
            && parts.Contains("SHIFT")
            && parts.Contains("S")
            && (parts.Contains("WIN") || parts.Contains("WINDOWS"));
    }

    public static string NormalizeHotkey(string? gesture) =>
        string.IsNullOrWhiteSpace(gesture) || IsSystemSnip(gesture) ? FactoryHotkey : gesture.Trim();

    public static StillFrame CaptureDesktop(IReadOnlyList<(int X, int Y, int Width, int Height)> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("没有可截取的显示器。");
        }

        var originX = monitors.Min(monitor => monitor.X);
        var originY = monitors.Min(monitor => monitor.Y);
        var width = monitors.Max(monitor => monitor.X + monitor.Width) - originX;
        var height = monitors.Max(monitor => monitor.Y + monitor.Height) - originY;
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            foreach (var monitor in monitors)
            {
                graphics.CopyFromScreen(
                    monitor.X,
                    monitor.Y,
                    monitor.X - originX,
                    monitor.Y - originY,
                    new Size(monitor.Width, monitor.Height),
                    CopyPixelOperation.SourceCopy);
            }
        }

        ForceOpaque(bitmap);
        return new StillFrame(bitmap, originX, originY);
    }

    public static byte[] CapturePng(int x, int y, int width, int height)
    {
        if (!FormsRegion(width, height))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        }

        ForceOpaque(bitmap);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public static string SavePng(string folder, byte[] png, DateTime localTime)
    {
        if (png.Length == 0)
        {
            throw new InvalidOperationException("空图片。");
        }

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, FileName(localTime));
        File.WriteAllBytes(path, png);
        return path;
    }

    public static void WritePng(string path, byte[] png)
    {
        if (png.Length == 0)
        {
            throw new InvalidOperationException("空图片。");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("没有保存路径。", nameof(path));
        }

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, png);
    }

    private static void ForceOpaque(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);
        try
        {
            var count = Math.Abs(data.Stride) * bitmap.Height;
            var bytes = new byte[count];
            Marshal.Copy(data.Scan0, bytes, 0, count);
            for (var i = 3; i < count; i += 4)
            {
                bytes[i] = 255;
            }

            Marshal.Copy(bytes, 0, data.Scan0, count);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

public sealed class StillPixels
{
    public StillPixels(int width, int height, byte[] bgra)
    {
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Bgra { get; }
}

public sealed class StillFrame : IDisposable
{
    private readonly Bitmap _bitmap;

    internal StillFrame(Bitmap bitmap, int originX, int originY)
    {
        _bitmap = bitmap;
        OriginX = originX;
        OriginY = originY;
    }

    public int OriginX { get; }
    public int OriginY { get; }
    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    public byte[] SlicePng(int x, int y, int width, int height) => Crop(x, y, width, height);

    public StillPixels Slice(int x, int y, int width, int height)
    {
        var localX = x - OriginX;
        var localY = y - OriginY;
        if (width <= 0 || height <= 0 || localX < 0 || localY < 0 || localX + width > _bitmap.Width || localY + height > _bitmap.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        var data = _bitmap.LockBits(new Rectangle(localX, localY, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[width * height * 4];
            var rowBytes = width * 4;
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * rowBytes, rowBytes);
            }

            return new StillPixels(width, height, pixels);
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }
    }

    public byte[] CropPng(int x, int y, int width, int height, IReadOnlyList<SnipMark>? marks = null)
    {
        if (!StillShot.FormsRegion(width, height))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        return Crop(x, y, width, height, marks);
    }

    public void Dispose() => _bitmap.Dispose();

    private byte[] Crop(int x, int y, int width, int height, IReadOnlyList<SnipMark>? marks = null)
    {
        var localX = x - OriginX;
        var localY = y - OriginY;
        using var piece = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(piece))
        {
            graphics.DrawImage(
                _bitmap,
                new Rectangle(0, 0, width, height),
                new Rectangle(localX, localY, width, height),
                GraphicsUnit.Pixel);
            if (marks is { Count: > 0 })
            {
                SnipInk.Draw(graphics, x, y, marks);
            }
        }

        using var stream = new MemoryStream();
        piece.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
