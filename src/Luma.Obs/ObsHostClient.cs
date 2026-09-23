using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Luma.Core.Session;
using Luma.Core.Settings;

namespace Luma.Obs;

public sealed class ObsHostClient : IRecordingEngine, IDisposable
{
    public const string PipeName = "luma-obs-host";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly SemaphoreSlim _send = new(1, 1);
    private Process? _process;
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private EngineStatus _status = new();
    private string _outputPath = "";
    private Task<RecordingResult>? _stopInFlight;

    public async Task EnsureStartedAsync(CancellationToken token = default)
    {
        if (_pipe is { IsConnected: true } && _process is { HasExited: false })
        {
            return;
        }

        ResetConnection();
        var host = FindHost() ?? throw new InvalidOperationException("未找到 Luma.ObsHost。请先构建 native/Luma.ObsHost 并拉取 OBS 运行时。");
        var workDir = Path.GetDirectoryName(host) ?? AppContext.BaseDirectory;
        _process = Process.Start(new ProcessStartInfo
        {
            FileName = host,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir
        }) ?? throw new InvalidOperationException("无法启动 Luma.ObsHost。");

        _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"Luma.ObsHost 启动后立即退出，代码 {_process.ExitCode}。");
            }

            try
            {
                await _pipe.ConnectAsync(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        if (_pipe is not { IsConnected: true })
        {
            throw new InvalidOperationException($"无法连接 Luma.ObsHost：{last?.Message}");
        }

        _reader = new StreamReader(_pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
    }

    public async Task StartAsync(RecordingRequest request, CancellationToken token = default)
    {
        try
        {
            await EnsureStartedAsync(token).ConfigureAwait(false);
            var overlay = request.Overlay;
            var marks = overlay?.Watermarks ?? [];
            var text = marks.FirstOrDefault(item => item.Kind == WatermarkKind.Text);
            var image = marks.FirstOrDefault(item => item.Kind == WatermarkKind.Image);
            var stamp = marks.FirstOrDefault(item => item.Kind == WatermarkKind.Timestamp);
            var mark = text ?? image ?? stamp;
            var status = await SendAsync(new
            {
                op = "start",
                outputPath = request.OutputPath,
                posterPath = Luma.Core.Library.LibraryPaths.PreparePoster(request.OutputPath),
                mode = request.Target.Mode.ToString(),
                monitorIndex = request.Target.MonitorIndex,
                windowId = request.Target.WindowId ?? "",
                cropX = request.Target.CropX,
                cropY = request.Target.CropY,
                cropWidth = request.Target.CropWidth,
                cropHeight = request.Target.CropHeight,
                width = request.Quality.Resolution.Width,
                height = request.Quality.Resolution.Height,
                fps = request.Quality.FrameRate,
                bitrateKbps = request.Quality.BitrateKbps,
                hardware = request.Quality.HardwareEncoding,
                systemAudio = request.CaptureSystemAudio,
                microphone = request.CaptureMicrophone,
                micId = request.MicrophoneDeviceId ?? "",
                camera = overlay?.CameraEnabled == true,
                cameraId = overlay?.CameraDeviceName ?? "",
                cameraX = overlay?.CameraX ?? 0.5,
                cameraY = overlay?.CameraY ?? 0.5,
                cameraW = overlay?.CameraWidth ?? 0.24,
                cameraH = overlay?.CameraHeight ?? 0.24,
                textMark = text?.Content ?? "",
                textOpacity = text?.Opacity ?? 1,
                imagePath = image?.Content ?? "",
                imageOpacity = image?.Opacity ?? 0.9,
                timestamp = stamp is not null,
                markX = mark?.X ?? 0.02,
                markY = mark?.Y ?? 0.02,
                markW = mark?.Width ?? 0.2,
                markH = mark?.Height ?? 0.08
            }, token).ConfigureAwait(false);
            if (!status.Ok)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.Error) ? "开始录制失败。" : status.Error);
            }

            _outputPath = status.OutputPath;
            _status = status.ToEngineStatus();
        }
        catch
        {
            ResetConnection();
            throw;
        }
    }

    public async Task PauseAsync()
    {
        var status = await SendAsync(new { op = "pause" }).ConfigureAwait(false);
        ThrowIfFailed(status, "暂停失败。");
        _status = status.ToEngineStatus();
    }

    public async Task ResumeAsync()
    {
        var status = await SendAsync(new { op = "resume" }).ConfigureAwait(false);
        ThrowIfFailed(status, "恢复失败。");
        _status = status.ToEngineStatus();
    }

    public async Task MuteAsync(bool muted)
    {
        await EnsureStartedAsync().ConfigureAwait(false);
        var status = await SendAsync(new { op = "mute", muted }).ConfigureAwait(false);
        ThrowIfFailed(status, "麦克风静音失败。");
    }

    public Task<RecordingResult> StopAsync()
    {
        lock (_gate)
        {
            return _stopInFlight ??= StopCoreAsync();
        }
    }

    private async Task<RecordingResult> StopCoreAsync()
    {
        try
        {
            var status = await SendAsync(new { op = "stop" }).ConfigureAwait(false);
            _status = status.ToEngineStatus();
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while ((SessionPhase)status.Phase == SessionPhase.Processing)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new InvalidOperationException("录制停止超过 120 秒；控制面仍可用，但 OBS/驱动停止线程未返回，请重启 Luma 后再录制");
                }

                await Task.Delay(200).ConfigureAwait(false);
                status = await SendAsync(new { op = "status" }).ConfigureAwait(false);
                _status = status.ToEngineStatus();
            }

            if ((SessionPhase)status.Phase != SessionPhase.Idle && !status.Ok)
            {
                ThrowIfFailed(status, "停止录制失败。");
            }

            var path = string.IsNullOrWhiteSpace(status.OutputPath) ? _outputPath : status.OutputPath;
            if (path.EndsWith(".partial.mkv", StringComparison.OrdinalIgnoreCase))
            {
                var finalPath = path[..^".partial.mkv".Length];
                path = await RemuxAsync(path, finalPath).ConfigureAwait(false);
            }
            else if (path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
            {
                path = await StripVideoAsync(path).ConfigureAwait(false);
            }

            var bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            var probed = ProbeDuration(path);
            var usable = bytes > 0 && (FindFfmpeg() is null || StopFileRules.IsUsable(bytes, probed, _status.EncodedDuration));
            if (!usable)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.Error) ? "成片未通过校验。" : status.Error);
            }

            if (status.StopForced || !status.Ok)
            {
                status.Ok = true;
                status.Error = "";
                status.StopForced = true;
                status.Warning = StopFileRules.ForcedWarning;
                _status = status.ToEngineStatus();
            }

            return new RecordingResult
            {
                OutputPath = path,
                Duration = _status.EncodedDuration,
                Status = _status
            };
        }
        finally
        {
            lock (_gate)
            {
                _stopInFlight = null;
            }
        }
    }

    public EngineStatus GetStatus() => _status;

    public async Task RefreshStatusAsync(CancellationToken token = default)
    {
        if (!await TryRefreshStatusAsync(token).ConfigureAwait(false) && _pipe is not { IsConnected: true })
        {
            throw new InvalidOperationException("OBS 宿主没有返回状态。");
        }
    }

    public async Task<bool> TryRefreshStatusAsync(CancellationToken token = default)
    {
        if (_pipe is not { IsConnected: true } || _process is { HasExited: true })
        {
            return false;
        }

        try
        {
            var status = await SendAsync(new { op = "status" }, token).ConfigureAwait(false);
            if (!status.Ok)
            {
                return false;
            }

            _status = status.ToEngineStatus();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A failed poll must leave the live recording connected.
            return false;
        }
    }

    public void Dispose() => ResetConnection();

    private async Task<HostStatus> SendAsync(object message, CancellationToken token = default)
    {
        await _send.WaitAsync(token).ConfigureAwait(false);
        try
        {
            NamedPipeClientStream pipe;
            StreamReader reader;
            lock (_gate)
            {
                if (_pipe is null || _reader is null)
                {
                    throw new InvalidOperationException("OBS 宿主未连接。");
                }

                pipe = _pipe;
                reader = _reader;
            }

            var json = JsonSerializer.Serialize(message, JsonOptions) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, token).ConfigureAwait(false);
            await pipe.FlushAsync(token).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(token).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("OBS 宿主没有返回状态。");
            return JsonSerializer.Deserialize<HostStatus>(line, JsonOptions)
                   ?? throw new InvalidOperationException("无法解析宿主状态。");
        }
        finally
        {
            _send.Release();
        }
    }

    private void ResetConnection()
    {
        lock (_gate)
        {
            try { _reader?.Dispose(); } catch { /* ignore */ }
            try { _pipe?.Dispose(); } catch { /* ignore */ }
            _reader = null;
            _pipe = null;
            if (_process is { HasExited: false })
            {
                try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            _process?.Dispose();
            _process = null;
        }
    }

    private static async Task<string> RemuxAsync(string source, string destination)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null || !File.Exists(source))
        {
            return File.Exists(destination) ? destination : source;
        }

        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-y");
        start.ArgumentList.Add("-i");
        start.ArgumentList.Add(source);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("copy");
        start.ArgumentList.Add(destination);
        using var process = Process.Start(start);
        if (process is null)
        {
            return source;
        }

        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0 || !File.Exists(destination) || new FileInfo(destination).Length == 0)
        {
            return source;
        }

        try { File.Delete(source); } catch { /* the mp4 is the file the library should keep */ }
        _ = error;
        return destination;
    }

    private static async Task<string> StripVideoAsync(string path)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null || !File.Exists(path))
        {
            return path;
        }

        var temp = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), Path.GetFileNameWithoutExtension(path) + ".audio.m4a");
        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-y");
        start.ArgumentList.Add("-i");
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("-vn");
        start.ArgumentList.Add("-c:a");
        start.ArgumentList.Add("copy");
        start.ArgumentList.Add(temp);
        using var process = Process.Start(start);
        if (process is null)
        {
            return path;
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0 || !File.Exists(temp) || new FileInfo(temp).Length == 0)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* keep the original */ }
            return path;
        }

        File.Delete(path);
        File.Move(temp, path);
        return path;
    }

    private static TimeSpan ProbeDuration(string path)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null || !File.Exists(path))
        {
            return TimeSpan.Zero;
        }

        try
        {
            var start = new ProcessStartInfo(ffmpeg)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-hide_banner");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start);
            if (process is null)
            {
                return TimeSpan.Zero;
            }

            var read = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return TimeSpan.Zero;
            }

            var text = read.GetAwaiter().GetResult();

            var match = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!match.Success)
            {
                return TimeSpan.Zero;
            }

            var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return new TimeSpan(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0) + TimeSpan.FromSeconds(seconds);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static string? FindFfmpeg()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        return Environment.GetEnvironmentVariable("PATH")
            ?.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static void ThrowIfFailed(HostStatus status, string fallback)
    {
        if (!status.Ok)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.Error) ? fallback : status.Error);
        }
    }

    public static string? FindHost()
    {
        return EnumerateHostCandidates()
            .Where(path => File.Exists(path) && File.Exists(Path.Combine(Path.GetDirectoryName(path) ?? "", "obs.dll")))
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateHostCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Luma.ObsHost.exe");
        yield return Path.Combine(AppContext.BaseDirectory, "obs-runtime", "Luma.ObsHost.exe");

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, ".obs-runtime", "Luma.ObsHost.exe");
            yield return Path.Combine(dir.FullName, "native", "Luma.ObsHost", "build", "bin", "Luma.ObsHost.exe");
        }
    }
}
