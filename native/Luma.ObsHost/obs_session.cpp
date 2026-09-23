#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "obs_session.h"
#include "json_util.h"

#include <obs.h>
#include <util/base.h>

#include <windows.h>
#include <d3d11.h>
#include <dxgi1_5.h>

#include <atomic>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <fstream>
#include <thread>
#include <mutex>
#include <sstream>
#include <vector>

namespace
{
std::mutex g_mutex;
std::atomic<bool> g_stopRunning{false};
std::thread g_stopThread;
bool g_ready = false;
bool g_videoReady = false;
int g_canvasW = 0;
int g_canvasH = 0;
int g_fps = 30;

obs_scene_t* g_scene = nullptr;
obs_source_t* g_videoSource = nullptr;
obs_source_t* g_systemAudio = nullptr;
obs_source_t* g_mic = nullptr;
std::vector<obs_source_t*> g_extra;
obs_source_t* g_stamp = nullptr;
std::atomic<bool> g_stampRun{false};
std::thread g_stampThread;
obs_encoder_t* g_venc = nullptr;
obs_encoder_t* g_aenc = nullptr;
obs_output_t* g_output = nullptr;

SessionStatus g_status{};
ULONGLONG g_startTick = 0;
double g_pauseAccum = 0;
ULONGLONG g_pauseTick = 0;

std::ofstream g_log;

std::string WideToUtf8(const wchar_t* wide)
{
    if (!wide || !wide[0])
    {
        return {};
    }
    const int n = WideCharToMultiByte(CP_UTF8, 0, wide, -1, nullptr, 0, nullptr, nullptr);
    if (n <= 1)
    {
        return {};
    }
    std::string out(static_cast<size_t>(n - 1), 0);
    WideCharToMultiByte(CP_UTF8, 0, wide, -1, out.data(), n, nullptr, nullptr);
    return out;
}

std::wstring Utf8ToWide(const std::string& utf8)
{
    if (utf8.empty())
    {
        return {};
    }
    const int n = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
    if (n <= 1)
    {
        return {};
    }
    std::wstring out(static_cast<size_t>(n - 1), 0);
    MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, out.data(), n);
    return out;
}

std::string HostDirUtf8()
{
    wchar_t path[MAX_PATH]{};
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    if (wchar_t* slash = wcsrchr(path, L'\\'))
    {
        *slash = 0;
    }
    return WideToUtf8(path);
}

void LogLine(const char* fmt, ...)
{
    char buf[2048];
    va_list args;
    va_start(args, fmt);
    vsprintf_s(buf, fmt, args);
    va_end(args);
    if (g_log)
    {
        g_log << buf << std::endl;
        g_log.flush();
    }
}

void ObsLogHandler(int level, const char* format, va_list args, void*)
{
    char buf[4096];
    vsnprintf(buf, sizeof(buf), format, args);
    const char* tag = level >= LOG_ERROR ? "E" : level >= LOG_WARNING ? "W" : "I";
    LogLine("[obs %s] %s", tag, buf);
}

void OpenLog()
{
    wchar_t appdata[MAX_PATH]{};
    if (GetEnvironmentVariableW(L"LOCALAPPDATA", appdata, MAX_PATH) == 0)
    {
        return;
    }
    std::wstring dir = std::wstring(appdata) + L"\\Luma";
    CreateDirectoryW(dir.c_str(), nullptr);
    g_log.open((dir + L"\\obs-host.log").c_str(), std::ios::out | std::ios::trunc);
}

int Even(int value)
{
    return value < 2 ? 2 : (value & ~1);
}

struct MonitorPick
{
    int wanted = 0;
    int seen = 0;
    bool found = false;
    std::wstring device;
};

BOOL CALLBACK PickMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM data)
{
    auto* pick = reinterpret_cast<MonitorPick*>(data);
    if (pick->seen++ != pick->wanted)
    {
        return TRUE;
    }

    MONITORINFOEXW info{};
    info.cbSize = sizeof(info);
    if (GetMonitorInfoW(monitor, &info))
    {
        pick->device = info.szDevice;
        pick->found = true;
    }
    return FALSE;
}

// Index must follow EnumDisplayMonitors, the order the app lists displays in.
// EnumDisplayDevices also counts detached outputs, so index 0 can be a
// \\.\DISPLAY1 with no screen while the real monitor is \\.\DISPLAY2.
void GetMonitorSize(int index, int& width, int& height, std::string& monitorId)
{
    width = GetSystemMetrics(SM_CXSCREEN);
    height = GetSystemMetrics(SM_CYSCREEN);
    MonitorPick pick;
    pick.wanted = index < 0 ? 0 : index;
    EnumDisplayMonitors(nullptr, nullptr, PickMonitor, reinterpret_cast<LPARAM>(&pick));
    if (!pick.found && pick.wanted != 0)
    {
        pick = {};
        EnumDisplayMonitors(nullptr, nullptr, PickMonitor, reinterpret_cast<LPARAM>(&pick));
    }
    if (!pick.found)
    {
        return;
    }

    monitorId = WideToUtf8(pick.device.c_str());
    DEVMODEW mode{};
    mode.dmSize = sizeof(mode);
    if (EnumDisplaySettingsW(pick.device.c_str(), ENUM_CURRENT_SETTINGS, &mode))
    {
        width = static_cast<int>(mode.dmPelsWidth);
        height = static_cast<int>(mode.dmPelsHeight);
    }
}

