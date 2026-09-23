using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Luma.Core.Capture;
using Luma.Core.Lan;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class WebScreenshotTests
{
    [Fact]
    public void Still_uses_host_pixels_and_known_monitor_origin()
    {
        var monitors = new (int X, int Y, int Width, int Height)[] { (-100, -20, 80, 40), (-20, -20, 50, 60) };
        var windows = new (string Id, string Title, int X, int Y, int Width, int Height)[]
        {
            ("note:class:notepad.exe", "Notepad", -90, -10, 20, 16),
            ("off:class:gone.exe", "Gone", -400, -400, 10, 10)
        };
        var payload = WebStill.Describe(Png(130, 60), monitors, windows);

        Assert.Equal(-100, payload.OriginX);
        Assert.Equal(-20, payload.OriginY);
        Assert.Equal(130, payload.Width);
        Assert.Equal(60, payload.Height);
        using var stream = new MemoryStream(Convert.FromBase64String(payload.Png));
        using var bitmap = new Bitmap(stream);
        Assert.Equal(130, bitmap.Width);
        Assert.Equal(60, bitmap.Height);
        Assert.Equal(0, payload.Monitors[0].X);
        Assert.Equal(0, payload.Monitors[0].Y);
        Assert.Equal(80, payload.Monitors[1].X);
        var window = Assert.Single(payload.Windows);
        Assert.Equal(10, window.X);
        Assert.Equal(10, window.Y);
        Assert.Equal(20, window.Width);
        Assert.Equal(16, window.Height);
    }

    [Fact]
    public void Scaled_preview_is_rejected()
    {
        var monitors = new (int X, int Y, int Width, int Height)[] { (0, 0, 64, 48) };
        Assert.Throws<InvalidOperationException>(() => WebStill.Describe(Png(32, 24), monitors, []));
    }

    [Fact]
    public void Capture_matches_the_monitor_rectangle()
    {
        var payload = WebStill.Capture([(0, 0, 32, 24)], []);
        Assert.Equal(0, payload.OriginX);
        Assert.Equal(0, payload.OriginY);
        Assert.Equal(32, payload.Width);
        Assert.Equal(24, payload.Height);
        using var stream = new MemoryStream(Convert.FromBase64String(payload.Png));
        using var bitmap = new Bitmap(stream);
        Assert.Equal(32, bitmap.Width);
        Assert.Equal(24, bitmap.Height);
    }

    [Fact]
    public async Task Still_route_does_not_keep_or_save_a_png()
    {
        await using var host = await Host.Start();
        var marker = Convert.ToBase64String(Png(16, 16))[..24];
        host.Server.CaptureWebStill = () => WebStill.Describe(
            Png(16, 16),
            [(0, 0, 16, 16)],
            []);

        using var client = new HttpClient();
        var shot = await client.GetStringAsync(host.Url + "/api/v1/screenshot");
        Assert.Contains(marker, shot);
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));

        var save = await client.PostAsync(host.Url + "/api/v1/screenshot/save", new StringContent("{}", Encoding.UTF8, "application/json"));
        var copy = await client.GetAsync(host.Url + "/api/v1/screenshot/copy");
        var cancel = await client.DeleteAsync(host.Url + "/api/v1/screenshot");
        Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, copy.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));
        Assert.DoesNotContain("screenshot/save", OpenApiDocument.Render("en"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Window_index_returns_that_window_still()
    {
        await using var host = await Host.Start();
        host.Server.CaptureWebStill = () => WebStill.Describe(Png(16, 16), [(0, 0, 16, 16)], []);
        host.Server.CaptureWindowStill = index =>
            index == 2 ? WebStill.Describe(Png(8, 8), [(0, 0, 8, 8)], [("note:class:notepad.exe", "Notepad", 0, 0, 8, 8)]) : null;

        using var client = new HttpClient();
        var body = await client.GetStringAsync(host.Url + "/api/v1/screenshot?windowIndex=2");
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(8, doc.RootElement.GetProperty("data").GetProperty("width").GetInt32());
        Assert.Equal("Notepad", doc.RootElement.GetProperty("data").GetProperty("windows")[0].GetProperty("title").GetString());
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));

        var missing = await client.GetAsync(host.Url + "/api/v1/screenshot?windowIndex=9");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Ocr_returns_text_and_writes_no_png()
    {
        await using var host = await Host.Start();
        host.Server.RecognizeWebStill = png =>
        {
            Assert.NotEmpty(png);
            return Task.FromResult<string?>("hello");
        };
        var png = Convert.ToBase64String(Png(16, 16));
        using var client = new HttpClient();
        var response = await client.PostAsync(
            host.Url + "/api/v1/screenshot/ocr",
            new StringContent("{\"png\":\"" + png + "\"}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("hello", doc.RootElement.GetProperty("data").GetProperty("text").GetString());
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));
    }

    [Fact]
    public async Task Locked_client_gets_no_pixels_and_empty_key_gets_the_still()
    {
        await using var host = await Host.Start();
        var png = Png(16, 16);
        var marker = Convert.ToBase64String(png)[..24];
        host.Server.CaptureWebStill = () => WebStill.Describe(png, [(0, 0, 16, 16)], []);
        host.Settings.Lan.AccessKey = "secret";

        using var client = new HttpClient();
        var denied = await client.GetAsync(host.Url + "/api/v1/screenshot");
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.DoesNotContain(marker, deniedBody);
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));

        host.Settings.Lan.AccessKey = "";
        var allowed = await client.GetStringAsync(host.Url + "/api/v1/screenshot");
        using var doc = JsonDocument.Parse(allowed);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains(marker, doc.RootElement.GetProperty("data").GetProperty("png").GetString());
        Assert.Empty(Directory.EnumerateFiles(host.Folder, "*.png"));
    }

    [Fact]
    public async Task Still_request_leaves_the_recording_session_unchanged()
    {
        await using var host = await Host.Start();
        var output = Path.Combine(host.Folder, "clip.mp4");
        var calls = 0;
        host.Server.SessionCommand += payload =>
        {
            calls++;
            var path = payload.GetProperty("path").GetString();
            Assert.Equal("/api/v1/session", path);
            return Task.FromResult(JsonSerializer.SerializeToElement(new
            {
                ok = true,
                data = new { phase = 2, output }
            }));
        };
        host.Server.CaptureWebStill = () => WebStill.Describe(Png(16, 16), [(0, 0, 16, 16)], []);

        using var client = new HttpClient();
        var before = await client.GetStringAsync(host.Url + "/api/v1/session");
        var callsAfterStatus = calls;
        await client.GetStringAsync(host.Url + "/api/v1/screenshot");
        Assert.Equal(callsAfterStatus, calls);
        var after = await client.GetStringAsync(host.Url + "/api/v1/session");
        Assert.Equal(before, after);
        using var doc = JsonDocument.Parse(after);
        Assert.Equal(2, doc.RootElement.GetProperty("data").GetProperty("phase").GetInt32());
        Assert.Equal(output, doc.RootElement.GetProperty("data").GetProperty("output").GetString());
    }

    [Fact]
    public void Openapi_documents_only_the_still_route()
    {
        var spec = OpenApiDocument.Render("zh-Hans");
        using var doc = JsonDocument.Parse(spec);
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/screenshot", out var route));
        Assert.True(route.TryGetProperty("get", out _));
        Assert.False(route.TryGetProperty("post", out _));
        Assert.False(paths.TryGetProperty("/api/v1/screenshot/save", out _));
        Assert.False(paths.TryGetProperty("/api/v1/screenshot/copy", out _));
        var still = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ScreenshotStill").GetProperty("properties");
        foreach (var name in new[] { "originX", "originY", "width", "height", "png", "monitors", "windows" })
        {
            Assert.True(still.TryGetProperty(name, out _), name);
        }

        var unauthorized = route.GetProperty("get").GetProperty("responses").GetProperty("401");
        Assert.Contains("需要访问密钥。", unauthorized.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Web_shell_has_a_screenshot_action_and_five_languages()
    {
        Assert.True(LanWebAssets.TryGet("index.html", out _, out var htmlBytes));
        var html = Encoding.UTF8.GetString(htmlBytes);
        Assert.Contains("id=\"shotBtn\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shotBtn\" data-mode", html, StringComparison.Ordinal);
        Assert.Contains("id=\"shotCanvas\"", html, StringComparison.Ordinal);

        Assert.True(LanWebAssets.TryGet("app.js", out _, out var jsBytes));
        var js = Encoding.UTF8.GetString(jsBytes);
        foreach (var key in new[] { "mode.shot", "shot.save", "shot.copy", "shot.cancel", "shot.save.fail", "shot.copy.fail" })
        {
            Assert.Contains("\"" + key + "\"", js, StringComparison.Ordinal);
        }

        foreach (var label in new[] { "Screenshot", "截图", "截圖", "スクリーンショット", "스크린샷" })
        {
            Assert.Contains(label, js, StringComparison.Ordinal);
        }
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(255, 12, 80, 160));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private sealed class Host : IAsyncDisposable
    {
        public required LanServer Server { get; init; }
        public required AppSettings Settings { get; init; }
        public required string Folder { get; init; }
        public required string Url { get; init; }

        public static Task<Host> Start()
        {
            var folder = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var store = new SettingsStore(Path.Combine(folder, "settings.json"));
            var settings = store.Load();
            settings.Lan.Enabled = true;
            settings.Lan.Port = FreePort();
            settings.Lan.AccessKey = "";
            settings.SaveFolder = folder;
            var server = new LanServer(store, () => settings);
            server.Start();
            Assert.True(server.IsRunning, server.LastError);
            return Task.FromResult(new Host
            {
                Server = server,
                Settings = settings,
                Folder = folder,
                Url = "http://127.0.0.1:" + settings.Lan.Port
            });
        }

        public ValueTask DisposeAsync()
        {
            Server.Dispose();
            return ValueTask.CompletedTask;
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
