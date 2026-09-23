using System.Runtime.InteropServices;
using Luma.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace Luma.App.Views;

public static class SnipNotice
{
    private static Window? _window;

    public static void Show(string message)
    {
        _window?.Close();
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = new Window
        {
            Title = "Luma",
            Content = new Grid
            {
                Padding = new Thickness(16, 12, 16, 12),
                Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
                Children = { text }
            },
            SystemBackdrop = new DesktopAcrylicBackdrop()
        };
        window.AppWindow.IsShownInSwitchers = false;
        AppTheme.ApplyToWindow(window, App.Settings.Theme);
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        var hwnd = WindowNative.GetWindowHandle(window);
        try { SetWindowDisplayAffinity(hwnd, 0x11); } catch { }
        var display = DisplayCatalog.ListDisplays().FirstOrDefault();
        var x = display is null ? 80 : display.X + Math.Max(0, (display.Width - 360) / 2);
        var y = display is null ? 40 : display.Y + 48;
        window.AppWindow.Move(new PointInt32(x, y));
        window.AppWindow.Resize(new SizeInt32(360, 72));
        window.Activate();
        _window = window;
        var timer = window.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(3);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }

            try { window.Close(); } catch { }
        };
        timer.Start();
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
}
