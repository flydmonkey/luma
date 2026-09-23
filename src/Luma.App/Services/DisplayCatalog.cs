using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Luma.App.Services;

public sealed record DisplayInfo(int Index, int X, int Y, int Width, int Height, string DeviceName);

public sealed record WindowInfo(nint Handle, string Title, string ClassName, string ProcessName, int X, int Y, int Width, int Height, bool Minimized)
{
    public string ObsWindowId => $"{Title}:{ClassName}:{ProcessName}";
}

public static class DisplayCatalog
{
    public static IReadOnlyList<DisplayInfo> ListDisplays()
    {
        var list = new List<DisplayInfo>();
        var index = 0;
        EnumDisplayMonitors(0, 0, (nint monitor, nint _, ref Rect _, nint _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
                var name = EnumDisplayDevices(info.Device, 0, ref device, 0) ? device.DeviceString : info.Device;
                var rect = info.Monitor;
                list.Add(new DisplayInfo(index, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, name));
                index++;
            }

            return true;
        }, 0);
        if (list.Count == 0)
        {
            list.Add(new DisplayInfo(0, 0, 0, GetSystemMetrics(0), GetSystemMetrics(1), "DISPLAY1"));
        }

        return list;
    }

    public static IReadOnlyList<WindowInfo> ListWindows(bool gamesOnly)
    {
        var list = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4) != 0)
            {
                return true;
            }

            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            GetWindowText(hwnd, title, title.Capacity);
            var text = title.ToString().Trim();
            if (text.Length == 0 || text is "Program Manager" or "Luma")
            {
                return true;
            }

            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            GetWindowThreadProcessId(hwnd, out var pid);
            var processName = "unknown.exe";
            try
            {
                processName = Process.GetProcessById((int)pid).ProcessName + ".exe";
            }
            catch
            {
                // process may have exited
            }

            if (!GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var minimized = IsIconic(hwnd);
            if (gamesOnly && (minimized || width < 800 || height < 600))
            {
                return true;
            }

            list.Add(new WindowInfo(hwnd, text, cls.ToString(), processName, rect.Left, rect.Top, width, height, minimized));
            return true;
        }, 0);
        return list;
    }

    public static string? CaptureMonitorPng(DisplayInfo display)
    {
        try
        {
            using var bitmap = new System.Drawing.Bitmap(Math.Max(1, display.Width), Math.Max(1, display.Height));
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(display.X, display.Y, 0, 0, bitmap.Size);
            var path = Path.Combine(Path.GetTempPath(), $"luma-region-{Guid.NewGuid():N}.png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private delegate bool MonitorProc(nint monitor, nint hdc, ref Rect rect, nint data);
    private delegate bool EnumProc(nint hwnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorProc proc, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? device, uint devNum, ref DisplayDevice displayDevice, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder className, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
}
