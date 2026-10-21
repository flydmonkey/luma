namespace Luma.Core.Session;

public static class AudioDeviceId
{
    private const string Marker = "MMDEVAPI#";

    // DeviceInformation ids look like \\?\SWD#MMDEVAPI#{0.0.1.00000000}.{guid}#{interface};
    // WASAPI only resolves the {0.0.1.00000000}.{guid} part.
    public static string ToWasapi(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == "default")
        {
            return "default";
        }

        var start = id.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return id;
        }

        start += Marker.Length;
        var end = id.IndexOf('#', start);
        return end < 0 ? id[start..] : id[start..end];
    }

    public static bool IsDefault(string? id) => string.IsNullOrWhiteSpace(id) || id == "default";

    public static bool Contains(IEnumerable<string> available, string id)
        => available.Any(item => string.Equals(ToWasapi(item), ToWasapi(id), StringComparison.OrdinalIgnoreCase));
}
