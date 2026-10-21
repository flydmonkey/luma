#pragma once

#include <string>

struct StartRequest
{
    std::string outputPath;
    std::string posterPath;
    std::string mode;
    int monitorIndex = 0;
    std::string windowId;
    int cropX = 0;
    int cropY = 0;
    int cropWidth = 0;
    int cropHeight = 0;
    int width = 1920;
    int height = 1080;
    int fps = 30;
    int bitrateKbps = 8000;
    int quality = 0;
    bool hardware = true;
    bool systemAudio = true;
    bool microphone = false;
    std::string micId;
    double systemVolume = 1;
    double micVolume = 1;
    bool noiseSuppression = false;
    bool separateTracks = true;
    bool showCursor = true;
    // "auto", "dxgi", "wgc" or "gdi".
    std::string captureMethod = "auto";
    bool camera = false;
    std::string cameraId;
    std::string cameraName;
    double cameraX = 0.5;
    double cameraY = 0.5;
    double cameraW = 0.24;
    double cameraH = 0.24;
    std::string textMark;
    double textOpacity = 1;
    std::string imagePath;
    double imageOpacity = 0.9;
    bool timestamp = false;
    double markX = 0.02;
    double markY = 0.02;
    double markW = 0.2;
    double markH = 0.08;
};

struct SessionStatus
{
    bool ok = true;
    int phase = 0;
    std::string encoderName;
    bool usedHardware = false;
    int width = 0;
    int height = 0;
    double effectiveFps = 0;
    long long skippedFrames = 0;
    double encodedDurationSeconds = 0;
    std::string outputPath;
    std::string error;
    std::string warning;
    bool stopForced = false;
    bool outputLost = false;
    std::string lostReason;
    double systemLevel = 0;
    double micLevel = 0;
};

bool ObsInit(std::string& error);
void ObsShutdown();
bool ObsReady();
SessionStatus ObsStart(const StartRequest& request);
SessionStatus ObsPause(bool pause);
SessionStatus ObsMute(bool muted);
SessionStatus ObsVolume(double system, double mic);
SessionStatus ObsStop();
bool ObsWaitStop(unsigned long timeoutMs);
void ObsJoinStop();
SessionStatus ObsStatus();
std::string StatusToJson(const SessionStatus& status);
StartRequest ParseStartRequest(const std::string& json);