// duplicator-monitor-capture: 1 = DXGI, 2 = WGC.
// DuplicateOutput1 is unsupported on some AMD drivers and the picture is black.
// Forcing WGC on every machine blacks out Intel, where DXGI already works.
int ChooseDisplayCaptureMethod()
{
    static int cached = 0;
    if (cached != 0)
    {
        return cached;
    }

    const HRESULT unsupported = static_cast<HRESULT>(0x887A0004);
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    D3D_FEATURE_LEVEL level{};
    const HRESULT created = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        &device,
        &level,
        &context);
    if (FAILED(created) || device == nullptr)
    {
        LogLine("display capture WGC: D3D11 probe failed 0x%08lX", created);
        cached = 2;
        return cached;
    }

    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* adapter = nullptr;
    device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    if (dxgiDevice != nullptr)
    {
        dxgiDevice->GetAdapter(&adapter);
    }

    HRESULT duplication = unsupported;
    bool dxgiOk = false;
    if (adapter != nullptr)
    {
        for (UINT index = 0;; index++)
        {
            IDXGIOutput* output = nullptr;
            if (adapter->EnumOutputs(index, &output) == DXGI_ERROR_NOT_FOUND)
            {
                break;
            }

            IDXGIOutput5* output5 = nullptr;
            if (SUCCEEDED(output->QueryInterface(__uuidof(IDXGIOutput5), reinterpret_cast<void**>(&output5)))
                && output5 != nullptr)
            {
                const DXGI_FORMAT format = DXGI_FORMAT_B8G8R8A8_UNORM;
                IDXGIOutputDuplication* dup = nullptr;
                duplication = output5->DuplicateOutput1(device, 0, 1, &format, &dup);
                if (SUCCEEDED(duplication) && dup != nullptr)
                {
                    dxgiOk = true;
                    dup->Release();
                }

                output5->Release();
            }

            output->Release();
            if (dxgiOk)
            {
                break;
            }
        }
    }

    if (adapter != nullptr)
    {
        adapter->Release();
    }
    if (dxgiDevice != nullptr)
    {
        dxgiDevice->Release();
    }
    context->Release();
    device->Release();

    cached = dxgiOk ? 1 : 2;
    LogLine("display capture %s (DuplicateOutput1 0x%08lX)", dxgiOk ? "DXGI" : "WGC", duplication);
    return cached;
}

void StopStamp();

void ReleaseGraph(bool drain = true)
{
    StopStamp();
    if (g_output)
    {
        if (obs_output_active(g_output))
        {
            obs_output_stop(g_output);
            if (drain)
            {
                for (int i = 0; i < 50 && obs_output_active(g_output); i++)
                {
                    Sleep(100);
                }
            }
            if (obs_output_active(g_output))
            {
                obs_output_force_stop(g_output);
            }
        }
        obs_output_release(g_output);
        g_output = nullptr;
    }
    if (g_venc)
    {
        obs_encoder_release(g_venc);
        g_venc = nullptr;
    }
    if (g_aenc)
    {
        obs_encoder_release(g_aenc);
        g_aenc = nullptr;
    }
    obs_set_output_source(0, nullptr);
    if (g_scene)
    {
        obs_scene_release(g_scene);
        g_scene = nullptr;
    }
    if (g_videoSource)
    {
        obs_source_release(g_videoSource);
        g_videoSource = nullptr;
    }
    if (g_systemAudio)
    {
        obs_source_release(g_systemAudio);
        g_systemAudio = nullptr;
    }
    if (g_mic)
    {
        obs_source_release(g_mic);
        g_mic = nullptr;
    }
    for (obs_source_t* source : g_extra)
    {
        obs_source_release(source);
    }
    g_extra.clear();
}

void StopStamp()
{
    g_stampRun = false;
    if (g_stampThread.joinable())
    {
        g_stampThread.join();
    }
    g_stamp = nullptr;
}

bool ResetVideoAudio(int canvasW, int canvasH, int outW, int outH, int fps, std::string& error)
{
    canvasW = Even(canvasW);
    canvasH = Even(canvasH);
    outW = Even(outW);
    outH = Even(outH);
    if (fps < 1)
    {
        fps = 30;
    }

    obs_video_info ovi{};
    ovi.graphics_module = "libobs-d3d11";
    ovi.fps_num = static_cast<uint32_t>(fps);
    ovi.fps_den = 1;
    ovi.base_width = static_cast<uint32_t>(canvasW);
    ovi.base_height = static_cast<uint32_t>(canvasH);
    ovi.output_width = static_cast<uint32_t>(outW);
    ovi.output_height = static_cast<uint32_t>(outH);
    ovi.output_format = VIDEO_FORMAT_NV12;
    ovi.adapter = 0;
    ovi.gpu_conversion = true;
    ovi.colorspace = VIDEO_CS_709;
    ovi.range = VIDEO_RANGE_PARTIAL;
    ovi.scale_type = OBS_SCALE_BICUBIC;

    const int vr = obs_reset_video(&ovi);
    if (vr != OBS_VIDEO_SUCCESS)
    {
        std::ostringstream oss;
        oss << "obs_reset_video 失败，代码 " << vr;
        error = oss.str();
        LogLine("%s", error.c_str());
        return false;
    }

    obs_audio_info oai{};
    oai.samples_per_sec = 48000;
    oai.speakers = SPEAKERS_STEREO;
    if (!obs_reset_audio(&oai))
    {
        error = "obs_reset_audio 失败。";
        LogLine("%s", error.c_str());
        return false;
    }

    g_canvasW = canvasW;
    g_canvasH = canvasH;
    g_fps = fps;
    g_videoReady = true;
    return true;
}

obs_source_t* CreateInput(const char* unversionedId, const char* name, obs_data_t* settings)
{
    const char* id = obs_get_latest_input_type_id(unversionedId);
    if (!id || !id[0])
    {
        id = unversionedId;
    }
    LogLine("create source %s (%s)", id, name);
    return obs_source_create(id, name, settings, nullptr);
}

void ApplyCommonEncoderSettings(obs_data_t* settings, const StartRequest& request)
{
    obs_data_set_string(settings, "rate_control", "CBR");
    obs_data_set_int(settings, "bitrate", request.bitrateKbps);
    obs_data_set_int(settings, "max_bitrate", request.bitrateKbps);
    obs_data_set_int(settings, "keyint_sec", 2);
    obs_data_set_string(settings, "preset", "p5");
    obs_data_set_string(settings, "profile", "high");
    obs_data_set_string(settings, "tune", "zerolatency");
    obs_data_set_bool(settings, "psycho_aq", false);
}

