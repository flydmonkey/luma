using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Luma.Core.Capture;
using Luma.Core.Legal;
using Luma.Core.Localization;
using Luma.Core.Settings;
using Luma.Core.Skill;

namespace Luma.Core.Lan;

public sealed class LanServer : IDisposable
{
    private readonly SettingsStore _store;
    private readonly Func<AppSettings> _settings;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    public string? LastError { get; private set; }
    public bool IsRunning { get; private set; }
    public int BoundPort { get; private set; }
    public IReadOnlyList<string> BoundUrls { get; private set; } = [];

    public event Func<JsonElement, Task<JsonElement>>? SessionCommand;
    public event Action? SettingsChanged;
    public Func<string, object>? ListTargets { get; set; }
    public Func<WebStillPayload>? CaptureWebStill { get; set; }
    public Func<int, WebStillPayload?>? CaptureWindowStill { get; set; }
    public Func<byte[], Task<string?>>? RecognizeWebStill { get; set; }
    public Func<string, string, string, Task<string>>? RunJob { get; set; }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _sessions = new(StringComparer.Ordinal);

    public LanServer(SettingsStore store, Func<AppSettings> settings)
    {
        _store = store;
        _settings = settings;
    }

    public void Start()
    {
        var settings = _settings();
        if (!settings.Lan.Enabled)
        {
            Stop();
            return;
        }

        if (!LanPort.IsValid(settings.Lan.Port))
        {
            LastError = "端口必须是 1 到 65535 之间的整数。";
            IsRunning = false;
            return;
        }

        Stop();
        RunNetsh($"http delete urlacl url=http://+:{settings.Lan.Port}/");
        var urls = ListenUrls(settings.Lan.Port).ToArray();
        foreach (var url in urls)
        {
            RunNetsh($"http add urlacl url={url} user=Everyone");
        }
        if (!TryListen(urls, out var listener))
        {
            var loopback = new[] { $"http://127.0.0.1:{settings.Lan.Port}/", $"http://localhost:{settings.Lan.Port}/" };
            if (!TryListen(loopback, out listener))
            {
                LastError = $"端口 {settings.Lan.Port} 已被占用或无法绑定。";
                IsRunning = false;
                BoundUrls = [];
                return;
            }

            urls = loopback;
        }

        AllowFirewall(settings.Lan.Port);
        _listener = listener;
        BoundPort = settings.Lan.Port;
        BoundUrls = urls.Select(url => url.TrimEnd('/')).ToArray();
        LastError = null;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
        _listener?.Close();
        _listener = null;
        IsRunning = false;
        BoundPort = 0;
        BoundUrls = [];
    }

    private static IEnumerable<string> ListenUrls(int port)
    {
        yield return $"http://127.0.0.1:{port}/";
        yield return $"http://localhost:{port}/";
        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(Dns.GetHostName());
        }
        catch
        {
            yield break;
        }

