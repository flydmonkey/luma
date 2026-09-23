using System.Text.Json;

namespace Luma.Core.Library;

public static class LibraryFileInfo
{
    public const string FileName = ".luma-files.json";
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string IndexPath(string saveFolder) => Path.Combine(saveFolder, FileName);

    public static TimeSpan? TryGet(string mediaPath)
    {
        var folder = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        var name = Path.GetFileName(mediaPath);
        return Read(folder).TryGetValue(name, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    public static IReadOnlyDictionary<string, double> Read(string saveFolder)
    {
        lock (Gate)
        {
            return Load(saveFolder);
        }
    }

    public static void Remember(string mediaPath, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var folder = Path.GetDirectoryName(mediaPath);
        var name = Path.GetFileName(mediaPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
        {
            return;
        }

        lock (Gate)
        {
            var map = Load(folder);
            map[name] = duration.TotalSeconds;
            Save(folder, map);
        }
    }

    public static void Forget(string mediaPath)
    {
        var folder = Path.GetDirectoryName(mediaPath);
        var name = Path.GetFileName(mediaPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
        {
            return;
        }

        lock (Gate)
        {
            var map = Load(folder);
            if (!Remove(map, name))
            {
                return;
            }

            Save(folder, map);
        }
    }

    public static void Move(string oldPath, string newPath)
    {
        var folder = Path.GetDirectoryName(oldPath);
        var oldName = Path.GetFileName(oldPath);
        var newName = Path.GetFileName(newPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
        {
            return;
        }

        lock (Gate)
        {
            var map = Load(folder);
            if (!TryTake(map, oldName, out var seconds))
            {
                return;
            }

            map[newName] = seconds;
            Save(folder, map);
        }
    }

    private static Dictionary<string, double> Load(string saveFolder)
    {
        var path = IndexPath(saveFolder);
        if (!File.Exists(path))
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path), JsonOptions);
            return loaded is null
                ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, double>(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Save(string saveFolder, Dictionary<string, double> map)
    {
        Directory.CreateDirectory(saveFolder);
        File.WriteAllText(IndexPath(saveFolder), JsonSerializer.Serialize(map, JsonOptions));
    }

    private static bool TryTake(Dictionary<string, double> map, string name, out double seconds)
    {
        if (map.Remove(name, out seconds))
        {
            return true;
        }

        var match = map.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            seconds = 0;
            return false;
        }

        seconds = map[match];
        map.Remove(match);
        return true;
    }

    private static bool Remove(Dictionary<string, double> map, string name) => TryTake(map, name, out _);
}