obs_encoder_t* CreateVideoEncoderById(const char* id, const StartRequest& request)
{
    obs_data_t* settings = obs_data_create();
    ApplyCommonEncoderSettings(settings, request);
    if (strcmp(id, "obs_x264") == 0)
    {
        obs_data_set_string(settings, "preset", "veryfast");
    }
    obs_encoder_t* enc = obs_video_encoder_create(id, "luma-video", settings, nullptr);
    obs_data_release(settings);
    return enc;
}

void Warn(const std::string& text)
{
    if (g_status.warning.empty())
    {
        g_status.warning = text;
    }
    else
    {
        g_status.warning += " ";
        g_status.warning += text;
    }
}

void PlaceItem(obs_source_t* source, int canvasW, int canvasH, double nx, double ny, double nw, double nh)
{
    if (!source || !g_scene)
    {
        return;
    }
    obs_sceneitem_t* item = obs_scene_add(g_scene, source);
    if (!item)
    {
        return;
    }
    struct vec2 pos{};
    pos.x = static_cast<float>(canvasW * nx);
    pos.y = static_cast<float>(canvasH * ny);
    obs_sceneitem_set_pos(item, &pos);
    struct vec2 bounds{};
    bounds.x = static_cast<float>(canvasW * nw < 32 ? 32 : canvasW * nw);
    bounds.y = static_cast<float>(canvasH * nh < 32 ? 32 : canvasH * nh);
    obs_sceneitem_set_bounds_type(item, OBS_BOUNDS_SCALE_INNER);
    obs_sceneitem_set_bounds(item, &bounds);
}

obs_source_t* MakeText(const char* name, const char* text)
{
    obs_data_t* settings = obs_data_create();
    obs_data_set_string(settings, "text", text);
    obs_data_set_int(settings, "color", 0xFFFFFFFF);
    obs_data_t* font = obs_data_create();
    obs_data_set_string(font, "face", "Segoe UI");
    obs_data_set_int(font, "size", 36);
    obs_data_set_obj(settings, "font", font);
    obs_data_release(font);
    obs_source_t* source = CreateInput("text_gdiplus", name, settings);
    if (!source)
    {
        source = CreateInput("text_gdiplus_v2", name, settings);
    }
    if (!source)
    {
        source = CreateInput("text_ft2_source", name, settings);
    }
    obs_data_release(settings);
    if (source)
    {
        g_extra.push_back(source);
    }
    return source;
}

void StampLoop()
{
    while (g_stampRun)
    {
        Sleep(1000);
        if (!g_stampRun || !g_stamp)
        {
            continue;
        }
        std::time_t now = std::time(nullptr);
        std::tm local{};
        localtime_s(&local, &now);
        char text[32]{};
        std::strftime(text, sizeof(text), "%Y-%m-%d %H:%M:%S", &local);
        obs_data_t* settings = obs_data_create();
        obs_data_set_string(settings, "text", text);
        obs_source_update(g_stamp, settings);
        obs_data_release(settings);
    }
}

void AddOverlays(const StartRequest& request, int canvasW, int canvasH)
{
    if (request.camera)
    {
        obs_data_t* settings = obs_data_create();
        if (!request.cameraId.empty() && request.cameraId != "default")
        {
            obs_data_set_string(settings, "video_device_id", request.cameraId.c_str());
        }
        obs_data_set_int(settings, "audio_output_mode", 0);
        obs_source_t* source = CreateInput("dshow_input", "luma-camera", settings);
        obs_data_release(settings);
        if (source)
        {
            g_extra.push_back(source);
            PlaceItem(source, canvasW, canvasH, request.cameraX, request.cameraY, request.cameraW, request.cameraH);
        }
        else
        {
            Warn("摄像头不可用。");
        }
    }

    if (!request.textMark.empty())
    {
        obs_source_t* source = MakeText("luma-text", request.textMark.c_str());
        if (source)
        {
            PlaceItem(source, canvasW, canvasH, request.markX, request.markY, request.markW, request.markH);
        }
        else
        {
            Warn("文字水印不可用。");
        }
    }

    if (!request.imagePath.empty())
    {
        obs_data_t* settings = obs_data_create();
        obs_data_set_string(settings, "file", request.imagePath.c_str());
        obs_source_t* source = CreateInput("image_source", "luma-image", settings);
        obs_data_release(settings);
        if (source)
        {
            g_extra.push_back(source);
            PlaceItem(source, canvasW, canvasH, request.markX, request.markY, request.markW, request.markH);
        }
        else
        {
            Warn("图片水印不可用。");
        }
    }

    if (request.timestamp)
    {
        obs_source_t* source = MakeText("luma-stamp", "--");
        if (source)
        {
            g_stamp = source;
            PlaceItem(source, canvasW, canvasH, request.markX, request.markY + 0.08, 0.28, 0.06);
            g_stampRun = true;
            g_stampThread = std::thread(StampLoop);
        }
        else
        {
            Warn("时间戳不可用。");
        }
    }
}

void FitItem(obs_sceneitem_t* item, int width, int height)
{
    if (!item)
    {
        return;
    }
    struct vec2 bounds{};
    bounds.x = static_cast<float>(width);
    bounds.y = static_cast<float>(height);
    obs_sceneitem_set_bounds_type(item, OBS_BOUNDS_SCALE_INNER);
    obs_sceneitem_set_bounds_alignment(item, OBS_ALIGN_CENTER);
    obs_sceneitem_set_bounds(item, &bounds);
}

void RefreshDurationLocked()
{
    if (g_status.phase == 0 || g_status.phase == 4)
    {
        return;
    }
    if (g_output)
    {
        const int frames = obs_output_get_total_frames(g_output);
        const int dropped = obs_output_get_frames_dropped(g_output);
        if (g_fps > 0 && frames > 0)
        {
            g_status.encodedDurationSeconds = static_cast<double>(frames) / static_cast<double>(g_fps);
        }
        g_status.skippedFrames = dropped;
        g_status.effectiveFps = static_cast<double>(g_fps);
    }
    if (g_status.encodedDurationSeconds <= 0 && g_startTick != 0)
    {
        ULONGLONG now = GetTickCount64();
        double live = (now - g_startTick) / 1000.0 - g_pauseAccum;
        if (g_status.phase == 3 && g_pauseTick != 0)
        {
            live -= (now - g_pauseTick) / 1000.0;
        }
        if (live < 0)
        {
            live = 0;
        }
        g_status.encodedDurationSeconds = live;
    }
}

