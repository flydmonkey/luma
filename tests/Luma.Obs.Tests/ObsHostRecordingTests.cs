using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Luma.Core.Lan;
using Luma.Core.Session;
using Luma.Core.Settings;
using Luma.Media;
using Luma.Obs;

namespace Luma.Obs.Tests;

public sealed class ObsHostRecordingTests
{
    [Fact]
    public async Task Host_records_playable_mp4()
    {
        if (ObsHostClient.FindHost() is null)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"luma-obs-test-{Guid.NewGuid():N}.mp4");
        using var host = new ObsHostClient();
        try
        {
            await host.StartAsync(new RecordingRequest
            {
                OutputPath = path,
                Target = new CaptureTarget { Mode = CaptureMode.Display },
                Quality = QualitySettings.FromLevel(QualityLevel.Sd, 30),
                CaptureSystemAudio = false,
                CaptureMicrophone = false
            });

            await Task.Delay(1500);
            var stopTask = host.StopAsync();
            var sawStatus = false;
            while (!stopTask.IsCompleted)
            {
                sawStatus |= await host.TryRefreshStatusAsync();
                await Task.Delay(20);
            }

            var result = await stopTask;
            Assert.True(sawStatus, "停录期间 status 查询没有返回。");
            Assert.True(File.Exists(result.OutputPath), "录制结束后应留下文件。");
            Assert.True(new FileInfo(result.OutputPath).Length > 1024, "成片过小，可能没有写入媒体数据。");
            Assert.False(string.Equals(result.Status.EncoderName, "hardware", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(result.Status.EncoderName));
            Assert.NotEqual("obs_qsv11", result.Status.EncoderName);
            if (result.Status.UsedHardware)
            {
                Assert.Contains(result.Status.EncoderName, new[] { "h264_texture_amf", "jim_nvenc", "ffmpeg_nvenc", "obs_qsv11_v2" });
                Assert.DoesNotContain("回退", result.Status.Warning ?? "");
            }
            else
            {
                Assert.Equal("obs_x264", result.Status.EncoderName);
                Assert.Contains("回退", result.Status.Warning ?? "");
            }
            Assert.True(result.Duration.TotalSeconds > 0.3);
            Assert.Equal(30, result.Status.EffectiveFps, 0);
            var fileDuration = MediaProbe.TryDuration(result.OutputPath);
            Assert.True(fileDuration > TimeSpan.Zero);
            Assert.True(Math.Abs(fileDuration.TotalSeconds - result.Duration.TotalSeconds) <= 1.5);
            Assert.False(string.Equals(result.Status.EncoderName, "hardware", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Host_records_audio_only_m4a()
    {
        if (ObsHostClient.FindHost() is null)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"luma-obs-audio-{Guid.NewGuid():N}.m4a");
        using var host = new ObsHostClient();
        try
        {
            await host.StartAsync(new RecordingRequest
            {
                OutputPath = path,
                Target = new CaptureTarget { Mode = CaptureMode.AudioOnly },
                Quality = QualitySettings.FromLevel(QualityLevel.Sd, 30),
                CaptureSystemAudio = true,
                CaptureMicrophone = false
            });

            await Task.Delay(1200);
            var result = await host.StopAsync();
            Assert.True(File.Exists(result.OutputPath));
            Assert.EndsWith(".m4a", result.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(new FileInfo(result.OutputPath).Length > 256);
            var described = MediaProbe.Describe(result.OutputPath);
            Assert.Contains("Audio:", described);
            Assert.DoesNotContain("Video:", described);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Killed_host_leaves_a_file_that_repair_can_open()
    {
        if (ObsHostClient.FindHost() is null)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"luma-obs-kill-{Guid.NewGuid():N}.mp4");
        var before = Process.GetProcessesByName("Luma.ObsHost").Select(item => item.Id).ToHashSet();
        using var host = new ObsHostClient();
        try
        {
            await host.StartAsync(new RecordingRequest
            {
                OutputPath = path,
                Target = new CaptureTarget { Mode = CaptureMode.Display },
                Quality = QualitySettings.FromLevel(QualityLevel.Sd, 30),
                CaptureSystemAudio = false,
                CaptureMicrophone = false
            });
            await Task.Delay(3000);
            var partialEarly = path + ".partial.mkv";
            var sizeBeforeKill = File.Exists(partialEarly) ? new FileInfo(partialEarly).Length : -1;
            var created = Process.GetProcessesByName("Luma.ObsHost").First(item => !before.Contains(item.Id));
            created.Kill(entireProcessTree: true);
            await Task.Delay(400);
            var partial = path + ".partial.mkv";
            var leftovers = Directory.GetFiles(Path.GetTempPath(), "luma-obs-kill-*");
            Assert.True(File.Exists(partial), "beforeKill=" + sizeBeforeKill + " left " + string.Join(", ", leftovers));
            Assert.True(new FileInfo(partial).Length > 1024);
            var repaired = await new MediaEditor().RepairAsync(partial);
            Assert.True(File.Exists(repaired));
            Assert.True(new FileInfo(repaired).Length > 0);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
            foreach (var extra in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "*"))
            {
                try { File.Delete(extra); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task Five_minute_display_recording_stays_within_one_second()
    {
        if (ObsHostClient.FindHost() is null)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"luma-obs-5min-{Guid.NewGuid():N}.mp4");
        using var host = new ObsHostClient();
        try
        {
            await host.StartAsync(new RecordingRequest
            {
                OutputPath = path,
                Target = new CaptureTarget { Mode = CaptureMode.Display },
                Quality = QualitySettings.FromLevel(QualityLevel.Sd, 30),
                CaptureSystemAudio = true,
                CaptureMicrophone = false
            });
            await Task.Delay(TimeSpan.FromMinutes(5));
            var result = await host.StopAsync();
            var fileDuration = MediaProbe.TryDuration(result.OutputPath);
            var described = MediaProbe.Describe(result.OutputPath);
            Assert.Contains("Video:", described);
            Assert.Contains("Audio:", described);
            Assert.True(Math.Abs(fileDuration.TotalSeconds - result.Duration.TotalSeconds) <= 1);
            Assert.False(string.Equals(result.Status.EncoderName, "hardware", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Lan_address_start_stop_writes_only_on_this_pc()
    {
        if (ObsHostClient.FindHost() is null)
        {
            return;
        }

        var lan = Dns.GetHostAddresses(Dns.GetHostName())
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));
        Assert.NotNull(lan);
        var folder = Path.Combine(Path.GetTempPath(), "luma-lan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var storePath = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(storePath);
        var settings = store.Load();
        settings.Lan.Enabled = true;
        settings.Lan.Port = FreePort();
        settings.SaveFolder = folder;
        using var host = new ObsHostClient();
        using var server = new LanServer(store, () => settings);
        server.SessionCommand += async payload =>
        {
            var route = payload.GetProperty("path").GetString() ?? "";
            if (route.EndsWith("/start", StringComparison.Ordinal))
            {
                var output = Path.Combine(folder, "lan-take.mp4");
                await host.StartAsync(new RecordingRequest
                {
                    OutputPath = output,
                    Target = new CaptureTarget { Mode = CaptureMode.Display },
                    Quality = QualitySettings.FromLevel(QualityLevel.Sd, 30),
                    CaptureSystemAudio = false,
                    CaptureMicrophone = false
                });
                return JsonSerializer.SerializeToElement(new { ok = true });
            }

            if (route.EndsWith("/stop", StringComparison.Ordinal))
            {
                var result = await host.StopAsync();
                return JsonSerializer.SerializeToElement(new { ok = true, path = result.OutputPath });
            }

            return JsonSerializer.SerializeToElement(new { ok = true });
        };
        server.Start();
        Assert.True(server.IsRunning, server.LastError);
        using var client = new HttpClient();
        var root = $"http://{lan}:{settings.Lan.Port}";
        var page = await client.GetStringAsync(root + "/");
        Assert.Contains("Luma", page);
        var started = await client.PostAsync(root + "/api/v1/session/start", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        await Task.Delay(1200);
        var stopped = await client.PostAsync(root + "/api/v1/session/stop", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.True(stopped.IsSuccessStatusCode, await stopped.Content.ReadAsStringAsync());
        var files = Directory.GetFiles(folder, "*.mp4");
        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.StartsWith(folder, file, StringComparison.OrdinalIgnoreCase));
        foreach (var file in files)
        {
            try { File.Delete(file); } catch { /* ignore */ }
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
