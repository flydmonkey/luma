using Windows.Devices.Enumeration;

namespace Luma.App.Services;

public static class LanTargetCatalog
{
    public static object List(string? kind)
    {
        return (kind ?? "displays").ToLowerInvariant() switch
        {
            "windows" => Windows(false),
            "games" => Array.Empty<object>(),
            "cameras" => Devices(DeviceClass.VideoCapture),
            "microphones" or "mics" => Devices(DeviceClass.AudioCapture),
            _ => DisplayCatalog.ListDisplays().Select(display => new
            {
                id = display.Index.ToString(),
                label = $"{display.Index + 1}  {display.DeviceName}  {display.Width}×{display.Height}",
                previewMissing = true
            }).ToArray()
        };
    }

    private static object Windows(bool gamesOnly) =>
        DisplayCatalog.ListWindows(gamesOnly).Select(window => new
        {
            id = window.ObsWindowId,
            label = string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title,
            previewMissing = true
        }).ToArray();

    private static object Devices(DeviceClass deviceClass)
    {
        try
        {
            var devices = DeviceInformation.FindAllAsync(deviceClass).AsTask().GetAwaiter().GetResult();
            return devices.Select(device => new
            {
                id = device.Id,
                label = device.Name,
                previewMissing = true
            }).ToArray();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }
}
