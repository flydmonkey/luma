namespace Luma.Core.Os;

public sealed class OsCompatibilityResult
{
    public required bool IsSupported { get; init; }
    public required string Message { get; init; }
    public required Version Version { get; init; }
}

public static class OsVersionGate
{
    public static readonly Version MinimumWindows = new(10, 0, 17763);

    public static OsCompatibilityResult Evaluate(Version? overrideVersion = null)
    {
        var version = overrideVersion ?? Environment.OSVersion.Version;
        var supported = version.Major > 10 || (version.Major == 10 && version.Build >= MinimumWindows.Build);
        return new OsCompatibilityResult
        {
            IsSupported = supported,
            Version = version,
            Message = supported
                ? "当前系统支持录制。"
                : "需要 Windows 10 1809 或更高版本才能录制。"
        };
    }
}
