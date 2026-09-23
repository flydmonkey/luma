namespace Luma.Core.Library;

public sealed class LibraryItem
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Name { get; set; }
    public required long SizeBytes { get; init; }
    public required TimeSpan Duration { get; set; }
    public required DateTimeOffset Created { get; init; }
    public string? PosterPath { get; init; }
    public bool IsAudio => Path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase);

    public string SizeText => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / (1024d * 1024d):0.0} MB"
        : $"{SizeBytes / 1024d:0} KB";

    public string DurationText => Duration.TotalSeconds > 0
        ? (Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"mm\:ss"))
        : (IsAudio ? "音频" : "");
    public string DateText => Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => $"{Name}  {SizeText}  {DateText}";
}