SessionStatus Fail(const std::string& error)
{
    ReleaseGraph();
    g_status = {};
    g_status.ok = false;
    g_status.error = error;
    LogLine("session failed: %s", error.c_str());
    return g_status;
}

struct OutputStopSignal
{
    HANDLE event = nullptr;
    volatile LONG code = 0;
};

struct StopWatchdog
{
    obs_output_t* output = nullptr;
    volatile LONG stopReturned = 0;
    volatile LONG forceRequested = 0;
    volatile LONG refs = 0;
};

extern "C" static void OutputStopped(void* data, calldata_t* params)
{
    auto* signal = static_cast<OutputStopSignal*>(data);
    InterlockedExchange(&signal->code, static_cast<LONG>(calldata_int(params, "code")));
    if (signal->event)
    {
        SetEvent(signal->event);
    }
}

bool WaitForOutputStop(obs_output_t* output, HANDLE event, DWORD timeoutMs)
{
    const ULONGLONG started = GetTickCount64();
    while (GetTickCount64() - started < timeoutMs)
    {
        if (!obs_output_active(output))
        {
            return true;
        }
        if (event && WaitForSingleObject(event, 100) == WAIT_OBJECT_0)
        {
            return true;
        }
        if (!event)
        {
            Sleep(100);
        }
    }
    return !obs_output_active(output) || (event && WaitForSingleObject(event, 0) == WAIT_OBJECT_0);
}

void ReleaseStopWatchdog(StopWatchdog* watchdog)
{
    if (watchdog && InterlockedDecrement(&watchdog->refs) == 0)
    {
        obs_output_release(watchdog->output);
        delete watchdog;
    }
}

DWORD WINAPI ForceStopWatchdog(LPVOID data)
{
    auto* watchdog = static_cast<StopWatchdog*>(data);
    for (int i = 0; i < 150; ++i)
    {
        if (InterlockedCompareExchange(&watchdog->stopReturned, 0, 0) != 0)
        {
            ReleaseStopWatchdog(watchdog);
            return 0;
        }
        Sleep(100);
    }
    LogLine("stop watchdog: obs_output_stop blocked for 15s; requesting force stop");
    InterlockedExchange(&watchdog->forceRequested, 1);
    obs_output_force_stop(watchdog->output);
    LogLine("stop watchdog: force stop call returned");
    ReleaseStopWatchdog(watchdog);
    return 0;
}

bool DrainOutput(obs_output_t* output, bool* forced)
{
    OutputStopSignal signal{};
    signal.event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    signal_handler_t* handler = obs_output_get_signal_handler(output);
    if (signal.event && handler)
    {
        signal_handler_connect(handler, "stop", OutputStopped, &signal);
    }
    else
    {
        LogLine("stop signal event unavailable; falling back to active polling");
    }

    auto* watchdog = new StopWatchdog();
    watchdog->output = obs_output_get_ref(output);
    watchdog->refs = 2;
    const HANDLE thread = CreateThread(nullptr, 0, ForceStopWatchdog, watchdog, 0, nullptr);
    if (!thread)
    {
        LogLine("stop watchdog: failed to create thread (%lu)", GetLastError());
        ReleaseStopWatchdog(watchdog);
        ReleaseStopWatchdog(watchdog);
        watchdog = nullptr;
    }
    else
    {
        CloseHandle(thread);
    }

    const ULONGLONG started = GetTickCount64();
    LogLine("stop: calling obs_output_stop");
    obs_output_stop(output);
    LogLine("stop: obs_output_stop returned after %llu ms", static_cast<unsigned long long>(GetTickCount64() - started));
    if (watchdog)
    {
        if (InterlockedCompareExchange(&watchdog->forceRequested, 0, 0) != 0 && forced)
        {
            *forced = true;
        }
        InterlockedExchange(&watchdog->stopReturned, 1);
        ReleaseStopWatchdog(watchdog);
    }

    bool stopped = WaitForOutputStop(output, signal.event, 45000);
    if (!stopped)
    {
        LogLine("stop: no stop signal and output remained active for 45s; forcing stop");
        if (forced)
        {
            *forced = true;
        }
        obs_output_force_stop(output);
        const ULONGLONG forceStarted = GetTickCount64();
        stopped = WaitForOutputStop(output, signal.event, 15000);
        LogLine("stop: force-stop wait finished after %llu ms (stopped=%s)",
            static_cast<unsigned long long>(GetTickCount64() - forceStarted), stopped ? "true" : "false");
    }

    if (handler && signal.event)
    {
        signal_handler_disconnect(handler, "stop", OutputStopped, &signal);
    }
    if (signal.event)
    {
        CloseHandle(signal.event);
    }

    const char* lastError = obs_output_get_last_error(output);
    if (lastError && lastError[0])
    {
        LogLine("stop: output reported '%s'; deferring final success to file validation (forced=%s)",
            lastError, forced && *forced ? "true" : "false");
    }
    return stopped;
}

void FinishStop()
{
    obs_output_t* output = nullptr;
    {
        std::lock_guard lock(g_mutex);
        output = g_output;
    }

    bool forced = false;
    const bool stopped = output && DrainOutput(output, &forced);
    if (output)
    {
        Sleep(500);
    }

    std::lock_guard lock(g_mutex);
    if (g_output && g_fps > 0)
    {
        const int frames = obs_output_get_total_frames(g_output);
        if (frames > 0)
        {
            g_status.encodedDurationSeconds = static_cast<double>(frames) / static_cast<double>(g_fps);
        }
    }
    if (g_output && obs_output_active(g_output))
    {
        forced = true;
    }
    ReleaseGraph(false);
    g_startTick = 0;
    g_pauseTick = 0;
    g_status.phase = 0;
    g_status.stopForced = forced;
    if (!stopped)
    {
        g_status.ok = false;
        g_status.error = "OBS output remained active after 45s graceful stop and 15s force-stop wait";
        g_status.warning.clear();
        LogLine("stop failed: output remained active");
    }
    else
    {
        g_status.ok = true;
        g_status.error.clear();
        g_status.warning = forced ? "OBS 正常停止超时，已强制结束；成片已通过校验" : "";
        LogLine("recording stopped seconds=%.2f forced=%s path=%s",
            g_status.encodedDurationSeconds, forced ? "true" : "false", g_status.outputPath.c_str());
    }
    g_stopRunning.store(false);
}

