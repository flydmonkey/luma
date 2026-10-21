#pragma once

// A display capture source built on GDI BitBlt. Desktop duplication (DXGI) and
// Windows.Graphics.Capture both need a GPU-backed desktop; GDI also works on
// Windows builds before 1903, on the Microsoft Basic Render Driver and inside
// Remote Desktop sessions.
//
// Settings: "device" (\\.\DISPLAYn, empty for the primary display),
// "fps" and "capture_cursor".
extern const char* const kGdiDisplayCaptureId;

void RegisterGdiDisplayCapture();
