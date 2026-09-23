using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Luma.Core.Capture;
using Luma.Core.Localization;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Luma.App.Services;

public static class StillShotOcr
{
    public static async Task<string?> RecognizeAsync(StillPixels pixels)
    {
        var engine = CreateEngine();
        if (engine is null)
        {
            return null;
        }

        var (width, height, bgra) = Fit(pixels, OcrEngine.MaxImageDimension);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            bgra.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Ignore);
        var result = await engine.RecognizeAsync(bitmap);
        return result.Text?.Trim() ?? "";
    }

    public static async Task<string?> RecognizePngAsync(byte[] png)
    {
        if (png.Length == 0)
        {
            return "";
        }

        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = bitmap.Width * 4;
            var pixels = new byte[rowBytes * bitmap.Height];
            for (var row = 0; row < bitmap.Height; row++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * rowBytes, rowBytes);
            }

            return await RecognizeAsync(new StillPixels(bitmap.Width, bitmap.Height, pixels)).ConfigureAwait(false);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static OcrEngine? CreateEngine()
    {
        var preferred = new Language(UiLanguages.FrameworkTag(UiCopy.Lang));
        if (OcrEngine.IsLanguageSupported(preferred))
        {
            var match = OcrEngine.TryCreateFromLanguage(preferred);
            if (match is not null)
            {
                return match;
            }
        }

        var profile = OcrEngine.TryCreateFromUserProfileLanguages();
        if (profile is not null)
        {
            return profile;
        }

        foreach (var language in OcrEngine.AvailableRecognizerLanguages)
        {
            var engine = OcrEngine.TryCreateFromLanguage(language);
            if (engine is not null)
            {
                return engine;
            }
        }

        return null;
    }

    private static (int Width, int Height, byte[] Bgra) Fit(StillPixels pixels, uint maxEdge)
    {
        var limit = maxEdge == 0 ? int.MaxValue : (int)Math.Min(maxEdge, int.MaxValue);
        if (pixels.Width <= limit && pixels.Height <= limit)
        {
            return (pixels.Width, pixels.Height, pixels.Bgra);
        }

        var scale = Math.Min(limit / (double)pixels.Width, limit / (double)pixels.Height);
        var width = Math.Max(1, (int)Math.Floor(pixels.Width * scale));
        var height = Math.Max(1, (int)Math.Floor(pixels.Height * scale));
        using var source = BitmapFrom(pixels);
        using var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }

        return (width, height, Pack(scaled));
    }

    private static Bitmap BitmapFrom(StillPixels pixels)
    {
        var bitmap = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, pixels.Width, pixels.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = pixels.Width * 4;
            for (var row = 0; row < pixels.Height; row++)
            {
                Marshal.Copy(pixels.Bgra, row * rowBytes, IntPtr.Add(data.Scan0, row * data.Stride), rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static byte[] Pack(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = bitmap.Width * 4;
            var pixels = new byte[rowBytes * bitmap.Height];
            for (var row = 0; row < bitmap.Height; row++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * rowBytes, rowBytes);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