bool EncoderRegistered(const char* wanted)
{
    const char* id = nullptr;
    for (size_t index = 0; obs_enum_encoder_types(index, &id); ++index)
    {
        if (!id || strcmp(id, wanted) != 0)
        {
            continue;
        }
        const uint32_t caps = obs_get_encoder_caps(id);
        const char* codec = obs_get_encoder_codec(id);
        return obs_get_encoder_type(id) == OBS_ENCODER_VIDEO
            && codec
            && strcmp(codec, "h264") == 0
            && (caps & (OBS_ENCODER_CAP_DEPRECATED | OBS_ENCODER_CAP_INTERNAL)) == 0;
    }
    return false;
}

std::string EncoderUnavailableReason(const char* wanted)
{
    if (strcmp(wanted, "obs_qsv11") == 0)
    {
        return "OBS encoder id obs_qsv11 is deprecated; use obs_qsv11_v2";
    }
    if (strncmp(wanted, "obs_qsv11", 9) == 0)
    {
        return "Intel QSV H.264 was not registered; verify an enabled Intel GPU and its media driver";
    }
    if (strcmp(wanted, "jim_nvenc") == 0 || strcmp(wanted, "ffmpeg_nvenc") == 0)
    {
        HMODULE nvenc = LoadLibraryA("nvEncodeAPI64.dll");
        if (!nvenc)
        {
            return "NVIDIA NVENC driver API nvEncodeAPI64.dll is unavailable";
        }
        FreeLibrary(nvenc);
        return "NVIDIA driver API loaded but OBS registered no compatible H.264 NVENC encoder";
    }
    if (strcmp(wanted, "h264_texture_amf") == 0)
    {
        HMODULE amf = LoadLibraryA("amfrt64.dll");
        if (!amf)
        {
            return "AMD AMF driver runtime amfrt64.dll is unavailable";
        }
        FreeLibrary(amf);
        return "AMD AMF runtime loaded but OBS registered no compatible H.264 AMF encoder";
    }
    return std::string("OBS encoder ") + wanted + " was not registered for H.264 on this system";
}

void LogHardwareEncoders()
{
    const char* ids[] = {"obs_qsv11_v2", "h264_texture_amf", "jim_nvenc", "ffmpeg_nvenc", "obs_qsv11"};
    for (const char* id : ids)
    {
        if (EncoderRegistered(id))
        {
            LogLine("encoder %s available", id);
        }
        else
        {
            LogLine("encoder %s unavailable: %s", id, EncoderUnavailableReason(id).c_str());
        }
    }
}
} // namespace

bool ObsInit(std::string& error)
{
    std::lock_guard lock(g_mutex);
    if (g_ready)
    {
        return true;
    }

    OpenLog();
    const std::string host = HostDirUtf8();
    LogLine("host dir %s", host.c_str());

    const std::wstring hostW = Utf8ToWide(host);
    SetCurrentDirectoryW(hostW.c_str());
    SetDllDirectoryW(hostW.c_str());

    base_set_log_handler(ObsLogHandler, nullptr);

    wchar_t appdata[MAX_PATH]{};
    GetEnvironmentVariableW(L"LOCALAPPDATA", appdata, MAX_PATH);
    std::string config = WideToUtf8(appdata) + "/Luma/obs-config";
    CreateDirectoryW((std::wstring(appdata) + L"\\Luma\\obs-config").c_str(), nullptr);

    if (!obs_startup("en-US", config.c_str(), nullptr))
    {
        error = "obs_startup 失败。";
        LogLine("%s", error.c_str());
        return false;
    }

    const std::string pluginBin = host + "/obs-plugins/64bit";
    const std::string pluginData = host + "/data/obs-plugins/%module%/";
    const std::string libobsData = host + "/data/libobs/";
    LogLine("data path %s", libobsData.c_str());
    obs_add_data_path(libobsData.c_str());
    obs_add_module_path((pluginBin + "/").c_str(), pluginData.c_str());

    // win-capture reads gs_get_device_type() while it loads. Without a D3D11
    // device it marks WGC unsupported and window capture falls back to BitBlt,
    // which only keeps the cursor on GPU-composited windows.
    if (!ResetVideoAudio(1920, 1080, 1920, 1080, 30, error))
    {
        obs_shutdown();
        return false;
    }

    obs_load_all_modules();
    obs_log_loaded_modules();
    obs_post_load_modules();
    LogHardwareEncoders();

    g_ready = true;
    g_status.encoderName = "idle";
    LogLine("libobs ready");
    return true;
}

void ObsShutdown()
{
    std::lock_guard lock(g_mutex);
    ReleaseGraph();
    if (g_ready)
    {
        obs_shutdown();
        g_ready = false;
        g_videoReady = false;
    }
    g_log.close();
}

static bool HasContainerExtension(std::string path, const std::string& extension)
{
    for (char& ch : path)
    {
        if (ch >= 'A' && ch <= 'Z')
        {
            ch = static_cast<char>(ch - 'A' + 'a');
        }
    }

    return path.size() >= extension.size()
        && path.compare(path.size() - extension.size(), extension.size(), extension) == 0;
}

bool ObsReady()
{
    std::lock_guard lock(g_mutex);
    return g_ready;
}

