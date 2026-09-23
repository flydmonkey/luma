namespace Luma.Core.Settings;

public static class LanPort
{
    public const int Min = 1;
    public const int Max = 65535;
    public const int Default = LanSettings.DefaultPort;

    public static bool TryParse(string? text, out int port, out string? error)
    {
        port = 0;
        if (!int.TryParse(text, out var value) || value < Min || value > Max)
        {
            error = "端口必须是 1 到 65535 之间的整数。";
            return false;
        }

        port = value;
        error = null;
        return true;
    }

    public static bool IsValid(int port) => port is >= Min and <= Max;
}
