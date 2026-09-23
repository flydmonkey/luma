using System.Net;
using System.Net.Sockets;
using Luma.Core.Lan;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class LanWebTests
{
    [Fact]
    public async Task Enabled_server_serves_three_pages_and_rejects_port_patch()
    {
        var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.Lan.Enabled = true;
        settings.Lan.Port = FreePort();
        settings.Lan.AccessKey = "";
        settings.UiLanguage = "zh-Hans";
        using var server = new LanServer(store, () => settings);
        server.Start();
        Assert.True(server.IsRunning, server.LastError);
        using var client = new HttpClient();
        var html = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/");
        Assert.Contains("id=\"recordPage\"", html);
        Assert.Contains("id=\"libraryPage\"", html);
        Assert.Contains("id=\"settingsPage\"", html);
        var privacy = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/legal/privacy");
        var terms = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/legal/terms");
        var skill = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/skill");
        var docs = await client.GetStringAsync($"http://127.0.0.1:{settings.Lan.Port}/api/docs");
        Assert.Contains("隐私", privacy);
        Assert.Contains("使用", terms);
        Assert.Contains("Luma Control", skill);
        Assert.Contains("rapi-doc", docs);

        using var patch = new StringContent("""{"lanPort":9}""", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"http://127.0.0.1:{settings.Lan.Port}/api/v1/settings", patch);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        server.Stop();
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
