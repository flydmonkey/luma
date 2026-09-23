using System.Text.Json;
using Luma.Core.Localization;
using Luma.Core.Session;

namespace Luma.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Luma",
            "settings.json");
    }

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
            {
                var created = new AppSettings();
                Directory.CreateDirectory(created.SaveFolder);
                return created;
            }

            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            loaded.Lan ??= new LanSettings();
            if (!LanPort.IsValid(loaded.Lan.Port))
            {
                loaded.Lan.Port = LanPort.Default;
            }

            loaded.Lan.AccessKey ??= "";
            loaded.UiLanguage = UiLanguages.Normalize(loaded.UiLanguage);
            loaded.SaveFolder = AppSettings.ResolveSaveFolder(loaded.SaveFolder);
            loaded.Quality ??= QualitySettings.FromLevel(QualityLevel.Hd, 30);
            loaded.RecordingFormat = RecordingContainers.Normalize(loaded.RecordingFormat);
            loaded.Audio ??= new AudioSettings();
            loaded.Overlay ??= new OverlaySettings();
            loaded.Hotkeys ??= new HotkeySettings();
            loaded.Hotkeys.Screenshot = Capture.StillShot.NormalizeHotkey(loaded.Hotkeys.Screenshot);
            loaded.Automation ??= new AutomationSettings();
            if (loaded.LastMode == CaptureMode.Game)
            {
                loaded.LastMode = CaptureMode.Display;
            }

            foreach (var rule in loaded.Automation.Schedules)
            {
                if (rule.Mode == CaptureMode.Game)
                {
                    rule.Mode = CaptureMode.Display;
                }
            }

            return loaded;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            Directory.CreateDirectory(settings.SaveFolder);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }

    public AppSettings Reset()
    {
        var settings = new AppSettings();
        Save(settings);
        return settings;
    }
}