SessionStatus ObsStart(const StartRequest& request)
{
    std::lock_guard lock(g_mutex);
    if (!g_ready)
    {
        return Fail("libobs 尚未初始化。");
    }
    if (g_status.phase == 4)
    {
        SessionStatus denied = g_status;
        denied.ok = false;
        denied.error = "录制正在结束。";
        return denied;
    }
    if (g_output && obs_output_active(g_output))
    {
        return Fail("已有录制正在进行。");
    }

    ReleaseGraph();
    g_status = {};
    g_status.ok = true;
    g_pauseAccum = 0;
    g_pauseTick = 0;

    if (request.outputPath.empty())
    {
        return Fail("缺少输出路径。");
    }

    std::string livePath = request.outputPath;
    if (HasContainerExtension(livePath, ".mp4") || HasContainerExtension(livePath, ".mov"))
    {
        livePath += ".partial.mkv";
    }
    const std::wstring outputWide = Utf8ToWide(livePath);
    if (const wchar_t* slash = wcsrchr(outputWide.c_str(), L'\\'))
    {
        std::wstring dir(outputWide.c_str(), slash);
        CreateDirectoryW(dir.c_str(), nullptr);
    }

    int monitorW = 1920;
    int monitorH = 1080;
    std::string monitorId;
    GetMonitorSize(request.monitorIndex, monitorW, monitorH, monitorId);

    int canvasW = monitorW;
    int canvasH = monitorH;
    if (request.mode == "Region" && request.cropWidth > 0 && request.cropHeight > 0)
    {
        canvasW = request.cropWidth;
        canvasH = request.cropHeight;
    }
    if (request.mode == "AudioOnly")
    {
        canvasW = 1280;
        canvasH = 720;
    }

    int encW = request.width > 0 ? request.width : canvasW;
    int encH = request.height > 0 ? request.height : canvasH;
    if (encW > canvasW)
    {
        encW = canvasW;
    }
    if (encH > canvasH)
    {
        encH = canvasH;
    }

    std::string error;
    if (!ResetVideoAudio(canvasW, canvasH, encW, encH, request.fps, error))
    {
        return Fail(error);
    }

    g_scene = obs_scene_create("luma");
    if (!g_scene)
    {
        return Fail("无法创建内部 scene。");
    }

    const std::string mode = request.mode.empty() ? "Display" : request.mode;
    if (mode == "AudioOnly")
    {
        obs_data_t* color = obs_data_create();
        obs_data_set_int(color, "width", canvasW);
        obs_data_set_int(color, "height", canvasH);
        obs_data_set_int(color, "color", 0xFF101010);
        g_videoSource = CreateInput("color_source", "luma-color", color);
        obs_data_release(color);
    }
    else if (mode == "Game")
    {
        return Fail("游戏录制已关闭。");
    }
    else if (mode == "Window")
    {
        if (request.windowId.empty() || request.windowId == "pending")
        {
            return Fail("请先选择窗口。");
        }
        obs_data_t* settings = obs_data_create();
        obs_data_set_string(settings, "window", request.windowId.c_str());
        obs_data_set_bool(settings, "capture_cursor", true);
        obs_data_set_int(settings, "priority", 2);
        obs_data_set_int(settings, "method", 2);
        obs_data_set_int(settings, "capture_mode", 0);
        g_videoSource = CreateInput("window_capture", "luma-video", settings);
        obs_data_release(settings);
    }
    else
    {
        obs_data_t* settings = obs_data_create();
        obs_data_set_int(settings, "monitor", request.monitorIndex);
        if (!monitorId.empty())
        {
            obs_data_set_string(settings, "monitor_id", monitorId.c_str());
        }
        obs_data_set_bool(settings, "capture_cursor", true);
        obs_data_set_int(settings, "method", ChooseDisplayCaptureMethod());
        obs_data_set_bool(settings, "force_sdr", true);
        g_videoSource = CreateInput("monitor_capture", "luma-display", settings);
        obs_data_release(settings);
        if (mode == "Region" && request.cropWidth > 0 && request.cropHeight > 0)
        {
            // Crop is applied after the item exists.
        }
    }

    if (!g_videoSource)
    {
        return Fail("无法创建采集源。");
    }

    obs_sceneitem_t* item = obs_scene_add(g_scene, g_videoSource);
    FitItem(item, canvasW, canvasH);
    if (mode == "Region" && request.cropWidth > 0 && request.cropHeight > 0 && item)
    {
        obs_sceneitem_crop crop{};
        crop.left = request.cropX;
        crop.top = request.cropY;
        crop.right = (monitorW - request.cropX - request.cropWidth);
        crop.bottom = (monitorH - request.cropY - request.cropHeight);
        if (crop.right < 0)
        {
            crop.right = 0;
        }
        if (crop.bottom < 0)
        {
            crop.bottom = 0;
        }
        obs_sceneitem_set_crop(item, &crop);
    }

    if (request.systemAudio)
    {
        obs_data_t* settings = obs_data_create();
        obs_data_set_string(settings, "device_id", "default");
        g_systemAudio = CreateInput("wasapi_output_capture", "luma-system-audio", settings);
        obs_data_release(settings);
        if (g_systemAudio)
        {
            obs_scene_add(g_scene, g_systemAudio);
        }
        else
        {
            g_status.warning = "系统声采集不可用。";
        }
    }

    if (request.microphone)
    {
        obs_data_t* settings = obs_data_create();
        obs_data_set_string(settings, "device_id", request.micId.empty() ? "default" : request.micId.c_str());
        g_mic = CreateInput("wasapi_input_capture", "luma-mic", settings);
        obs_data_release(settings);
        if (g_mic)
        {
            obs_scene_add(g_scene, g_mic);
        }
        else if (g_status.warning.empty())
        {
            g_status.warning = "麦克风不可用。";
        }
        else
        {
            g_status.warning += " 麦克风不可用。";
        }
    }

    if (mode == "Game")
    {
        uint32_t captured = 0;
        for (int i = 0; i < 12 && captured == 0; i++)
        {
            Sleep(200);
            captured = obs_source_get_width(g_videoSource);
        }
        if (captured == 0)
        {
            return Fail("游戏采集没有画面。若游戏是独占全屏，请改成无边框窗口后再试。");
        }
    }

    AddOverlays(request, canvasW, canvasH);

    obs_source_t* sceneSource = obs_scene_get_source(g_scene);
    obs_set_output_source(0, sceneSource);

    std::string encoderId;
    bool usedHardware = false;
    std::vector<const char*> encoderIds;
    std::string hardwareFailure;
    if (request.hardware)
    {
        const char* hardwareIds[] = {"h264_texture_amf", "jim_nvenc", "ffmpeg_nvenc", "obs_qsv11_v2"};
        for (const char* id : hardwareIds)
        {
            if (EncoderRegistered(id))
            {
                encoderIds.push_back(id);
            }
            else
            {
                if (!hardwareFailure.empty())
                {
                    hardwareFailure += " ";
                }
                hardwareFailure += std::string(id) + ": " + EncoderUnavailableReason(id);
            }
        }
    }
    encoderIds.push_back("obs_x264");

    auto tryStart = [&](const char* id) -> bool {
        if (g_venc)
        {
            obs_encoder_release(g_venc);
            g_venc = nullptr;
        }
        if (g_aenc)
        {
            obs_encoder_release(g_aenc);
            g_aenc = nullptr;
        }
        if (g_output)
        {
            obs_output_release(g_output);
            g_output = nullptr;
        }

        if (id && id[0])
        {
            g_venc = CreateVideoEncoderById(id, request);
            if (!g_venc)
            {
                error = std::string("无法创建编码器 ") + id;
                LogLine("%s", error.c_str());
                return false;
            }
            encoderId = id;
            usedHardware = strcmp(id, "obs_x264") != 0;
            obs_encoder_set_video(g_venc, obs_get_video());
        }
        else
        {
            encoderId = "aac";
            usedHardware = false;
        }

        obs_data_t* aac = obs_data_create();
        obs_data_set_int(aac, "bitrate", 160);
        g_aenc = obs_audio_encoder_create("ffmpeg_aac", "luma-audio", aac, 0, nullptr);
        obs_data_release(aac);
        if (!g_aenc)
        {
            error = "无法创建 AAC 编码器。";
            return false;
        }
        obs_encoder_set_audio(g_aenc, obs_get_audio());

        obs_data_t* mux = obs_data_create();
        obs_data_set_string(mux, "path", livePath.c_str());
        obs_data_set_string(mux, "directory", "");
        g_output = obs_output_create("ffmpeg_muxer", "luma-file", mux, nullptr);
        if (!g_output)
        {
            g_output = obs_output_create("mp4_output", "luma-file", mux, nullptr);
        }
        obs_data_release(mux);
        if (!g_output)
        {
            error = "无法创建 ffmpeg_muxer / mp4_output。";
            return false;
        }

        if (g_venc)
        {
            obs_output_set_video_encoder(g_output, g_venc);
        }
        obs_output_set_audio_encoder(g_output, g_aenc, 0);
        const char* label = id && id[0] ? id : "aac";
        if (!obs_output_start(g_output))
        {
            const char* last = obs_output_get_last_error(g_output);
            error = last && last[0] ? last : (std::string(label) + " 启动失败");
            LogLine("output start failed (%s): %s", label, error.c_str());
            return false;
        }

        for (int i = 0; i < 40 && !obs_output_active(g_output); i++)
        {
            Sleep(50);
        }
        if (!obs_output_active(g_output))
        {
            const char* last = obs_output_get_last_error(g_output);
            error = last && last[0] ? last : "输出没有进入活动状态。";
            return false;
        }
        LogLine("using encoder %s", encoderId.c_str());
        return true;
    };

    bool started = false;
    for (const char* id : encoderIds)
    {
        if (tryStart(id))
        {
            started = true;
            if (strcmp(id, "obs_x264") == 0 && request.hardware)
            {
                if (hardwareFailure.empty())
                {
                    hardwareFailure = "no hardware encoder produced an active output";
                }
                Warn("硬件编码不可用，已回退 obs_x264。" + hardwareFailure);
                LogLine("encoder fallback obs_x264: %s", hardwareFailure.c_str());
            }
            break;
        }
        if (strcmp(id, "obs_x264") != 0)
        {
            const std::string failed = std::string(id) + " failed: " + error;
            hardwareFailure = hardwareFailure.empty() ? failed : failed + " " + hardwareFailure;
            LogLine("encoder %s failed: %s", id, error.c_str());
        }
    }
    if (!started)
    {
        return Fail(error.empty() ? "无法开始输出。" : error);
    }

    g_startTick = GetTickCount64();
    g_status.ok = true;
    g_status.phase = 2;
    g_status.encoderName = encoderId;
    const char* displayName = obs_encoder_get_display_name(encoderId.c_str());
    g_status.usedHardware = usedHardware && encoderId != "obs_x264";
    g_status.width = encW;
    g_status.height = encH;
    g_status.effectiveFps = request.fps;
    g_status.outputPath = livePath;
    g_status.encodedDurationSeconds = 0;
    LogLine("recording started encoder=%s display=%s hardware=%s path=%s",
        encoderId.c_str(),
        displayName && displayName[0] ? displayName : encoderId.c_str(),
        g_status.usedHardware ? "true" : "false",
        request.outputPath.c_str());
    return g_status;
}

