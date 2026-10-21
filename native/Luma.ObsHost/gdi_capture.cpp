#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "gdi_capture.h"

#include <obs.h>
#include <util/base.h>
#include <util/platform.h>

#include <windows.h>

#include <atomic>
#include <string>
#include <thread>

const char* const kGdiDisplayCaptureId = "luma_gdi_display_capture";

namespace
{
struct GdiCapture
{
    obs_source_t* source = nullptr;
    std::wstring device;
    int fps = 30;
    bool cursor = true;
    std::atomic<bool> run{false};
    std::thread thread;
};

struct ScreenRect
{
    int x = 0;
    int y = 0;
    int width = 0;
    int height = 0;
};

std::wstring Utf8ToWide(const char* utf8)
{
    if (!utf8 || !utf8[0])
    {
        return {};
    }
    const int n = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
    if (n <= 1)
    {
        return {};
    }
    std::wstring out(static_cast<size_t>(n - 1), 0);
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, out.data(), n);
    return out;
}

// The host is only system-DPI aware. Per-monitor awareness on the capture
// thread keeps BitBlt and cursor coordinates in physical pixels on mixed-DPI
// setups. The API is missing before Windows 10 1607, hence the lookup.
void UsePhysicalPixels()
{
    using SetContext = DPI_AWARENESS_CONTEXT(WINAPI*)(DPI_AWARENESS_CONTEXT);
    if (HMODULE user32 = GetModuleHandleW(L"user32.dll"))
    {
        if (auto set = reinterpret_cast<SetContext>(GetProcAddress(user32, "SetThreadDpiAwarenessContext")))
        {
            set(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
    }
}

ScreenRect ReadScreenRect(const std::wstring& device)
{
    ScreenRect rect;
    DEVMODEW mode{};
    mode.dmSize = sizeof(mode);
    if (EnumDisplaySettingsW(device.empty() ? nullptr : device.c_str(), ENUM_CURRENT_SETTINGS, &mode))
    {
        rect.width = static_cast<int>(mode.dmPelsWidth);
        rect.height = static_cast<int>(mode.dmPelsHeight);
        if (!device.empty())
        {
            rect.x = mode.dmPosition.x;
            rect.y = mode.dmPosition.y;
        }
    }
    if (rect.width <= 0 || rect.height <= 0)
    {
        rect.width = GetSystemMetrics(SM_CXSCREEN);
        rect.height = GetSystemMetrics(SM_CYSCREEN);
    }
    return rect;
}

void DrawCursor(HDC target, const ScreenRect& rect)
{
    CURSORINFO info{};
    info.cbSize = sizeof(info);
    if (!GetCursorInfo(&info) || !(info.flags & CURSOR_SHOWING) || !info.hCursor)
    {
        return;
    }

    ICONINFO icon{};
    if (!GetIconInfo(info.hCursor, &icon))
    {
        return;
    }
    const int x = info.ptScreenPos.x - rect.x - static_cast<int>(icon.xHotspot);
    const int y = info.ptScreenPos.y - rect.y - static_cast<int>(icon.yHotspot);
    DrawIconEx(target, x, y, info.hCursor, 0, 0, 0, nullptr, DI_NORMAL);
    if (icon.hbmMask)
    {
        DeleteObject(icon.hbmMask);
    }
    if (icon.hbmColor)
    {
        DeleteObject(icon.hbmColor);
    }
}

void CaptureLoop(GdiCapture* capture)
{
    UsePhysicalPixels();

    HDC screen = nullptr;
    HDC memory = nullptr;
    HBITMAP bitmap = nullptr;
    HGDIOBJ previous = nullptr;
    void* bits = nullptr;
    int width = 0;
    int height = 0;
    bool failing = false;

    const uint64_t interval = 1000000000ULL / static_cast<uint64_t>(capture->fps);
    uint64_t next = os_gettime_ns();

    while (capture->run)
    {
        if (!screen)
        {
            screen = GetDC(nullptr);
            memory = screen ? CreateCompatibleDC(screen) : nullptr;
        }

        const ScreenRect rect = ReadScreenRect(capture->device);
        if (memory && (rect.width != width || rect.height != height || !bitmap))
        {
            if (bitmap)
            {
                SelectObject(memory, previous);
                DeleteObject(bitmap);
                bitmap = nullptr;
                bits = nullptr;
            }
            BITMAPINFO info{};
            info.bmiHeader.biSize = sizeof(info.bmiHeader);
            info.bmiHeader.biWidth = rect.width;
            info.bmiHeader.biHeight = -rect.height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = BI_RGB;
            bitmap = CreateDIBSection(memory, &info, DIB_RGB_COLORS, &bits, nullptr, 0);
            if (bitmap)
            {
                previous = SelectObject(memory, bitmap);
                width = rect.width;
                height = rect.height;
            }
        }

        // CAPTUREBLT includes layered windows such as the click highlight and the pen layer.
        if (bits && BitBlt(memory, 0, 0, width, height, screen, rect.x, rect.y, SRCCOPY | CAPTUREBLT))
        {
            if (failing)
            {
                blog(LOG_INFO, "[gdi-display-capture] BitBlt recovered");
                failing = false;
            }
            if (capture->cursor)
            {
                DrawCursor(memory, rect);
            }
            GdiFlush();

            obs_source_frame frame{};
            frame.data[0] = static_cast<uint8_t*>(bits);
            frame.linesize[0] = static_cast<uint32_t>(width) * 4;
            frame.width = static_cast<uint32_t>(width);
            frame.height = static_cast<uint32_t>(height);
            frame.format = VIDEO_FORMAT_BGRX;
            frame.full_range = true;
            frame.timestamp = os_gettime_ns();
            obs_source_output_video(capture->source, &frame);
        }
        else
        {
            // The screen DC goes stale when the input desktop switches (lock screen, UAC);
            // the last frame stays on screen until a fresh DC works again.
            if (!failing)
            {
                blog(LOG_WARNING, "[gdi-display-capture] BitBlt failed: %lu", GetLastError());
                failing = true;
            }
            if (memory)
            {
                if (bitmap)
                {
                    SelectObject(memory, previous);
                    DeleteObject(bitmap);
                    bitmap = nullptr;
                    bits = nullptr;
                }
                DeleteDC(memory);
                memory = nullptr;
            }
            if (screen)
            {
                ReleaseDC(nullptr, screen);
                screen = nullptr;
            }
        }

        next += interval;
        const uint64_t now = os_gettime_ns();
        if (next > now)
        {
            os_sleepto_ns(next);
        }
        else
        {
            next = now;
        }
    }

    if (bitmap)
    {
        SelectObject(memory, previous);
        DeleteObject(bitmap);
    }
    if (memory)
    {
        DeleteDC(memory);
    }
    if (screen)
    {
        ReleaseDC(nullptr, screen);
    }
}

const char* GdiGetName(void*)
{
    return "Luma GDI Display Capture";
}

void* GdiCreate(obs_data_t* settings, obs_source_t* source)
{
    auto* capture = new GdiCapture();
    capture->source = source;
    capture->device = Utf8ToWide(obs_data_get_string(settings, "device"));
    const long long fps = obs_data_get_int(settings, "fps");
    capture->fps = fps < 1 ? 30 : (fps > 60 ? 60 : static_cast<int>(fps));
    capture->cursor = obs_data_get_bool(settings, "capture_cursor");

    // Show each grab as soon as it arrives instead of buffering by timestamp.
    obs_source_set_async_unbuffered(source, true);

    const ScreenRect rect = ReadScreenRect(capture->device);
    blog(LOG_INFO, "[gdi-display-capture] device=%s %dx%d at (%d,%d) fps=%d cursor=%s",
        obs_data_get_string(settings, "device"), rect.width, rect.height, rect.x, rect.y,
        capture->fps, capture->cursor ? "true" : "false");

    capture->run = true;
    capture->thread = std::thread(CaptureLoop, capture);
    return capture;
}

void GdiDestroy(void* data)
{
    auto* capture = static_cast<GdiCapture*>(data);
    capture->run = false;
    if (capture->thread.joinable())
    {
        capture->thread.join();
    }
    delete capture;
}
} // namespace

void RegisterGdiDisplayCapture()
{
    obs_source_info info{};
    info.id = kGdiDisplayCaptureId;
    info.type = OBS_SOURCE_TYPE_INPUT;
    info.output_flags = OBS_SOURCE_ASYNC_VIDEO | OBS_SOURCE_DO_NOT_DUPLICATE;
    info.get_name = GdiGetName;
    info.create = GdiCreate;
    info.destroy = GdiDestroy;
    obs_register_source(&info);
}
