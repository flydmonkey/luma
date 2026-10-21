using System.Net;
using System.Net.Sockets;
using Luma.Core.Lan;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class LanWebTests
{
    [Theory]
    [InlineData("en", "access key", "Open")]
    [InlineData("zh-Hans", "访问密钥", "打开")]
    [InlineData("ja", "アクセスキー", "開く")]
    [InlineData("xx", "access key", "Open")]
    public void Unlock_page_follows_the_ui_language(string locale, string prompt, string button)
    {
        var html = LanWebAssets.UnlockHtml(wrongKey: false, locale);
        Assert.Contains(prompt, html);
        Assert.Contains($">{button}</button>", html);
        Assert.DoesNotContain("__", html);
    }

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

    [Fact]
    public async Task Settings_patch_updates_screen_aids_and_audio_levels()
    {
        var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.Lan.Enabled = true;
        settings.Lan.Port = FreePort();
        settings.Lan.AccessKey = "";
        using var server = new LanServer(store, () => settings);
        server.Start();
        Assert.True(server.IsRunning, server.LastError);
        using var client = new HttpClient();
        using var patch = new StringContent(
            """{"screenAids":{"showCursor":false,"highlightClicks":true,"annotation":true,"inkColor":"#3B82F6","inkWidth":8},"audio":{"captureSystem":true,"microphoneVolume":150,"noiseSuppression":false}}""",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"http://127.0.0.1:{settings.Lan.Port}/api/v1/settings", patch);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.False(settings.ScreenAids.ShowCursor);
        Assert.True(settings.ScreenAids.HighlightClicks);
        Assert.True(settings.ScreenAids.Annotation);
        Assert.Equal("#3B82F6", settings.ScreenAids.InkColor);
        Assert.Equal(8, settings.ScreenAids.InkWidth);
        Assert.Equal(150, settings.Audio.MicrophoneVolume);
        Assert.False(settings.Audio.NoiseSuppression);
        server.Stop();
    }

    [Fact]
    public void Screen_aids_apply_to_display_and_region_only()
    {
        Assert.True(ScreenAidSettings.AppliesTo(Luma.Core.Session.CaptureMode.Display));
        Assert.True(ScreenAidSettings.AppliesTo(Luma.Core.Session.CaptureMode.Region));
        Assert.False(ScreenAidSettings.AppliesTo(Luma.Core.Session.CaptureMode.Window));
        Assert.False(ScreenAidSettings.AppliesTo(Luma.Core.Session.CaptureMode.AudioOnly));
    }

    [Fact]
    public void Ink_style_falls_back_to_red_and_medium()
    {
        Assert.Equal("#3B82F6", ScreenAidSettings.NormalizeInkColor("#3b82f6"));
        Assert.Equal("#EB2D2D", ScreenAidSettings.NormalizeInkColor("#123456"));
        Assert.Equal("#EB2D2D", ScreenAidSettings.NormalizeInkColor(null));
        Assert.Equal(8, ScreenAidSettings.NormalizeInkWidth(8));
        Assert.Equal(4, ScreenAidSettings.NormalizeInkWidth(5));
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
