using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Luma.Core.Settings;
using Windows.UI;

namespace Luma.App.Services;

public static class AppTheme
{
    public static ElementTheme Resolve(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => ElementTheme.Light,
        AppThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    public static ApplicationTheme? ApplicationOverride(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => ApplicationTheme.Light,
        AppThemeMode.Dark => ApplicationTheme.Dark,
        _ => null
    };

    public static void ApplyToWindow(Window window, AppThemeMode mode)
    {
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = Resolve(mode);
        }

        ApplyTitleBar(window.AppWindow?.TitleBar, IsDark(mode));
    }

    public static bool IsDark(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => false,
        AppThemeMode.Dark => true,
        _ => NativeMenuTheme.SystemUsesDarkTheme()
    };

    public static void ApplyTitleBar(AppWindowTitleBar? bar, bool dark)
    {
        if (bar is null)
        {
            return;
        }

        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        bar.ButtonInactiveForegroundColor = dark
            ? Color.FromArgb(255, 160, 160, 160)
            : Color.FromArgb(255, 96, 96, 96);
        bar.ButtonHoverBackgroundColor = dark
            ? Color.FromArgb(24, 255, 255, 255)
            : Color.FromArgb(16, 0, 0, 0);
        bar.ButtonPressedBackgroundColor = dark
            ? Color.FromArgb(40, 255, 255, 255)
            : Color.FromArgb(24, 0, 0, 0);
    }
}
