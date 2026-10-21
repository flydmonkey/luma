using System.Reflection;

namespace Luma.Core.Lan;

public static class LanWebAssets
{
    public static bool TryGet(string name, out string contentType, out byte[] body)
    {
        contentType = name switch
        {
            "index.html" => "text/html; charset=utf-8",
            "app.css" => "text/css; charset=utf-8",
            "app.js" or "rapidoc-min.js" => "application/javascript; charset=utf-8",
            _ => ""
        };
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Luma.Core.Lan.Web." + name);
        if (stream is null || contentType.Length == 0)
        {
            body = [];
            return false;
        }

        using (stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            body = memory.ToArray();
            return true;
        }
    }

    private static readonly Dictionary<string, (string Prompt, string Wrong, string Open)> UnlockCopy = new()
    {
        [Localization.UiLanguages.English] = ("This computer requires an access key.", "The access key is incorrect.", "Open"),
        [Localization.UiLanguages.SimplifiedChinese] = ("这台电脑设置了访问密钥。", "访问密钥不正确。", "打开"),
        [Localization.UiLanguages.TraditionalChinese] = ("這台電腦設定了存取金鑰。", "存取金鑰不正確。", "開啟"),
        [Localization.UiLanguages.Japanese] = ("このコンピューターにはアクセスキーが設定されています。", "アクセスキーが正しくありません。", "開く"),
        [Localization.UiLanguages.Korean] = ("이 컴퓨터에는 액세스 키가 설정되어 있습니다.", "액세스 키가 올바르지 않습니다.", "열기")
    };

    public static string UnlockHtml(bool wrongKey, string locale)
    {
        var lang = UnlockCopy.ContainsKey(locale) ? locale : Localization.UiLanguages.English;
        var copy = UnlockCopy[lang];
        return UnlockTemplate
            .Replace("__LANG__", lang, StringComparison.Ordinal)
            .Replace("__MESSAGE__", wrongKey ? copy.Wrong : copy.Prompt, StringComparison.Ordinal)
            .Replace("__OPEN__", copy.Open, StringComparison.Ordinal);
    }

    private const string UnlockTemplate = """
<!doctype html>
<html lang="__LANG__">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Luma</title>
</head>
<body style="font-family:Segoe UI,sans-serif;background:#202020;color:#fff;display:grid;place-items:center;min-height:100vh;margin:0">
  <form method="post" action="/unlock" style="background:#2c2c2c;padding:24px;border-radius:8px;width:min(360px,92vw)">
    <h1 style="font-size:18px;margin:0 0 12px">Luma</h1>
    <p style="opacity:.8">__MESSAGE__</p>
    <input name="access_key" type="password" autofocus style="width:100%;padding:8px;margin:8px 0"/>
    <button type="submit" style="padding:8px 16px">__OPEN__</button>
  </form>
</body>
</html>
""";
}