SessionStatus ObsPause(bool pause)
{
    std::lock_guard lock(g_mutex);
    if (g_status.phase == 4)
    {
        SessionStatus denied = g_status;
        denied.ok = false;
        denied.error = "录制正在结束。";
        return denied;
    }
    if (!g_output || !obs_output_active(g_output))
    {
        g_status.ok = false;
        g_status.error = "当前没有正在进行的录制。";
        return g_status;
    }
    if (!obs_output_can_pause(g_output))
    {
        g_status.ok = false;
        g_status.error = "当前输出不支持暂停。";
        return g_status;
    }
    if (!obs_output_pause(g_output, pause))
    {
        g_status.ok = false;
        g_status.error = pause ? "暂停失败。" : "恢复失败。";
        return g_status;
    }
    if (pause)
    {
        g_pauseTick = GetTickCount64();
        g_status.phase = 3;
    }
    else
    {
        if (g_pauseTick != 0)
        {
            g_pauseAccum += (GetTickCount64() - g_pauseTick) / 1000.0;
            g_pauseTick = 0;
        }
        g_status.phase = 2;
    }
    g_status.ok = true;
    g_status.error.clear();
    RefreshDurationLocked();
    return g_status;
}

SessionStatus ObsMute(bool muted)
{
    std::lock_guard lock(g_mutex);
    if (g_status.phase == 4)
    {
        SessionStatus denied = g_status;
        denied.ok = false;
        denied.error = "录制正在结束。";
        return denied;
    }
    if (!g_mic)
    {
        g_status.ok = true;
        g_status.warning = "当前没有麦克风轨道。";
        return g_status;
    }
    obs_source_set_muted(g_mic, muted);
    g_status.ok = true;
    g_status.error.clear();
    return g_status;
}

