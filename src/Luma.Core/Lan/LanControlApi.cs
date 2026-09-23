using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Luma.Core.Library;
using Luma.Core.Localization;
using Luma.Core.Settings;

namespace Luma.Core.Lan;

public sealed record LanJobView(string Id, string Kind, string Status, string? Result, string? Error);

public static class LanControlApi
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "theme", "saveFolder", "showRecordingBar", "silentMode", "closeToTray", "launchToTray",
        "hideTrayIcon", "monitorIndex", "lastMode", "quality", "audio", "overlay", "hotkeys",
        "automation", "uiLanguage", "recordingFormat"
    };

    private static readonly ConcurrentDictionary<string, LanJobView> Jobs = new(StringComparer.Ordinal);

    public static string Ok(object? data) => JsonSerializer.Serialize(new { ok = true, data, error = (string?)null }, Json);

    public static string Fail(string error) => JsonSerializer.Serialize(new { ok = false, data = (object?)null, error }, Json);

    public static object LibraryList(string folder)
    {
        var catalog = new LibraryCatalog();
        return catalog.List(folder).Select(item => new
        {
            id = item.Id,
            name = item.Name,
            length = item.SizeBytes,
            duration = item.Duration.ToString(),
            date = item.Created,
            isAudio = item.IsAudio
        }).ToArray();
    }

    public static LibraryItem? Find(string folder, string id)
    {
        var catalog = new LibraryCatalog();
        return catalog.List(folder).FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, id, StringComparison.OrdinalIgnoreCase));
    }

    public static object Rename(string folder, string id, string name)
    {
        var item = Find(folder, id) ?? throw new InvalidOperationException("未找到这个文件。");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("名称不能为空。");
        }

        var dest = new LibraryCatalog().Rename(item.Path, name.Trim());
        var updated = Find(folder, Path.GetFileNameWithoutExtension(dest))
            ?? throw new InvalidOperationException("重命名后未能读取记录。");
        return new
        {
            id = updated.Id,
            name = updated.Name,
            length = updated.SizeBytes,
            duration = updated.Duration.ToString(),
            date = updated.Created,
            isAudio = updated.IsAudio
        };
    }

    public static void Delete(string folder, string id, bool confirm)
    {
        if (!confirm)
        {
            throw new InvalidOperationException("删除需要 confirm=true。");
        }

        var item = Find(folder, id) ?? throw new InvalidOperationException("未找到这个文件。");
        new LibraryCatalog().Delete(item.Path);
    }

    public static JsonObject SettingsForClient(AppSettings settings)
    {
        var node = JsonSerializer.SerializeToNode(settings, Json)!.AsObject();
        if (node["lan"] is JsonObject lan)
        {
            lan["accessKey"] = string.IsNullOrEmpty(settings.Lan.AccessKey) ? "" : "***";
        }

        node["resolvedLanguage"] = UiLanguages.ResolveEffective(settings.UiLanguage);
        return node;
    }

    public static AppSettings ApplySettings(AppSettings source, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("设置必须是对象。");
        }

        foreach (var prop in patch.EnumerateObject())
        {
            if (prop.NameEquals("lan") || prop.NameEquals("lanPort") || prop.NameEquals("port") || prop.NameEquals("lanPlayback"))
            {
                throw new InvalidOperationException("不能从网页修改局域网端口或开关。");
            }

            if (!Allowed.Contains(prop.Name))
            {
                throw new InvalidOperationException($"不能修改字段 {prop.Name}。");
            }
        }

        var node = JsonSerializer.SerializeToNode(source, Json)!.AsObject();
        if (patch.TryGetProperty("theme", out var theme))
        {
            node["theme"] = ReadTheme(theme);
        }

        foreach (var prop in patch.EnumerateObject())
        {
            if (prop.NameEquals("theme"))
            {
                continue;
            }

            node[JsonNamingPolicy.CamelCase.ConvertName(prop.Name)] = JsonNode.Parse(prop.Value.GetRawText());
        }

        var updated = node.Deserialize<AppSettings>(Json) ?? throw new InvalidOperationException("无法应用设置。");
        updated.Lan = source.Lan;
        updated.UiLanguage = UiLanguages.Normalize(updated.UiLanguage);
        if (patch.TryGetProperty("hotkeys", out var hotkeys) && !hotkeys.TryGetProperty("screenshot", out _))
        {
            updated.Hotkeys.Screenshot = source.Hotkeys.Screenshot;
        }

        if (string.IsNullOrWhiteSpace(updated.SaveFolder))
        {
            throw new InvalidOperationException("保存目录不能为空。");
        }

        return updated;
    }

    public static AppSettings Reset(AppSettings source)
    {
        var reset = new AppSettings { Lan = source.Lan, SaveFolder = source.SaveFolder };
        return reset;
    }

    public static string EnqueueJob(string kind, string path, string body, Func<string, string, string, Task<string>>? work)
    {
        if (work is null)
        {
            throw new InvalidOperationException("后期任务还不可用。");
        }

        var id = Guid.NewGuid().ToString("N")[..12];
        Jobs[id] = new LanJobView(id, kind, "running", null, null);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await work(kind, path, body).ConfigureAwait(false);
                Jobs[id] = new LanJobView(id, kind, "done", result, null);
            }
            catch (Exception ex)
            {
                Jobs[id] = new LanJobView(id, kind, "error", null, ex.Message);
            }
        });
        return id;
    }

    public static LanJobView? Job(string id) => Jobs.TryGetValue(id, out var job) ? job : null;

    public static object Targets(object listed)
    {
        var node = JsonSerializer.SerializeToNode(listed, Json);
        if (node is not JsonArray array)
        {
            return Array.Empty<object>();
        }

        return array.Select(item =>
        {
            var obj = item as JsonObject;
            var id = obj?["id"]?.ToString() ?? "";
            var title = obj?["title"]?.ToString() ?? obj?["label"]?.ToString() ?? id;
            string? preview = null;
            if (obj?["preview"] is JsonValue previewValue && previewValue.TryGetValue<string>(out var previewText))
            {
                preview = previewText;
            }

            return new { id, title, detail = obj?["detail"]?.ToString() ?? "", preview };
        }).ToArray();
    }

    private static int ReadTheme(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number is 1 or 2)
        {
            return number;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text?.Equals("Light", StringComparison.OrdinalIgnoreCase) == true ? 1 : 2;
    }
}