        foreach (var address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            {
                yield return $"http://{address}:{port}/";
            }
        }
    }

    private static bool TryListen(IEnumerable<string> urls, out HttpListener? listener)
    {
        listener = new HttpListener();
        foreach (var url in urls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            listener.Prefixes.Add(url);
        }

        try
        {
            listener.Start();
            return true;
        }
        catch (HttpListenerException)
        {
            listener.Close();
            listener = null;
            return false;
        }
    }

    private static void AllowFirewall(int port) =>
        RunNetsh($"advfirewall firewall add rule name=\"Luma LAN {port}\" dir=in action=allow protocol=TCP localport={port}");

    private static void RunNetsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(3000);
        }
        catch
        {
            // loopback still works when reservation is unavailable
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context), token);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "/";
        if (path.Length == 0)
        {
            path = "/";
        }

        if (context.Request.HttpMethod == "GET" && await TryWriteDocumentAsync(context, path).ConfigureAwait(false))
        {
            return;
        }

        if (!Authorize(context))
        {
            if (path == "/unlock" && context.Request.HttpMethod == "POST")
            {
                await UnlockAsync(context).ConfigureAwait(false);
                return;
            }

            if (path is "/favicon.ico" or "/favicon.png")
            {
                await WriteFaviconAsync(context, path).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path is "/" or "/index.html")
            {
                await WriteHtml(context.Response, LanWebAssets.UnlockHtml(wrongKey: false)).ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, 401, LanControlApi.Fail("需要访问密钥。")).ConfigureAwait(false);
            return;
        }

        try
        {
            if (path is "/" or "/index.html" or "/app.js" or "/app.css")
            {
                var file = path is "/" or "/index.html" ? "index.html" : path.TrimStart('/');
                if (!LanWebAssets.TryGet(file, out var type, out var body))
                {
                    await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个页面。")).ConfigureAwait(false);
                    return;
                }

                context.Response.Headers["Cache-Control"] = "no-cache";
                await WriteBytesAsync(context.Response, 200, body, type).ConfigureAwait(false);
                return;
            }

            if (path is "/favicon.ico" or "/favicon.png")
            {
                await WriteFaviconAsync(context, path).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/v1", StringComparison.Ordinal))
            {
                await DispatchApiAsync(context, path).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/media/", StringComparison.Ordinal) || path.StartsWith("/poster/", StringComparison.Ordinal))
            {
                await WriteLibraryFileAsync(context, path).ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个接口。")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteAsync(context.Response, 500, LanControlApi.Fail(ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task DispatchApiAsync(HttpListenerContext context, string path)
    {
        var settings = _settings();
        var relative = path.Length > "/api/v1".Length ? path["/api/v1".Length..] : "/";
        if (relative.Length == 0)
        {
            relative = "/";
        }

        var query = context.Request.QueryString;
        var method = context.Request.HttpMethod;
        var bodyText = "";
        if (method is "POST" or "PUT" or "PATCH")
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            bodyText = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        try
        {
            if (method == "GET" && relative is "/" or "")
            {
                await WriteAsync(context.Response, 200, LanControlApi.Ok(new { name = "luma", control = true })).ConfigureAwait(false);
                return;
            }

            if ((relative.StartsWith("/session", StringComparison.Ordinal) || relative == "/target") && SessionCommand is not null)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
                var payload = JsonSerializer.SerializeToElement(new { path, method, body = json });
                var result = await SessionCommand(payload).ConfigureAwait(false);
                var status = result.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False ? 400 : 200;
                await WriteAsync(context.Response, status, result.GetRawText()).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && relative == "/screenshot")
            {
                var windowIndexText = query["windowIndex"];
                if (!string.IsNullOrEmpty(windowIndexText))
                {
                    if (CaptureWindowStill is null || !int.TryParse(windowIndexText, out var windowIndex))
                    {
                        await WriteAsync(context.Response, 400, LanControlApi.Fail("没有可截取的窗口。")).ConfigureAwait(false);
                        return;
                    }

                    var windowShot = CaptureWindowStill(windowIndex);
                    if (windowShot is null)
                    {
                        await WriteAsync(context.Response, 400, LanControlApi.Fail("没有可截取的窗口。")).ConfigureAwait(false);
                        return;
                    }

                    await WriteAsync(context.Response, 200, LanControlApi.Ok(windowShot)).ConfigureAwait(false);
                    return;
                }

                if (CaptureWebStill is null)
                {
                    await WriteAsync(context.Response, 400, LanControlApi.Fail("没有可截取的显示器。")).ConfigureAwait(false);
                    return;
                }

                var shot = CaptureWebStill();
                await WriteAsync(context.Response, 200, LanControlApi.Ok(shot)).ConfigureAwait(false);
                return;
            }

            if (method == "POST" && relative == "/screenshot/ocr")
            {
                var png = ScreenshotPng(bodyText);
                if (png.Length == 0)
                {
                    await WriteAsync(context.Response, 400, LanControlApi.Fail("空图片。")).ConfigureAwait(false);
                    return;
                }

                string? text = null;
                if (RecognizeWebStill is not null)
                {
                    text = await RecognizeWebStill(png).ConfigureAwait(false);
                }

                await WriteAsync(context.Response, 200, LanControlApi.Ok(new { text })).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && (relative.StartsWith("/targets", StringComparison.Ordinal)))
            {
                var kind = relative.StartsWith("/targets/", StringComparison.Ordinal)
                    ? relative["/targets/".Length..]
                    : query["kind"] ?? "displays";
                var listed = ListTargets?.Invoke(kind) ?? Array.Empty<object>();
                await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.Targets(listed))).ConfigureAwait(false);
                return;
            }

            if (relative == "/settings" && method == "GET")
            {
                await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.SettingsForClient(settings))).ConfigureAwait(false);
                return;
            }

            if (relative == "/settings" && method is "PATCH" or "POST")
            {
                var patch = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
                var updated = LanControlApi.ApplySettings(settings, patch);
                _store.Save(updated);
                CopySettings(updated, settings);
                SettingsChanged?.Invoke();
                await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.SettingsForClient(settings))).ConfigureAwait(false);
                return;
            }

            if (relative == "/settings/reset" && method == "POST")
            {
                var reset = LanControlApi.Reset(settings);
                _store.Save(reset);
                CopySettings(reset, settings);
                SettingsChanged?.Invoke();
                await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.SettingsForClient(settings))).ConfigureAwait(false);
                return;
            }

            if (relative == "/library" && method == "GET")
            {
                await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.LibraryList(settings.SaveFolder))).ConfigureAwait(false);
                return;
            }

            if (relative.StartsWith("/library/", StringComparison.Ordinal))
            {
                await DispatchLibraryAsync(context, settings.SaveFolder, relative["/library/".Length..], method, query["confirm"], bodyText).ConfigureAwait(false);
                return;
            }

            if (method == "GET" && relative.StartsWith("/jobs/", StringComparison.Ordinal))
            {
                var job = LanControlApi.Job(Uri.UnescapeDataString(relative["/jobs/".Length..]));
                await WriteAsync(context.Response, job is null ? 404 : 200, job is null ? LanControlApi.Fail("未找到这个作业。") : LanControlApi.Ok(job)).ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个接口。")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var status = ex.Message.Contains("confirm", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("不能", StringComparison.Ordinal) ? 400 : 400;
            await WriteAsync(context.Response, status, LanControlApi.Fail(ex.Message)).ConfigureAwait(false);
        }
    }

    private async Task DispatchLibraryAsync(HttpListenerContext context, string folder, string rest, string method, string? confirm, string bodyText)
    {
        var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个文件。")).ConfigureAwait(false);
            return;
        }

        var id = Uri.UnescapeDataString(parts[0]);
        if (parts.Length == 1 && method == "PATCH")
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
            var name = doc.RootElement.TryGetProperty("name", out var value) ? value.GetString() : null;
            await WriteAsync(context.Response, 200, LanControlApi.Ok(LanControlApi.Rename(folder, id, name ?? ""))).ConfigureAwait(false);
            return;
        }

        if (parts.Length == 1 && method == "DELETE")
        {
            LanControlApi.Delete(folder, id, string.Equals(confirm, "true", StringComparison.OrdinalIgnoreCase));
            await WriteAsync(context.Response, 200, LanControlApi.Ok(new { deleted = id })).ConfigureAwait(false);
            return;
        }

        if (parts.Length == 2 && method == "POST")
        {
            var item = LanControlApi.Find(folder, id) ?? throw new InvalidOperationException("未找到这个文件。");
            var jobId = LanControlApi.EnqueueJob(parts[1], item.Path, bodyText, RunJob);
            await WriteAsync(context.Response, 200, LanControlApi.Ok(new { jobId })).ConfigureAwait(false);
        }
    }

    private async Task WriteLibraryFileAsync(HttpListenerContext context, string path)
    {
        var poster = path.StartsWith("/poster/", StringComparison.Ordinal);
        var id = Uri.UnescapeDataString(path[(poster ? "/poster/" : "/media/").Length..]);
        var item = LanControlApi.Find(_settings().SaveFolder, id);
        if (item is null)
        {
            await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个文件。")).ConfigureAwait(false);
            return;
        }

        var file = poster ? item.PosterPath : item.Path;
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个文件。")).ConfigureAwait(false);
            return;
        }

        var type = poster ? "image/jpeg" : ContentTypeFor(file);
        await WriteBytesAsync(context.Response, 200, await File.ReadAllBytesAsync(file).ConfigureAwait(false), type).ConfigureAwait(false);
    }

    private static byte[] ScreenshotPng(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(bodyText);
        if (!doc.RootElement.TryGetProperty("png", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return [];
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? [] : Convert.FromBase64String(text);
    }

    private static string ContentTypeFor(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.ToLowerInvariant() switch
        {
            ".m4a" or ".aac" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "video/mp4"
        };
    }

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        var json = JsonSerializer.Serialize(source, LanControlApi.Json);
        var copy = JsonSerializer.Deserialize<AppSettings>(json, LanControlApi.Json) ?? source;
        target.SaveFolder = copy.SaveFolder;
        target.LastMode = copy.LastMode;
        target.MonitorIndex = copy.MonitorIndex;
        target.Quality = copy.Quality;
        target.RecordingFormat = copy.RecordingFormat;
        target.Audio = copy.Audio;
        target.Overlay = copy.Overlay;
        target.Hotkeys = copy.Hotkeys;
        target.Automation = copy.Automation;
        target.CloseToTray = copy.CloseToTray;
        target.LaunchToTray = copy.LaunchToTray;
        target.HideTrayIcon = copy.HideTrayIcon;
        target.ShowRecordingBar = copy.ShowRecordingBar;
        target.SilentMode = copy.SilentMode;
        target.Theme = copy.Theme;
        target.UiLanguage = copy.UiLanguage;
        target.Lan = copy.Lan;
    }

    private async Task<bool> TryWriteDocumentAsync(HttpListenerContext context, string path)
    {
        var settings = _settings();
        var dark = settings.Theme != AppThemeMode.Light;
        var locale = UiLanguages.ResolveEffective(settings.UiLanguage);
        if (path is "/legal/privacy" or "/legal/terms")
        {
            var document = path.EndsWith("terms", StringComparison.Ordinal) ? LegalDocuments.Terms : LegalDocuments.Privacy;
            var html = LegalDocuments.ApplyTheme(LegalDocuments.ReadHtml(document, locale), dark);
            await WriteHtml(context.Response, html).ConfigureAwait(false);
            return true;
        }

        if (path is "/openapi.json" or "/skill/openapi.json")
        {
            var lang = context.Request.QueryString["lang"];
            await WriteAsync(context.Response, 200, OpenApiDocument.Render(string.IsNullOrWhiteSpace(lang) ? locale : lang)).ConfigureAwait(false);
            return true;
        }

        if (path == "/api/docs")
        {
            await WriteHtml(context.Response, OpenApiDocument.DocsHtml(dark, locale)).ConfigureAwait(false);
            return true;
        }

        if (path == "/rapidoc-min.js")
        {
            if (!LanWebAssets.TryGet("rapidoc-min.js", out var type, out var body))
            {
                await WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个页面。")).ConfigureAwait(false);
                return true;
            }

            await WriteBytesAsync(context.Response, 200, body, type).ConfigureAwait(false);
            return true;
        }

        if (path == "/skill/reference.md")
        {
            await WriteAsync(context.Response, 200, ControlSkillDocument.ReadReference(), "text/markdown; charset=utf-8").ConfigureAwait(false);
            return true;
        }

        if (path == "/skill")
        {
            await WriteHtml(context.Response, ControlSkillDocument.ToHtml(dark)).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private async Task UnlockAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        var key = "";
        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "access_key")
            {
                key = Uri.UnescapeDataString(pair[1].Replace("+", " ", StringComparison.Ordinal));
            }
        }

        if (!string.Equals(key, _settings().Lan.AccessKey, StringComparison.Ordinal))
        {
            await WriteHtml(context.Response, LanWebAssets.UnlockHtml(wrongKey: true)).ConfigureAwait(false);
            return;
        }

        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        _sessions[token] = 1;
        context.Response.StatusCode = 302;
        context.Response.RedirectLocation = "/";
        context.Response.Headers["Set-Cookie"] = $"luma_lan={token}; HttpOnly; Path=/; SameSite=Lax";
        context.Response.Close();
    }

    private Task WriteFaviconAsync(HttpListenerContext context, string path)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Assets", path.TrimStart('/'));
        if (!File.Exists(file))
        {
            return WriteAsync(context.Response, 404, LanControlApi.Fail("未找到这个文件。"));
        }

        var type = path.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/x-icon";
        return WriteBytesAsync(context.Response, 200, File.ReadAllBytes(file), type);
    }

    private bool Authorize(HttpListenerContext context)
    {
        var accessKey = _settings().Lan.AccessKey;
        if (string.IsNullOrEmpty(accessKey))
        {
            return true;
        }

        var request = context.Request;
        var header = request.Headers["X-Record-Key"] ?? request.Headers["Authorization"];
        if (header is not null)
        {
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                header = header["Bearer ".Length..];
            }

            if (string.Equals(header, accessKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var cookie = request.Headers["Cookie"] ?? "";
        foreach (var part in cookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "luma_lan" && _sessions.ContainsKey(pair[1]))
            {
                return true;
            }
        }

        return false;
    }

    private static Task WriteHtml(HttpListenerResponse response, string html) =>
        WriteAsync(response, 200, html, "text/html; charset=utf-8");

    private static Task WriteJson(HttpListenerResponse response, object value) =>
        WriteAsync(response, 200, JsonSerializer.Serialize(value), "application/json");

    private static async Task WriteBytesAsync(HttpListenerResponse response, int status, byte[] bytes, string contentType)
    {
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static async Task WriteAsync(HttpListenerResponse response, int status, string body, string contentType = "application/json")
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    public void Dispose() => Stop();
}
