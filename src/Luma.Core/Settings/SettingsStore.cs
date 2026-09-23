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

            AppSettings loaded;
            try
            {
                var json = File.ReadAllText(_filePath);
                loaded = string.IsNullOrWhiteSpace(json)
                    ? throw new JsonException("设置文件为空。")
                    : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                      ?? throw new JsonException("设置文件没有有效内容。");
            }
            catch (JsonException)
            {
                return RecoverDefaults();
            }

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
            loaded.Automation.StartAtLogon = false;
            loaded.Automation.Schedules = [];
            if (loaded.LastMode == CaptureMode.Game)
            {
                loaded.LastMode = CaptureMode.Display;
            }

            return loaded;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            SaveCore(settings);
        }
    }

    public AppSettings Reset()
    {
        var settings = new AppSettings();
        Save(settings);
        return settings;
    }

    private AppSettings RecoverDefaults()
    {
        var backup = $"{_filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Copy(_filePath, backup, overwrite: false);
        }
        catch
        {
            // Recreating a usable settings file takes priority over preserving the damaged copy.
        }

        var settings = new AppSettings();
        SaveCore(settings);
        return settings;
    }

    private void SaveCore(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(settings.SaveFolder);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); } catch { /* best-effort temporary-file cleanup */ }
            }
        }
    }
}
