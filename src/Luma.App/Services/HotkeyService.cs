using System.Runtime.InteropServices;
using Luma.Core.Capture;
using Luma.Core.Settings;

namespace Luma.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HwndMessage = -3;
    private readonly NativeMethods.WndProc _proc;
    private nint _hwnd;

    public event Action? StartPressed;
    public event Action? PausePressed;
    public event Action? StopPressed;
    public event Action? ScreenshotPressed;

    public HotkeyService()
    {
        _proc = WndProc;
        var className = "LumaHotkeyWnd" + Guid.NewGuid().ToString("N");
        var wndClass = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            lpfnWndProc = _proc,
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = className
        };
        NativeMethods.RegisterClassEx(ref wndClass);
        _hwnd = NativeMethods.CreateWindowEx(0, className, "LumaHotkeys", 0, 0, 0, 0, 0, HwndMessage, 0, wndClass.hInstance, 0);
    }

    public void Apply(HotkeySettings settings)
    {
        Unregister();
        if (!settings.Enabled || _hwnd == 0)
        {
            return;
        }

        var failed = new List<string>();
        TryRegister(1, settings.Start, failed);
        TryRegister(2, settings.Pause, failed);
        TryRegister(3, settings.Stop, failed);
        TryRegister(4, settings.Screenshot, failed);
        if (failed.Count > 0)
        {
            throw new InvalidOperationException("无法注册热键 " + string.Join("、", failed) + "。");
        }
    }

    public void Dispose()
    {
        Unregister();
        if (_hwnd != 0)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }

    private void TryRegister(int id, string gesture, List<string> failed)
    {
        if (StillShot.IsSystemSnip(gesture))
        {
            failed.Add(gesture);
            return;
        }

        Parse(gesture, out var modifiers, out var key);
        if (key == 0)
        {
            return;
        }

        if (!NativeMethods.RegisterHotKey(_hwnd, id, modifiers, key))
        {
            failed.Add(gesture);
        }
    }

    private void Unregister()
    {
        if (_hwnd == 0)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, 1);
        NativeMethods.UnregisterHotKey(_hwnd, 2);
        NativeMethods.UnregisterHotKey(_hwnd, 3);
        NativeMethods.UnregisterHotKey(_hwnd, 4);
    }

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmHotkey)
        {
            switch (wParam.ToInt32())
            {
                case 1: StartPressed?.Invoke(); break;
                case 2: PausePressed?.Invoke(); break;
                case 3: StopPressed?.Invoke(); break;
                case 4: ScreenshotPressed?.Invoke(); break;
            }

            return 0;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public static string FormatGesture(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        if (!string.IsNullOrWhiteSpace(key)) parts.Add(key);
        return string.Join("+", parts);
    }

    private static void Parse(string gesture, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        foreach (var raw in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = raw.ToUpperInvariant();
            switch (part)
            {
                case "CTRL" or "CONTROL": modifiers |= 0x0002; break;
                case "ALT": modifiers |= 0x0001; break;
                case "SHIFT": modifiers |= 0x0004; break;
                case "WIN": modifiers |= 0x0008; break;
                default:
                    key = part.Length == 1 ? part[0] : part switch
                    {
                        "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                        "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                        "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                        _ => part.Length > 0 ? (uint)part[0] : 0
                    };
                    break;
            }
        }
    }

    private static class NativeMethods
    {
        public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

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

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hWnd, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassEx(ref WndClassEx lpwcx);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? lpModuleName);
    }
}
