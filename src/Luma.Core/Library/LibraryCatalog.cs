namespace Luma.Core.Library;

public sealed class LibraryCatalog
{
    public IReadOnlyList<LibraryItem> List(string saveFolder)
    {
        if (!Directory.Exists(saveFolder))
        {
            return [];
        }

        return Directory.EnumerateFiles(saveFolder)
            .Where(path =>
                path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".partial.mkv", StringComparison.OrdinalIgnoreCase))
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new LibraryItem
                {
                    Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..12],
                    Path = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path),
                    SizeBytes = info.Length,
                    Duration = TimeSpan.Zero,
                    Created = info.CreationTimeUtc,
                    PosterPath = FindPoster(path)
                };
            })
            .OrderByDescending(item => item.Created)
            .ToArray();
    }

    public void Delete(string path)
    {
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
        var directory = System.IO.Path.GetDirectoryName(path) ?? throw new InvalidOperationException();
        var extension = System.IO.Path.GetExtension(path);
        var dest = System.IO.Path.Combine(directory, newName + extension);
        File.Move(path, dest);
        return dest;
    }

    private static string? FindPoster(string path)
    {
        var poster = System.IO.Path.ChangeExtension(path, ".jpg");
        return File.Exists(poster) ? poster : null;
    }
}
