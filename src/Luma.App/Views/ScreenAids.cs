using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Luma.App.Views;

/// <summary>
/// A topmost per-pixel-alpha window. Unlike Luma's own chrome it is not excluded from capture,
/// so display and region recordings include what it draws.
/// </summary>
internal sealed class LayeredSurface : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExLayered = 0x80000;
    private const int WsExTopmost = 0x8;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x8000000;
    private const int WsExTransparent = 0x20;
    private const int GwlExStyle = -20;

    private static readonly Dictionary<nint, LayeredSurface> Live = [];
    private static readonly Native.WndProc SharedProc = Dispatch;
    private static readonly HashSet<string> Registered = [];

    private readonly Func<uint, nint, nint, nint?>? _handler;
    private nint _memDc;
    private nint _dib;
    private nint _oldBitmap;
    private Bitmap? _bitmap;

    public LayeredSurface(string className, int width, int height, bool penCursor, Func<uint, nint, nint, nint?>? handler = null)
    {
        _handler = handler;
        var instance = Native.GetModuleHandle(null);
        if (Registered.Add(className))
        {
            var wndClass = new Native.WndClassEx
            {
                cbSize = (uint)Marshal.SizeOf<Native.WndClassEx>(),
                lpfnWndProc = SharedProc,
                hInstance = instance,
                hCursor = Native.LoadCursor(0, penCursor ? 32631 : 32512),
                lpszClassName = className
            };
            Native.RegisterClassEx(ref wndClass);
        }

        Hwnd = Native.CreateWindowEx(
            WsExLayered | WsExTopmost | WsExToolWindow | WsExNoActivate | WsExTransparent,
            className, "Luma", WsPopup, 0, 0, width, height, 0, 0, instance, 0);
        Live[Hwnd] = this;
        Resize(width, height);
    }

    public nint Hwnd { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public Graphics Canvas { get; private set; } = null!;

    public void Resize(int width, int height)
    {
        ReleaseBitmap();
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        var screen = Native.GetDC(0);
        _memDc = Native.CreateCompatibleDC(screen);
        Native.ReleaseDC(0, screen);
        var info = new Native.BitmapInfoHeader
        {
            biSize = Marshal.SizeOf<Native.BitmapInfoHeader>(),
            biWidth = Width,
            biHeight = -Height,
            biPlanes = 1,
            biBitCount = 32
        };
        _dib = Native.CreateDIBSection(_memDc, ref info, 0, out var bits, 0, 0);
        _oldBitmap = Native.SelectObject(_memDc, _dib);
        _bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppPArgb, bits);
        Canvas = Graphics.FromImage(_bitmap);
        Canvas.SmoothingMode = SmoothingMode.AntiAlias;
        Canvas.Clear(Color.Transparent);
    }

    public void Present(int x, int y)
    {
        var screen = Native.GetDC(0);
        var position = new Native.Point { X = x, Y = y };
        var size = new Native.Size { Cx = Width, Cy = Height };
        var source = new Native.Point();
        var blend = new Native.BlendFunction { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        Native.UpdateLayeredWindow(Hwnd, screen, ref position, ref size, _memDc, ref source, 0, ref blend, 2);
        Native.ReleaseDC(0, screen);
    }

    public void Show() => Native.ShowWindow(Hwnd, 4);

    public void Hide() => Native.ShowWindow(Hwnd, 0);

    public void SetClickThrough(bool through)
    {
        var style = Native.GetWindowLong(Hwnd, GwlExStyle);
        style = through ? style | WsExTransparent : style & ~WsExTransparent;
        Native.SetWindowLong(Hwnd, GwlExStyle, style);
    }

    public void Dispose()
    {
        Live.Remove(Hwnd);
        Native.DestroyWindow(Hwnd);
        ReleaseBitmap();
    }

    private void ReleaseBitmap()
    {
        Canvas?.Dispose();
        _bitmap?.Dispose();
        _bitmap = null;
        if (_memDc != 0)
        {
            Native.SelectObject(_memDc, _oldBitmap);
            Native.DeleteObject(_dib);
            Native.DeleteDC(_memDc);
            _memDc = 0;
        }
    }

    private static nint Dispatch(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (Live.TryGetValue(hwnd, out var surface) && surface._handler?.Invoke(msg, wParam, lParam) is { } handled)
        {
            return handled;
        }

        return Native.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    internal static class Native
    {
        public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);
        public delegate nint HookProc(int code, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public nint hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfoHeader
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] public struct Size { public int Cx; public int Cy; }
        [StructLayout(LayoutKind.Sequential)] public struct BlendFunction { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassEx(ref WndClassEx wndClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);
        [DllImport("user32.dll")] public static extern nint LoadCursor(nint instance, int cursor);
        [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int command);
        [DllImport("user32.dll")] public static extern int GetWindowLong(nint hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(nint hwnd, int index, int value);
        [DllImport("user32.dll")] public static extern nint GetDC(nint hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(nint hwnd, nint dc);
        [DllImport("user32.dll")]
        public static extern bool UpdateLayeredWindow(nint hwnd, nint dst, ref Point position, ref Size size, nint src, ref Point source, int key, ref BlendFunction blend, int flags);
        [DllImport("user32.dll")] public static extern nint SetCapture(nint hwnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hwnd, int id);
        [DllImport("user32.dll")] public static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
        [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
        [DllImport("user32.dll")] public static extern nint MonitorFromPoint(Point point, uint flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
        [DllImport("gdi32.dll")] public static extern nint CreateCompatibleDC(nint dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(nint dc);
        [DllImport("gdi32.dll")] public static extern nint SelectObject(nint dc, nint obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint obj);
        [DllImport("gdi32.dll")]
        public static extern nint CreateDIBSection(nint dc, ref BitmapInfoHeader info, uint usage, out nint bits, nint section, uint offset);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);

        public static double ScaleAt(int x, int y)
        {
            var monitor = MonitorFromPoint(new Point { X = x, Y = y }, 2);
            return GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1;
        }
    }
}

/// <summary>A full-monitor drawing layer. While the pen is off it lets every click through.</summary>
public sealed class ScreenInkOverlay : IDisposable
{
    private const uint WmMouseMove = 0x200;
    private const uint WmLButtonDown = 0x201;
    private const uint WmLButtonUp = 0x202;
    private const uint WmRButtonDown = 0x204;
    private const uint WmMouseActivate = 0x21;
    private const uint WmHotkey = 0x312;
    private const int EscapeHotkey = 0x4C55;

    // Pen mode needs a hit-testable surface, and fully transparent pixels of a layered window aren't.
    private static readonly Color PenBackdrop = Color.FromArgb(1, 0, 0, 0);

    private readonly int _x;
    private readonly int _y;
    private readonly LayeredSurface _surface;
    private readonly Bitmap _strokes;
    private readonly Graphics _strokeCanvas;
    private readonly Pen _pen;
    private System.Drawing.Point? _last;
    private long _lastPresent;

    public ScreenInkOverlay(int x, int y, int width, int height)
    {
        _x = x;
        _y = y;
        _surface = new LayeredSurface("LumaInk", width, height, penCursor: true, OnMessage);
        _strokes = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        _strokeCanvas = Graphics.FromImage(_strokes);
        _strokeCanvas.SmoothingMode = SmoothingMode.AntiAlias;
        _strokeCanvas.Clear(Color.Transparent);
        var scale = LayeredSurface.Native.ScaleAt(x + width / 2, y + height / 2);
        _pen = new Pen(Color.FromArgb(235, 45, 45), (float)(4 * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        Compose();
        _surface.Show();
    }

    public bool IsDrawing { get; private set; }

    internal nint Handle => _surface.Hwnd;

    public void SetDrawing(bool drawing)
    {
        if (IsDrawing == drawing)
        {
            return;
        }

        IsDrawing = drawing;
        _surface.SetClickThrough(!drawing);
        if (drawing)
        {
            LayeredSurface.Native.RegisterHotKey(_surface.Hwnd, EscapeHotkey, 0, 0x1B);
        }
        else
        {
            LayeredSurface.Native.UnregisterHotKey(_surface.Hwnd, EscapeHotkey);
            EndStroke();
        }

        Compose();
    }

    public void Clear()
    {
        _strokeCanvas.Clear(Color.Transparent);
        Compose();
    }

    public void Dispose()
    {
        LayeredSurface.Native.UnregisterHotKey(_surface.Hwnd, EscapeHotkey);
        _surface.Dispose();
        _strokeCanvas.Dispose();
        _strokes.Dispose();
        _pen.Dispose();
    }

    private void Compose()
    {
        _surface.Canvas.Clear(IsDrawing ? PenBackdrop : Color.Transparent);
        _surface.Canvas.DrawImageUnscaled(_strokes, 0, 0);
        _surface.Present(_x, _y);
    }

    private nint? OnMessage(uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmMouseActivate:
                return 3;
            case WmHotkey when wParam.ToInt32() == EscapeHotkey:
            case WmRButtonDown:
                SetDrawing(false);
                return 0;
            case WmLButtonDown when IsDrawing:
                LayeredSurface.Native.SetCapture(_surface.Hwnd);
                _last = PointOf(lParam);
                Segment(_last.Value, _last.Value);
                Present(force: true);
                return 0;
            case WmMouseMove when _last is { } from:
                var to = PointOf(lParam);
                Segment(from, to);
                _last = to;
                Present(force: false);
                return 0;
            case WmLButtonUp when _last is not null:
                EndStroke();
                return 0;
            default:
                return null;
        }
    }

    private void EndStroke()
    {
        if (_last is null)
        {
            return;
        }

        _last = null;
        LayeredSurface.Native.ReleaseCapture();
        Present(force: true);
    }

    private void Segment(System.Drawing.Point from, System.Drawing.Point to)
    {
        _strokeCanvas.DrawLine(_pen, from, to);
        _surface.Canvas.DrawLine(_pen, from, to);
    }

    private void Present(bool force)
    {
        var now = Environment.TickCount64;
        if (force || now - _lastPresent >= 12)
        {
            _lastPresent = now;
            _surface.Present(_x, _y);
        }
    }

    private static System.Drawing.Point PointOf(nint lParam)
    {
        var value = lParam.ToInt64();
        return new System.Drawing.Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
    }
}
