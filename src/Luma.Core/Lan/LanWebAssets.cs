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

    public static string UnlockHtml(bool wrongKey) => """
<!doctype html>
<html lang="zh-Hans">
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
    <button type="submit" style="padding:8px 16px">打开</button>
  </form>
</body>
</html>
""".Replace("__MESSAGE__", wrongKey ? "访问密钥不正确。" : "这台电脑设置了访问密钥。", StringComparison.Ordinal);
}
