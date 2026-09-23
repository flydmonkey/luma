using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Luma.Core.Localization;

namespace Luma.Core.Lan;

public static class OpenApiDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Render(string? locale)
    {
        var lang = UiLanguages.ResolveEffective(string.IsNullOrWhiteSpace(locale) ? UiLanguages.System : locale);
        if (lang == UiLanguages.System)
        {
            lang = UiLanguages.En;
        }

        var skeleton = ReadNode("Luma.OpenApi.skeleton.json")
            ?? throw new InvalidOperationException("OpenAPI skeleton is missing.");
        var overlay = ReadNode($"Luma.OpenApi.{lang}.json") ?? ReadNode("Luma.OpenApi.en.json");
        if (overlay is not null)
        {
            Merge(skeleton, overlay);
        }

        return skeleton.ToJsonString(JsonOptions);
    }

    public static string DocsHtml(bool dark, string? locale = null)
    {
        var lang = UiLanguages.ResolveEffective(string.IsNullOrWhiteSpace(locale) ? UiLanguages.System : locale);
        if (lang == UiLanguages.System)
        {
            lang = UiLanguages.En;
        }

        using var spec = JsonDocument.Parse(Render(lang));
        var title = spec.RootElement.GetProperty("info").GetProperty("title").GetString() ?? "Luma";
        var theme = dark ? "dark" : "light";
        var bg = dark ? "#1c1c1c" : "#fafafa";
        return $$"""
            <!DOCTYPE html>
            <html lang="{{lang}}">
            <head>
            <meta charset="utf-8"/>
            <title>{{title}}</title>
            <script src="/rapidoc-min.js"></script>
            <style>html,body{margin:0;height:100%;background:{{bg}};}</style>
            </head>
            <body>
            <rapi-doc spec-url="/openapi.json" theme="{{theme}}" show-header="false" allow-try="true" render-style="view" regular-font="Segoe UI, sans-serif"></rapi-doc>
            </body>
            </html>
            """;
    }

    private static void Merge(JsonNode target, JsonNode overlay)
    {
        if (target is not JsonObject targetObj || overlay is not JsonObject overlayObj)
        {
            return;
        }

        foreach (var property in overlayObj)
        {
            if (property.Value is null)
            {
                continue;
            }

            if (targetObj[property.Key] is JsonObject existing && property.Value is JsonObject child)
            {
                Merge(existing, child);
                continue;
            }

            targetObj[property.Key] = property.Value.DeepClone();
        }
    }

    private static JsonNode? ReadNode(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd());
    }
}
