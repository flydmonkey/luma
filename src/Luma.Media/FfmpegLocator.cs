namespace Luma.Media;

public static class FfmpegLocator
{
    public static string? Find()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        var thirdParty = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "third_party", "ffmpeg", "ffmpeg.exe"));
        if (File.Exists(thirdParty))
        {
            return thirdParty;
        }

        return Environment.GetEnvironmentVariable("PATH")
            ?.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);
    }
}
