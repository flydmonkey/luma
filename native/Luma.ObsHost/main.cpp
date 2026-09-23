#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "json_util.h"
#include "obs_session.h"

#include <windows.h>

#include <objbase.h>

#include <string>

static constexpr wchar_t kPipeName[] = L"\\\\.\\pipe\\luma-obs-host";

static bool WriteLine(HANDLE pipe, const std::string& line)
{
    std::string payload = line;
    payload.push_back('\n');
    DWORD written = 0;
    return WriteFile(pipe, payload.data(), static_cast<DWORD>(payload.size()), &written, nullptr) == TRUE;
}

static bool ReadLine(HANDLE pipe, std::string& line)
{
    line.clear();
    char ch = 0;
    DWORD read = 0;
    while (ReadFile(pipe, &ch, 1, &read, nullptr) && read == 1)
    {
        if (ch == '\n')
        {
            if (!line.empty() && line.back() == '\r')
            {
                line.pop_back();
            }
            return true;
        }
        line.push_back(ch);
        if (line.size() > 1024 * 1024)
        {
            return false;
        }
    }
    return !line.empty();
}

static SessionStatus Handle(const std::string& request)
{
    const std::string op = JsonString(request, "op");
    if (op == "start")
    {
        return ObsStart(ParseStartRequest(request));
    }
    if (op == "pause")
    {
        return ObsPause(true);
    }
    if (op == "resume")
    {
        return ObsPause(false);
    }
    if (op == "stop")
    {
        return ObsStop();
    }
    if (op == "status")
    {
        return ObsStatus();
    }
    if (op == "mute")
    {
        return ObsMute(JsonBool(request, "muted", true));
    }

    SessionStatus status = ObsStatus();
    status.ok = false;
    status.error = "未知操作。";
    return status;
}

static int RunSelfTest(const std::wstring& pathW)
{
    std::string error;
    if (!ObsInit(error))
    {
        fprintf(stderr, "init failed: %s\n", error.c_str());
        return 10;
    }

    StartRequest request;
    request.outputPath = "";
    {
        char pathA[MAX_PATH * 4]{};
        WideCharToMultiByte(CP_UTF8, 0, pathW.c_str(), -1, pathA, sizeof(pathA), nullptr, nullptr);
        request.outputPath = pathA;
    }
    request.mode = "Display";
    request.width = 1280;
    request.height = 720;
    request.fps = 30;
    request.bitrateKbps = 4000;
    request.hardware = true;
    request.systemAudio = false;

    SessionStatus started = ObsStart(request);
    if (!started.ok)
    {
        fprintf(stderr, "start failed: %s\n", started.error.c_str());
        ObsShutdown();
        return 11;
    }

    Sleep(2500);
    SessionStatus stopped = ObsStop();
    if (stopped.phase == 4 && !ObsWaitStop(120000))
    {
        fprintf(stderr, "stop timed out\n");
        ObsJoinStop();
        ObsShutdown();
        return 13;
    }
    stopped = ObsStatus();
    ObsJoinStop();
    ObsShutdown();

    const DWORD attr = GetFileAttributesW(pathW.c_str());
    WIN32_FILE_ATTRIBUTE_DATA info{};
    const bool exists = GetFileAttributesExW(pathW.c_str(), GetFileExInfoStandard, &info) == TRUE;
    const ULONGLONG size = exists
        ? (static_cast<ULONGLONG>(info.nFileSizeHigh) << 32) | info.nFileSizeLow
        : 0;
    if (!exists || size < 1024 || attr == INVALID_FILE_ATTRIBUTES)
    {
        fprintf(stderr, "output missing or too small: %llu\n", size);
        return 12;
    }

    printf("encoder=%s hardware=%s seconds=%.2f bytes=%llu path=%ls\n",
        stopped.encoderName.c_str(),
        stopped.usedHardware ? "true" : "false",
        stopped.encodedDurationSeconds,
        size,
        pathW.c_str());
    return 0;
}

static int RunPipe()
{
    std::string error;
    if (!ObsInit(error))
    {
        fprintf(stderr, "init failed: %s\n", error.c_str());
        return 3;
    }

    HANDLE pipe = CreateNamedPipeW(
        kPipeName,
        PIPE_ACCESS_DUPLEX,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1,
        64 * 1024,
        64 * 1024,
        0,
        nullptr);
    if (pipe == INVALID_HANDLE_VALUE)
    {
        ObsShutdown();
        return 1;
    }

    if (!ConnectNamedPipe(pipe, nullptr) && GetLastError() != ERROR_PIPE_CONNECTED)
    {
        CloseHandle(pipe);
        ObsShutdown();
        return 2;
    }

    std::string request;
    while (ReadLine(pipe, request))
    {
        SessionStatus status = Handle(request);
        if (!WriteLine(pipe, StatusToJson(status)))
        {
            break;
        }
    }

    if (ObsStatus().phase != 0)
    {
        ObsStop();
        ObsWaitStop(120000);
    }
    ObsJoinStop();
    DisconnectNamedPipe(pipe);
    CloseHandle(pipe);
    ObsShutdown();
    return 0;
}

int wmain(int argc, wchar_t** argv)
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    SetProcessDPIAware();

    int code = 0;
    if (argc >= 2 && wcscmp(argv[1], L"--self-test") == 0)
    {
        wchar_t temp[MAX_PATH]{};
        GetTempPathW(MAX_PATH, temp);
        std::wstring path = argc >= 3 ? argv[2] : (std::wstring(temp) + L"luma-self-test.mp4");
        DeleteFileW(path.c_str());
        code = RunSelfTest(path);
    }
    else
    {
        code = RunPipe();
    }

    CoUninitialize();
    return code;
}
