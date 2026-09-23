using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Luma.Core.Capture;
using Windows.Devices.Enumeration;

namespace Luma.App.Services;

public static class LanTargetCatalog
{
    public static WebStillPayload CaptureWebStill()
    {
        var monitors = DisplayCatalog.ListDisplays()
            .Select(display => (display.X, display.Y, display.Width, display.Height))
            .ToArray();
        var windows = DisplayCatalog.ListWindows(false)
            .Where(window => !window.Minimized && window.Width > 0 && window.Height > 0)
            .Select(window => (
                window.ObsWindowId,
                Title: string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title,
                window.X,
                window.Y,
                window.Width,
                window.Height))
            .ToArray();
        return WebStill.Capture(monitors, windows);
    }

    public static WebStillPayload? CaptureWindowStill(int index)
    {
        var windows = DisplayCatalog.ListWindows(false);
        if ((uint)index >= (uint)windows.Count)
        {
            return null;
        }

        var window = windows[index];
        if (window.Minimized || window.Handle == 0)
        {
            return null;
        }

        Bitmap? bitmap = null;
        try
        {
            if (!TryCaptureWindow(window.Handle, out bitmap))
            {
                if (!TryBounds(window.Handle, out var x, out var y, out var width, out var height))
                {
                    return null;
                }

                bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }

            MakeOpaque(bitmap);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            var title = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title;
            return WebStill.Describe(
                stream.ToArray(),
                [(0, 0, bitmap.Width, bitmap.Height)],
                [(window.ObsWindowId, title, 0, 0, bitmap.Width, bitmap.Height)]);
        }
        catch
        {
            return null;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    public static object List(string? kind)
    {
        return (kind ?? "displays").ToLowerInvariant() switch
        {
            "windows" => Windows(),
            "games" => Array.Empty<object>(),
            "cameras" => Devices(DeviceClass.VideoCapture),
            "microphones" or "mics" => Devices(DeviceClass.AudioCapture),
            _ => Displays()
        };
    }

    private static object Displays()
        => DisplayCatalog.ListDisplays().Select(display => new
        {
            id = display.Index.ToString(),
            title = $"{display.Width}×{display.Height}",
            label = $"{display.Index + 1}  {display.DeviceName}  {display.Width}×{display.Height}",
            detail = string.IsNullOrWhiteSpace(display.DeviceName) ? $"显示器 {display.Index + 1}" : display.DeviceName,
            preview = Preview(display.X, display.Y, display.Width, display.Height)
        }).ToArray();

    private static object Windows()
        => DisplayCatalog.ListWindows(false).Select(window => new
        {
            id = window.ObsWindowId,
            title = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title,
            label = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title,
            detail = window.Minimized ? "已最小化" : $"{window.Width}×{window.Height}",
            preview = window.Minimized ? null : PreviewWindow(window)
        }).ToArray();

    private static string? PreviewWindow(WindowInfo window)
    {
        if (window.Handle != 0 && TryCaptureWindow(window.Handle, out var captured))
        {
            using (captured)
            {
                return Encode(captured);
            }
        }

        return Preview(window.X, window.Y, window.Width, window.Height);
    }

    private static bool TryCaptureWindow(nint hwnd, out Bitmap bitmap)
    {
        bitmap = null!;
        if (!TryBounds(hwnd, out _, out _, out var width, out var height))
        {
            return false;
        }

        var source = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            if (TryPrintWindow(hwnd, source) && !IsMostlyBlank(source))
            {
                bitmap = source;
                return true;
            }

            if (TryBitBlt(hwnd, source) && !IsMostlyBlank(source))
            {
                bitmap = source;
                return true;
            }
        }
        catch
        {
            source.Dispose();
            return false;
        }

        source.Dispose();
        return false;
    }

    private static bool TryBounds(nint hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var frame, Marshal.SizeOf<Rect>()) == 0
            && frame.Width > 0 && frame.Height > 0)
        {
            rect = frame;
        }

        x = rect.Left;
        y = rect.Top;
        width = rect.Width;
        height = rect.Height;
        return width >= 8 && height >= 8;
    }

    private static bool TryPrintWindow(nint hwnd, Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            return PrintWindow(hwnd, hdc, PwRenderFullContent) || PrintWindow(hwnd, hdc, 0);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    private static bool TryBitBlt(nint hwnd, Bitmap bitmap)
    {
        var source = GetWindowDC(hwnd);
        if (source == 0)
        {
            return false;
        }

        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            try
            {
                return BitBlt(hdc, 0, 0, bitmap.Width, bitmap.Height, source, 0, 0, Srccopy);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }
        finally
        {
            ReleaseDC(hwnd, source);
        }
    }

    private static bool IsMostlyBlank(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var samples = 0;
            var colored = 0;
            var step = Math.Max(16, Math.Min(bitmap.Width, bitmap.Height) / 24) * 4;
            for (var y = 0; y < bitmap.Height; y += Math.Max(1, step / 4))
            {
                var row = data.Scan0 + (y * data.Stride);
                for (var x = 0; x + 2 < bitmap.Width * 4; x += step)
                {
                    samples++;
                    var b = Marshal.ReadByte(row, x);
                    var g = Marshal.ReadByte(row, x + 1);
                    var r = Marshal.ReadByte(row, x + 2);
                    if (r > 10 || g > 10 || b > 10)
                    {
                        colored++;
                    }
                }
            }

            return samples == 0 || colored * 20 < samples;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static string? Encode(Bitmap source)
    {
        MakeOpaque(source);
        var thumbWidth = Math.Min(source.Width, 480);
        var thumbHeight = Math.Max(1, (int)(source.Height * (thumbWidth / (double)source.Width)));
        using var scaled = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.DrawImage(source, 0, 0, thumbWidth, thumbHeight);
        }

        using var stream = new MemoryStream();
        scaled.Save(stream, ImageFormat.Jpeg);
        return "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
    }

    private static void MakeOpaque(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var row = data.Scan0 + (y * data.Stride);
                for (var x = 3; x < bitmap.Width * 4; x += 4)
                {
                    Marshal.WriteByte(row, x, 255);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static string? Preview(int x, int y, int width, int height)
    {
        if (width < 8 || height < 8)
        {
            return null;
        }

        try
        {
            var thumbWidth = Math.Min(width, 480);
            var thumbHeight = Math.Max(1, (int)(height * (thumbWidth / (double)width)));
            using var source = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            }

            using var scaled = new Bitmap(source, new Size(thumbWidth, thumbHeight));
            using var stream = new MemoryStream();
            scaled.Save(stream, ImageFormat.Jpeg);
            return "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static object Devices(DeviceClass deviceClass)
    {
        try
        {
            var devices = DeviceInformation.FindAllAsync(deviceClass).AsTask().GetAwaiter().GetResult();
            return devices.Select(device => new
            {
                id = device.Id,
                label = device.Name,
                title = device.Name,
                detail = "",
                preview = (string?)null
            }).ToArray();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }

    private const uint PwRenderFullContent = 2;
    private const uint Srccopy = 0x00CC0020;
    private const int DwmwaExtendedFrameBounds = 9;

    [DllImport("user32.dll")] private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern nint GetWindowDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attr, out Rect rect, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
}
