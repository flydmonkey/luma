using Microsoft.UI.Xaml;

namespace Luma.App.Services;

public static class AppIcon
{
    public static string IdlePath => Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");

    public static string RecordingPath => Path.Combine(AppContext.BaseDirectory, "Assets", "App.recording.ico");

    public static void Apply(Window window)
    {
        if (window.AppWindow is not null && File.Exists(IdlePath))
        {
            window.AppWindow.SetIcon(IdlePath);
        }
    }
}
