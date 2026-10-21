using System.ComponentModel;

namespace Luma.Core.Library;

public sealed class LibraryItem : INotifyPropertyChanged
{
    private TimeSpan _duration;
    private string _statusText = "";

    public static string AudioLabel { get; set; } = "音频";

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Name { get; set; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset Created { get; init; }
    public string? PosterPath { get; init; }
    public bool Missing { get; init; }

    public required TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (_duration == value) return;
            _duration = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Duration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationText)));
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationText)));
        }
    }
    public bool IsAudio => Path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase);

    public string SizeText => Missing
        ? ""
        : SizeBytes >= 1024 * 1024
            ? $"{SizeBytes / (1024d * 1024d):0.0} MB"
            : $"{SizeBytes / 1024d:0} KB";

    public string DurationText => !string.IsNullOrEmpty(StatusText)
        ? StatusText
        : Duration.TotalSeconds > 0
            ? (Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"mm\:ss"))
            : (IsAudio ? AudioLabel : "");
    public string DateText => Missing || Created == default
        ? ""
        : Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => $"{Name}  {SizeText}  {DateText}";
}
