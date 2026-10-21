using System.Runtime.InteropServices;

namespace Luma.Core.Session;

public enum DiskVerdict
{
    Ok,
    Low,
    Full
}

public static class DiskSpaceRules
{
    public const long RefuseBelowBytes = 500L * 1024 * 1024;
    public const long StopBelowBytes = 300L * 1024 * 1024;
    public const int AudioKbpsPerTrack = 160;
    public static readonly TimeSpan LowBelow = TimeSpan.FromMinutes(10);

    /// <summary>Free bytes for the folder's volume, including network shares; null when it can't be read.</summary>
    public static long? FreeBytes(string folder)
    {
        try
        {
            var path = Path.GetFullPath(folder);
            while (!Directory.Exists(path))
            {
                var parent = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parent) || parent == path)
                {
                    return null;
                }

                path = parent;
            }

            return GetDiskFreeSpaceEx(path.EndsWith('\\') ? path : path + "\\", out var available, out _, out _)
                ? (long)available
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static TimeSpan Remaining(long freeBytes, int videoKbps, int audioTracks)
    {
        var bytesPerSecond = (Math.Max(0, videoKbps) + AudioKbpsPerTrack * Math.Max(1, audioTracks)) * 1000.0 / 8;
        return TimeSpan.FromSeconds(Math.Max(0, freeBytes) / bytesPerSecond);
    }

    public static DiskVerdict BeforeStart(long freeBytes, int videoKbps, int audioTracks)
        => Judge(freeBytes, videoKbps, audioTracks, RefuseBelowBytes);

    public static DiskVerdict WhileRecording(long freeBytes, int videoKbps, int audioTracks)
        => Judge(freeBytes, videoKbps, audioTracks, StopBelowBytes);

    private static DiskVerdict Judge(long freeBytes, int videoKbps, int audioTracks, long floor)
    {
        if (freeBytes < floor)
        {
            return DiskVerdict.Full;
        }

        return Remaining(freeBytes, videoKbps, audioTracks) < LowBelow ? DiskVerdict.Low : DiskVerdict.Ok;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong free);
}
