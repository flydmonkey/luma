using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Luma.App.Views;

/// <summary>Shows a fading ring wherever the mouse is clicked, so viewers can follow the pointer.</summary>
internal sealed class ClickHighlighter : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x201;
    private const int WmRButtonDown = 0x204;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(420);

    private readonly LayeredSurface.Native.HookProc _hookProc;
    private readonly DispatcherQueueTimer _timer;
    private LayeredSurface? _surface;
    private nint _hook;
    private int _x;
    private int _y;
    private bool _right;
    private double _scale = 1;
    private DateTime _started;

    public ClickHighlighter(DispatcherQueue dispatcher)
    {
        _hookProc = OnMouse;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += (_, _) => Paint();
        _hook = LayeredSurface.Native.SetWindowsHookEx(WhMouseLl, _hookProc, LayeredSurface.Native.GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_hook != 0)
        {
            LayeredSurface.Native.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        _surface?.Dispose();
        _surface = null;
    }

    private nint OnMouse(int code, nint wParam, nint lParam)
    {
        var message = wParam.ToInt32();
        if (code >= 0 && message is WmLButtonDown or WmRButtonDown)
        {
            _x = Marshal.ReadInt32(lParam);
            _y = Marshal.ReadInt32(lParam, 4);
            _right = message == WmRButtonDown;
            _started = DateTime.UtcNow;
            _timer.Start();
        }

        return LayeredSurface.Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Paint()
    {
        var progress = (DateTime.UtcNow - _started) / Duration;
        if (progress >= 1)
        {
            _timer.Stop();
            _surface?.Hide();
            return;
        }

        var scale = LayeredSurface.Native.ScaleAt(_x, _y);
        var size = (int)Math.Ceiling(72 * scale);
        if (_surface is null || Math.Abs(scale - _scale) > 0.01)
        {
            _surface?.Dispose();
            _surface = new LayeredSurface("LumaClickRing", size, size, penCursor: false);
            _scale = scale;
        }

        var canvas = _surface.Canvas;
        canvas.Clear(Color.Transparent);
        var eased = 1 - Math.Pow(1 - progress, 3);
        var radius = (float)(size / 2.0 * (0.3 + 0.62 * eased));
        var alpha = (int)(255 * (1 - progress));
        var tint = _right ? Color.FromArgb(80, 160, 255) : Color.FromArgb(255, 196, 0);
        var center = size / 2f;
        using (var fill = new SolidBrush(Color.FromArgb(alpha / 4, tint)))
        {
            canvas.FillEllipse(fill, center - radius, center - radius, radius * 2, radius * 2);
        }

        using (var ring = new Pen(Color.FromArgb(alpha, tint), (float)(3 * scale)))
        {
            canvas.DrawEllipse(ring, center - radius, center - radius, radius * 2, radius * 2);
        }

        _surface.Present(_x - size / 2, _y - size / 2);
        _surface.Show();
    }
}
