namespace Luma.Core.Settings;

public static class RecordingContainers
{
    public const string Mp4 = "mp4";
    public const string Mkv = "mkv";
    public const string Mov = "mov";
    public const string Flv = "flv";

    public static readonly string[] Choices = [Mp4, Mkv, Mov, Flv];

    public static string Normalize(string? value)
    {
        var text = value?.Trim().TrimStart('.').ToLowerInvariant();
        return text is Mkv or Mov or Flv ? text : Mp4;
    }

    public static string VideoExtension(string? value) => "." + Normalize(value);

    public static bool IsVideo(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".flv", StringComparison.OrdinalIgnoreCase);
    }
}
