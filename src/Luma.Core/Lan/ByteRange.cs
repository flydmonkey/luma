using System.Globalization;

namespace Luma.Core.Lan;

public enum ByteRangeKind
{
    Full,
    Partial,
    Unsatisfiable
}

public readonly record struct ByteRange(ByteRangeKind Kind, long Start, long End)
{
    public long Length => End - Start + 1;

    public static ByteRange Parse(string? header, long size)
    {
        var full = new ByteRange(ByteRangeKind.Full, 0, size - 1);
        if (string.IsNullOrWhiteSpace(header))
        {
            return full;
        }

        var text = header.Trim();
        if (!text.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        var spec = text["bytes=".Length..].Trim();
        if (spec.Contains(',', StringComparison.Ordinal))
        {
            return full;
        }

        var dash = spec.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            return full;
        }

        var left = spec[..dash].Trim();
        var right = spec[(dash + 1)..].Trim();
        var unsatisfiable = new ByteRange(ByteRangeKind.Unsatisfiable, 0, -1);

        if (left.Length == 0)
        {
            if (!TryNumber(right, out var suffix))
            {
                return full;
            }

            if (suffix == 0 || size == 0)
            {
                return unsatisfiable;
            }

            return new ByteRange(ByteRangeKind.Partial, Math.Max(0, size - suffix), size - 1);
        }

        if (!TryNumber(left, out var start))
        {
            return full;
        }

        var end = size - 1;
        if (right.Length > 0)
        {
            if (!TryNumber(right, out end) || end < start)
            {
                return full;
            }
        }

        if (start >= size)
        {
            return unsatisfiable;
        }

        return new ByteRange(ByteRangeKind.Partial, start, Math.Min(end, size - 1));
    }

    private static bool TryNumber(string text, out long value)
        => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
