using System.Runtime.InteropServices;
using Microsoft.Win32;
using Luma.Core.Settings;
using Windows.UI.ViewManagement;

namespace Luma.App.Services;

internal static class NativeMenuTheme
{
    private const int AllowDark = 1;
    private const int ForceDark = 2;
    private const int ForceLight = 3;

    public static void Apply(AppThemeMode mode)
    {
        try
        {
            SetPreferredAppMode(AppTheme.IsDark(mode) ? ForceDark : ForceLight);
            FlushMenuThemes();
        }
        catch
        {
            try
            {
                SetPreferredAppMode(AllowDark);
                FlushMenuThemes();
            }
            catch
            {
                // keep the process running even if menu theme APIs are missing
            }
        }
    }

    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return new UISettings().GetColorValue(UIColorType.Background).R < 128;
        }
    }

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int preferredAppMode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();
}
