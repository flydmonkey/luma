#pragma once

#include <string>

struct StartRequest
{
    std::string outputPath;
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
    bool hardware = true;
    bool systemAudio = true;
    bool microphone = false;
    std::string micId;
    bool camera = false;
    std::string cameraId;
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
};

bool ObsInit(std::string& error);
void ObsShutdown();
bool ObsReady();
SessionStatus ObsStart(const StartRequest& request);
SessionStatus ObsPause(bool pause);
SessionStatus ObsMute(bool muted);
SessionStatus ObsStop();
SessionStatus ObsStatus();
std::string StatusToJson(const SessionStatus& status);
StartRequest ParseStartRequest(const std::string& json);
