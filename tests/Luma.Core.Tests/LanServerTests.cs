using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Luma.Core.Lan;
using Luma.Core.Session;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class LanServerTests
{
    [Fact]
    public void Default_port_is_12345_and_disabled_server_does_not_start()
    {
        var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        Assert.Equal(12345, settings.Lan.Port);
        settings.Lan.Enabled = false;
        using var server = new LanServer(store, () => settings);
        server.Start();
        Assert.False(server.IsRunning);
        Assert.Equal(0, server.BoundPort);
        Assert.Empty(server.BoundUrls);
    }

    [Fact]
    public void Occupied_port_is_reported_and_not_rebound()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:23457/");
        listener.Start();
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
            var store = new SettingsStore(path);
            var settings = store.Load();
            settings.Lan.Enabled = true;
            settings.Lan.Port = 23457;
            using var server = new LanServer(store, () => settings);
            server.Start();
            Assert.False(server.IsRunning);
            Assert.Contains("占用", server.LastError);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public void Window_start_without_target_is_rejected()
    {
        var reason = SessionStartRules.Reject(new CaptureTarget { Mode = CaptureMode.Window });
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Null(SessionStartRules.Reject(new CaptureTarget { Mode = CaptureMode.Display }));
    }

    [Fact]
    public async Task Session_start_without_target_returns_error_and_writes_no_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.Lan.Enabled = true;
        settings.Lan.Port = FreePort();
        settings.SaveFolder = folder;
        using var server = new LanServer(store, () => settings);
        server.SessionCommand += payload =>
        {
            var route = payload.GetProperty("path").GetString();
            Assert.Equal("/api/v1/session/start", route);
            var reason = SessionStartRules.Reject(new CaptureTarget { Mode = CaptureMode.Game });
            return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = false, error = reason }));
        };
        server.ListTargets = _ => new[] { new { id = "0", label = "Display 1", previewMissing = true } };
        server.Start();
        Assert.True(server.IsRunning, server.LastError);

        using var client = new HttpClient();
        var response = await client.PostAsync($"http://127.0.0.1:{settings.Lan.Port}/api/v1/session/start", new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(body.TrimStart().StartsWith("{"), body);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("error").GetString()));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.mp4"));

        var targets = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/api/v1/targets?kind=displays");
        Assert.Contains("Display 1", targets);
        var remote = server.BoundUrls.FirstOrDefault(url => url.Contains("://") && !url.Contains("127.0.0.1") && !url.Contains("localhost"));
        if (remote is not null)
        {
            var page = await client.GetStringAsync(remote + "/");
            Assert.Contains("Luma", page);
        }
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
