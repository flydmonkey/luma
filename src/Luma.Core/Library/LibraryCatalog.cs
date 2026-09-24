namespace Luma.Core.Library;

public sealed class LibraryCatalog
{
    public IReadOnlyList<LibraryItem> List(string saveFolder)
    {
        if (!Directory.Exists(saveFolder))
        {
            return [];
        }

        var durations = LibraryFileInfo.Read(saveFolder);
        var present = new List<LibraryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(saveFolder))
        {
            if (!IsMedia(path))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    continue;
                }

                seen.Add(info.Name);
                present.Add(Item(path, info.Name, info.Length, info.CreationTimeUtc, durations, missing: false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var missing = new List<LibraryItem>();
        foreach (var name in durations.Keys)
        {
            if (seen.Contains(name) || !IsMedia(name))
            {
                continue;
            }

            missing.Add(Item(Path.Combine(saveFolder, name), name, 0, default, durations, missing: true));
        }

        return present
            .OrderByDescending(item => item.Created)
            .Concat(missing.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static LibraryItem Item(string path, string fileName, long size, DateTimeOffset created, IReadOnlyDictionary<string, double> durations, bool missing)
    {
        return new LibraryItem
        {
            Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..12],
            Path = path,
            Name = Path.GetFileNameWithoutExtension(fileName),
            SizeBytes = size,
            Duration = durations.TryGetValue(fileName, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.Zero,
            Created = created,
            PosterPath = missing ? null : FindPoster(path),
            Missing = missing
        };
    }

    private static bool IsMedia(string path)
        => Luma.Core.Settings.RecordingContainers.IsVideo(path)
            || path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".partial.mkv", StringComparison.OrdinalIgnoreCase);

    public void Delete(string path)
    {
        LibraryFileInfo.Forget(path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var poster = FindPoster(path);
        if (poster is not null && File.Exists(poster))
        {
            File.Delete(poster);
        }
    }

    public string Rename(string path, string newName)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        var directory = System.IO.Path.GetDirectoryName(path) ?? throw new InvalidOperationException();
        var extension = System.IO.Path.GetExtension(path);
        var dest = System.IO.Path.Combine(directory, newName + extension);
        File.Move(path, dest);
        LibraryFileInfo.Move(path, dest);
        var poster = FindPoster(path);
        if (poster is not null)
        {
            try { File.Move(poster, LibraryPaths.PosterFor(dest), overwrite: true); } catch (IOException) { }
        }
        return dest;
    }

    private static string? FindPoster(string path)
    {
        var poster = LibraryPaths.PosterFor(path);
        return File.Exists(poster) ? poster : null;
    }
}
