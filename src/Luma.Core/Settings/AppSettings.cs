using Luma.Core.Session;

namespace Luma.Core.Settings;

public sealed class AppSettings
{
    public string SaveFolder { get; set; } = DefaultSaveFolder();
    public CaptureMode LastMode { get; set; } = CaptureMode.Display;
    public int MonitorIndex { get; set; }
    public QualitySettings Quality { get; set; } = QualitySettings.FromLevel(QualityLevel.Hd, 30);
    public string RecordingFormat { get; set; } = RecordingContainers.Mp4;
    public DisplayCaptureMethod DisplayCapture { get; set; } = DisplayCaptureMethod.Auto;
    public AudioSettings Audio { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public ScreenAidSettings ScreenAids { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public bool CloseToTray { get; set; } = true;
    public bool LaunchToTray { get; set; }
    public bool HideTrayIcon { get; set; }
    public bool ShowRecordingBar { get; set; } = true;
    public bool SilentMode { get; set; }
    public AppThemeMode Theme { get; set; } = AppThemeMode.Dark;
    public string UiLanguage { get; set; } = Localization.UiLanguages.System;
    public LanSettings Lan { get; set; } = new();

    public static string DefaultSaveFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(videos))
        {
            videos = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(videos, "Recordings");
    }

    public static string ResolveSaveFolder(string? folder)
    {
        return string.IsNullOrWhiteSpace(folder) ? DefaultSaveFolder() : folder;
    }
}

public enum AppThemeMode
{
    Light = 1,
    Dark = 2
}

public sealed class AudioSettings
{
    public bool CaptureSystem { get; set; } = true;
    public bool CaptureMicrophone { get; set; }
    public string? MicrophoneDeviceId { get; set; }
    public bool AudioOnly { get; set; }
    public int SystemVolume { get; set; } = 100;
    public int MicrophoneVolume { get; set; } = 100;
    public bool NoiseSuppression { get; set; } = true;

    public const int MaxVolume = 200;

    public static double Gain(int percent) => Math.Clamp(percent, 0, MaxVolume) / 100.0;
}

public sealed class ScreenAidSettings
{
    public bool ShowCursor { get; set; } = true;

    /// <summary>Draw a ring where the mouse clicks; recorded in display and region modes.</summary>
    public bool HighlightClicks { get; set; }

    /// <summary>Offer the pen on the recording bar; recorded in display and region modes.</summary>
    public bool Annotation { get; set; }

    public static readonly string[] InkColors = ["#EB2D2D", "#FFC400", "#22C55E", "#3B82F6", "#FFFFFF", "#111111"];
    public static readonly int[] InkWidths = [2, 4, 8];

    /// <summary>One of <see cref="InkColors"/>.</summary>
    public string InkColor { get; set; } = InkColors[0];

    /// <summary>Pen width in device-independent pixels; one of <see cref="InkWidths"/>.</summary>
    public int InkWidth { get; set; } = InkWidths[1];

    public static bool AppliesTo(CaptureMode mode) => mode is CaptureMode.Display or CaptureMode.Region;

    public static string NormalizeInkColor(string? color) =>
        InkColors.FirstOrDefault(item => string.Equals(item, color, StringComparison.OrdinalIgnoreCase)) ?? InkColors[0];

    public static int NormalizeInkWidth(int width) => InkWidths.Contains(width) ? width : InkWidths[1];
}

public sealed class OverlaySettings
{
    public bool CameraEnabled { get; set; }
    public string? CameraDeviceId { get; set; }
    public string? CameraDeviceName { get; set; }
    public double CameraX { get; set; } = 0.5;
    public double CameraY { get; set; } = 0.5;
    public double CameraWidth { get; set; } = 0.24;
    public double CameraHeight { get; set; } = 0.24;
    public List<WatermarkSettings> Watermarks { get; set; } = [];
}

public enum WatermarkKind
{
    Text = 0,
    Image = 1,
    Timestamp = 2
}

public sealed class WatermarkSettings
{
    public WatermarkKind Kind { get; set; }
    public string Content { get; set; } = "";
    public double X { get; set; } = 0.02;
    public double Y { get; set; } = 0.02;
    public double Width { get; set; } = 0.20;
    public double Height { get; set; } = 0.08;
    public double Opacity { get; set; } = 1.0;
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;
    public string Start { get; set; } = "Ctrl+Alt+R";
    public string Pause { get; set; } = "Ctrl+Alt+Shift+P";
    public string Stop { get; set; } = "Ctrl+Alt+S";
    public string Screenshot { get; set; } = Capture.StillShot.FactoryHotkey;
}

public sealed class AutomationSettings
{
    public bool StartAtLogon { get; set; }
    public bool SegmentEnabled { get; set; }
    public int SegmentMinutes { get; set; } = 10;
    public int SegmentMaxMegabytes { get; set; }
    public List<ScheduleRule> Schedules { get; set; } = [];
}

public sealed class ScheduleRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public TimeOnly Start { get; set; } = new(9, 0);
    public TimeOnly? End { get; set; } = new(10, 0);
    public int? DurationMinutes { get; set; }
    public CaptureMode Mode { get; set; } = CaptureMode.Display;
}

public sealed class LanSettings
{
    public const int DefaultPort = 12345;

    public bool Enabled { get; set; }
    public int Port { get; set; } = DefaultPort;
    public string AccessKey { get; set; } = "";
}
