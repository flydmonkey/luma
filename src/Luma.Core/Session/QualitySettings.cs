namespace Luma.Core.Session;

public enum QualityLevel
{
    Sd = 0,
    Hd = 1,
    ExtraHd = 2,
    FourK = 3
}

public sealed class QualitySettings
{
    public QualityLevel Level { get; set; } = QualityLevel.Hd;
    public int FrameRate { get; set; } = 30;
    public int BitrateKbps { get; set; } = 8000;
    public bool HardwareEncoding { get; set; } = true;

    public (int Width, int Height) Resolution => Level switch
    {
        QualityLevel.Sd => (1280, 720),
        QualityLevel.Hd => (1920, 1080),
        QualityLevel.ExtraHd => (2560, 1440),
        QualityLevel.FourK => (3840, 2160),
        _ => (1920, 1080)
    };

    public static QualitySettings FromLevel(QualityLevel level, int frameRate = 30) => level switch
    {
        QualityLevel.Sd => new QualitySettings { Level = level, FrameRate = frameRate, BitrateKbps = 4000 },
        QualityLevel.Hd => new QualitySettings { Level = level, FrameRate = frameRate, BitrateKbps = 8000 },
        QualityLevel.ExtraHd => new QualitySettings { Level = level, FrameRate = frameRate, BitrateKbps = 16000 },
        QualityLevel.FourK => new QualitySettings { Level = level, FrameRate = frameRate, BitrateKbps = 35000 },
        _ => new QualitySettings { Level = QualityLevel.Hd, FrameRate = frameRate, BitrateKbps = 8000 }
    };

    public static readonly QualityLevel[] Ascending = [QualityLevel.Sd, QualityLevel.Hd, QualityLevel.ExtraHd, QualityLevel.FourK];

    public static bool Fits(QualityLevel level, int width, int height)
    {
        var (levelWidth, levelHeight) = FromLevel(level).Resolution;
        return width >= levelWidth && height >= levelHeight;
    }

    public static QualityLevel[] ChoicesFitting(int width, int height) =>
        Ascending.Where(level => Fits(level, width, height)).ToArray();

    public static QualityLevel Clamp(QualityLevel level, int width, int height)
    {
        var choices = ChoicesFitting(width, height);
        if (choices.Length == 0 || choices.Contains(level))
        {
            return level;
        }

        return choices[^1];
    }
}