SessionStatus ObsStop()
{
    bool spawn = false;
    SessionStatus snapshot;
    {
        std::lock_guard lock(g_mutex);
        if (g_status.phase == 4)
        {
            g_status.ok = true;
            return g_status;
        }
        if (!g_output)
        {
            g_status.ok = false;
            g_status.error = "当前没有正在进行的录制。";
            g_status.phase = 0;
            return g_status;
        }

        RefreshDurationLocked();
        g_status.phase = 4;
        g_status.ok = true;
        g_status.error.clear();
        g_status.stopForced = false;
        g_status.warning = "正在结束 OBS 输出并校验文件…";
        spawn = !g_stopRunning.exchange(true);
        snapshot = g_status;
    }
    if (spawn)
    {
        if (g_stopThread.joinable())
        {
            g_stopThread.join();
        }
        g_stopThread = std::thread(FinishStop);
    }
    return snapshot;
}

bool ObsWaitStop(unsigned long timeoutMs)
{
    const ULONGLONG started = GetTickCount64();
    while (GetTickCount64() - started < timeoutMs)
    {
        if (ObsStatus().phase != 4)
        {
            return ObsStatus().ok;
        }
        Sleep(100);
    }
    return false;
}

void ObsJoinStop()
{
    if (g_stopRunning.load())
    {
        if (g_stopThread.joinable())
        {
            g_stopThread.detach();
        }
        return;
    }
    if (g_stopThread.joinable())
    {
        g_stopThread.join();
    }
}

SessionStatus ObsStatus()
{
    std::lock_guard lock(g_mutex);
    if (g_status.phase == 4)
    {
        g_status.ok = true;
        return g_status;
    }
    RefreshDurationLocked();
    if (g_status.phase != 0)
    {
        g_status.ok = true;
    }
    if (g_output && obs_output_active(g_output) && g_status.phase == 0)
    {
        g_status.phase = obs_output_paused(g_output) ? 3 : 2;
        g_status.ok = true;
    }
    return g_status;
}

std::string StatusToJson(const SessionStatus& status)
{
    std::ostringstream oss;
    oss << "{\"ok\":" << (status.ok ? "true" : "false")
        << ",\"phase\":" << status.phase
        << ",\"encoderName\":\"" << JsonEscape(status.encoderName) << "\""
        << ",\"usedHardware\":" << (status.usedHardware ? "true" : "false")
        << ",\"width\":" << status.width
        << ",\"height\":" << status.height
        << ",\"effectiveFps\":" << status.effectiveFps
        << ",\"skippedFrames\":" << status.skippedFrames
        << ",\"encodedDurationSeconds\":" << status.encodedDurationSeconds
        << ",\"outputPath\":\"" << JsonEscape(status.outputPath) << "\""
        << ",\"error\":\"" << JsonEscape(status.error) << "\""
        << ",\"warning\":\"" << JsonEscape(status.warning) << "\""
        << ",\"stopForced\":" << (status.stopForced ? "true" : "false") << "}";
    return oss.str();
}

StartRequest ParseStartRequest(const std::string& json)
{
    StartRequest request;
    request.outputPath = JsonString(json, "outputPath");
    request.mode = JsonString(json, "mode", "Display");
    request.monitorIndex = static_cast<int>(JsonInt(json, "monitorIndex", 0));
    request.windowId = JsonString(json, "windowId");
    request.cropX = static_cast<int>(JsonInt(json, "cropX", 0));
    request.cropY = static_cast<int>(JsonInt(json, "cropY", 0));
    request.cropWidth = static_cast<int>(JsonInt(json, "cropWidth", 0));
    request.cropHeight = static_cast<int>(JsonInt(json, "cropHeight", 0));
    request.width = static_cast<int>(JsonInt(json, "width", 1920));
    request.height = static_cast<int>(JsonInt(json, "height", 1080));
    request.fps = static_cast<int>(JsonInt(json, "fps", 30));
    request.bitrateKbps = static_cast<int>(JsonInt(json, "bitrateKbps", 8000));
    request.hardware = JsonBool(json, "hardware", true);
    request.systemAudio = JsonBool(json, "systemAudio", true);
    request.microphone = JsonBool(json, "microphone", false);
    request.micId = JsonString(json, "micId");
    request.camera = JsonBool(json, "camera", false);
    request.cameraId = JsonString(json, "cameraId");
    request.cameraX = JsonDouble(json, "cameraX", 0.5);
    request.cameraY = JsonDouble(json, "cameraY", 0.5);
    request.cameraW = JsonDouble(json, "cameraW", 0.24);
    request.cameraH = JsonDouble(json, "cameraH", 0.24);
    request.textMark = JsonString(json, "textMark");
    request.textOpacity = JsonDouble(json, "textOpacity", 1);
    request.imagePath = JsonString(json, "imagePath");
    request.imageOpacity = JsonDouble(json, "imageOpacity", 0.9);
    request.timestamp = JsonBool(json, "timestamp", false);
    request.markX = JsonDouble(json, "markX", 0.02);
    request.markY = JsonDouble(json, "markY", 0.02);
    request.markW = JsonDouble(json, "markW", 0.2);
    request.markH = JsonDouble(json, "markH", 0.08);
    return request;
}
