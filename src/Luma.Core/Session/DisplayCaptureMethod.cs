namespace Luma.Core.Session;

/// <summary>How display and region recordings read the screen.</summary>
public enum DisplayCaptureMethod
{
    /// <summary>DXGI where desktop duplication works, else WGC, else GDI.</summary>
    Auto = 0,
    Dxgi = 1,
    Wgc = 2,

    /// <summary>Slower, but works without a GPU, before Windows 10 1903 and over Remote Desktop.</summary>
    Gdi = 3
}

public static class DisplayCaptureMethods
{
    public static readonly DisplayCaptureMethod[] Choices =
        [DisplayCaptureMethod.Auto, DisplayCaptureMethod.Dxgi, DisplayCaptureMethod.Wgc, DisplayCaptureMethod.Gdi];

    public static DisplayCaptureMethod Normalize(DisplayCaptureMethod method) =>
        Enum.IsDefined(method) ? method : DisplayCaptureMethod.Auto;

    /// <summary>The value the OBS host reads from "captureMethod".</summary>
    public static string HostValue(DisplayCaptureMethod method) => Normalize(method) switch
    {
        DisplayCaptureMethod.Dxgi => "dxgi",
        DisplayCaptureMethod.Wgc => "wgc",
        DisplayCaptureMethod.Gdi => "gdi",
        _ => "auto"
    };
}
