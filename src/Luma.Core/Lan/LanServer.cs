using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Luma.Core.Library;
using Luma.Core.Settings;

namespace Luma.Core.Lan;

public sealed class LanServer : IDisposable
{
    private readonly SettingsStore _store;
    private readonly Func<AppSettings> _settings;
    private readonly LibraryCatalog _library = new();
    private readonly object _gate = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    public string? LastError { get; private set; }
    public bool IsRunning { get; private set; }
    public int BoundPort { get; private set; }
    public IReadOnlyList<string> BoundUrls { get; private set; } = [];

    public event Func<JsonElement, Task<JsonElement>>? SessionCommand;
    public Func<string, object>? ListTargets { get; set; }

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
        var settings = _settings();
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "/";
        if (path.Length == 0)
        {
            path = "/";
        }

        if (!Authorize(context.Request, settings.Lan.AccessKey) && path is not "/")
        {
            await WriteAsync(context.Response, 401, """{"error":"unauthorized"}""").ConfigureAwait(false);
            return;
        }

        try
        {
            if (path is "/" or "/index.html")
            {
                await WriteHtml(context.Response, LanWebAssets.IndexHtml).ConfigureAwait(false);
                return;
            }

            if (path == "/app.js")
            {
                await WriteAsync(context.Response, 200, LanWebAssets.AppJs, "application/javascript").ConfigureAwait(false);
                return;
            }

            if (path == "/app.css")
            {
                await WriteAsync(context.Response, 200, LanWebAssets.AppCss, "text/css").ConfigureAwait(false);
                return;
            }

            if (path == "/api/v1/settings" && context.Request.HttpMethod == "GET")
            {
                await WriteJson(context.Response, SettingsDto.From(settings, includeSecret: false)).ConfigureAwait(false);
                return;
            }

            if (path == "/api/v1/settings" && context.Request.HttpMethod is "PATCH" or "POST")
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                var patch = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                if (patch.TryGetProperty("lanPort", out _) || patch.TryGetProperty("port", out _))
                {
                    await WriteAsync(context.Response, 400, """{"error":"listen port cannot be changed from the web API"}""").ConfigureAwait(false);
                    return;
                }

                SettingsDto.Apply(settings, patch);
                _store.Save(settings);
                await WriteJson(context.Response, SettingsDto.From(settings, includeSecret: false)).ConfigureAwait(false);
                return;
            }

            if (path == "/api/v1/library" && context.Request.HttpMethod == "GET")
            {
                await WriteJson(context.Response, _library.List(settings.SaveFolder)).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/v1/library/", StringComparison.Ordinal) && context.Request.HttpMethod == "DELETE")
            {
                var confirm = context.Request.QueryString["confirm"];
                if (!string.Equals(confirm, "true", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteAsync(context.Response, 400, """{"error":"confirmation is required"}""").ConfigureAwait(false);
                    return;
                }

                var id = path["/api/v1/library/".Length..];
                var item = _library.List(settings.SaveFolder).FirstOrDefault(x => x.Id == id);
                if (item is null)
                {
                    await WriteAsync(context.Response, 404, """{"error":"not found"}""").ConfigureAwait(false);
                    return;
                }

                _library.Delete(item.Path);
                await WriteAsync(context.Response, 200, """{"ok":true}""").ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/v1/targets", StringComparison.Ordinal) && context.Request.HttpMethod == "GET")
            {
                var kind = context.Request.QueryString["kind"] ?? "displays";
                var targets = ListTargets?.Invoke(kind) ?? Array.Empty<object>();
                await WriteJson(context.Response, targets).ConfigureAwait(false);
                return;
            }

            if (path.StartsWith("/api/v1/session", StringComparison.Ordinal) && SessionCommand is not null)
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                var json = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var payload = JsonSerializer.SerializeToElement(new
                {
                    path,
                    method = context.Request.HttpMethod,
                    body = json
                });
                var result = await SessionCommand(payload).ConfigureAwait(false);
                await WriteJson(context.Response, result).ConfigureAwait(false);
                return;
            }

            await WriteAsync(context.Response, 404, """{"error":"not found"}""").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteAsync(context.Response, 500, JsonSerializer.Serialize(new { error = ex.Message })).ConfigureAwait(false);
        }
    }

    private static bool Authorize(HttpListenerRequest request, string accessKey)
    {
        if (string.IsNullOrEmpty(accessKey))
        {
            return true;
        }

        var header = request.Headers["X-Record-Key"] ?? request.Headers["Authorization"];
        if (header is null)
        {
            return false;
        }

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            header = header["Bearer ".Length..];
        }

        return string.Equals(header, accessKey, StringComparison.Ordinal);
    }

    private static Task WriteHtml(HttpListenerResponse response, string html) =>
        WriteAsync(response, 200, html, "text/html; charset=utf-8");

    private static Task WriteJson(HttpListenerResponse response, object value) =>
        WriteAsync(response, 200, JsonSerializer.Serialize(value), "application/json");

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

internal static class SettingsDto
{
    public static object From(AppSettings settings, bool includeSecret) => new
    {
        theme = settings.Theme.ToString(),
        uiLanguage = settings.UiLanguage,
        saveFolder = settings.SaveFolder,
        hardwareEncoding = settings.Quality.HardwareEncoding,
        lanEnabled = settings.Lan.Enabled,
        lanPort = settings.Lan.Port,
        accessKey = includeSecret ? settings.Lan.AccessKey : ""
    };

    public static void Apply(AppSettings settings, JsonElement patch)
    {
        if (patch.TryGetProperty("saveFolder", out var folder) && folder.ValueKind == JsonValueKind.String)
        {
            settings.SaveFolder = folder.GetString() ?? settings.SaveFolder;
        }

        if (patch.TryGetProperty("theme", out var theme) && theme.ValueKind == JsonValueKind.String)
        {
            if (Enum.TryParse<AppThemeMode>(theme.GetString(), true, out var mode))
            {
                settings.Theme = mode;
            }
        }

        if (patch.TryGetProperty("uiLanguage", out var language) && language.ValueKind == JsonValueKind.String)
        {
            settings.UiLanguage = language.GetString() ?? settings.UiLanguage;
        }
    }
}
