using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Luma.App.Services;

public static class StillShotClipboard
{
    public static Task CopyPngAsync(byte[] png)
    {
        using var stream = new MemoryStream(png);
        using var image = new Bitmap(stream);
        CopyBitmap(image);
        return Task.CompletedTask;
    }

    public static void CopyText(string text)
    {
        var chars = Encoding.Unicode.GetBytes(text + "\0");
        var memory = GlobalAlloc(0x0002, (UIntPtr)chars.Length);
        if (memory == 0)
        {
            throw new InvalidOperationException("无法复制到剪贴板。");
        }

        try
        {
            var locked = GlobalLock(memory);
            Marshal.Copy(chars, 0, locked, chars.Length);
            GlobalUnlock(memory);
            if (!TryOpenClipboard() || !EmptyClipboard() || SetClipboardData(13, memory) == 0)
            {
                throw new InvalidOperationException("无法复制到剪贴板。");
            }

            memory = 0;
        }
        finally
        {
            CloseClipboard();
            if (memory != 0)
            {
                GlobalFree(memory);
            }
        }
    }

    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (OpenClipboard(0))
            {
                return true;
            }

            Thread.Sleep(15);
        }

        return false;
    }

    private static void CopyBitmap(Bitmap image)
    {
        var width = image.Width;
        var height = image.Height;
        var bounds = new Rectangle(0, 0, width, height);
        var bits = image.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        IntPtr memory = 0;
        try
        {
            var stride = Math.Abs(bits.Stride);
            var header = 40;
            var size = header + stride * height;
            memory = GlobalAlloc(0x0002, (UIntPtr)size);
            if (memory == 0)
            {
                throw new InvalidOperationException("无法分配剪贴板图像。");
            }

            var locked = GlobalLock(memory);
            Marshal.WriteInt32(locked, 0, 40);
            Marshal.WriteInt32(locked, 4, width);
            Marshal.WriteInt32(locked, 8, height);
            Marshal.WriteInt16(locked, 12, 1);
            Marshal.WriteInt16(locked, 14, 32);
            Marshal.WriteInt32(locked, 16, 0);
            var pixels = IntPtr.Add(locked, header);
            for (var row = 0; row < height; row++)
            {
                var source = IntPtr.Add(bits.Scan0, (height - 1 - row) * bits.Stride);
                var target = IntPtr.Add(pixels, row * stride);
                var bytes = new byte[width * 4];
                Marshal.Copy(source, bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }

            GlobalUnlock(memory);
            if (!OpenClipboard(0) || !EmptyClipboard() || SetClipboardData(8, memory) == 0)
            {
                throw new InvalidOperationException("无法复制到剪贴板。");
            }

            memory = 0;
        }
        finally
        {
            image.UnlockBits(bits);
            CloseClipboard();
            if (memory != 0)
            {
                GlobalFree(memory);
            }
        }
    }

    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint hwnd);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);
}
