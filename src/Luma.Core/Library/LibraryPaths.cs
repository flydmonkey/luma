using System.Text.Json;

namespace Luma.Core.Library;

public static class LibraryPaths
{
    public const string MetaFolderName = ".luma";
    private const string LegacyIndexName = ".luma-files.json";
    private static readonly object Gate = new();

    public static string MetaFolder(string saveFolder) => Path.Combine(saveFolder, MetaFolderName);

    public static string IndexPath(string saveFolder) => Path.Combine(MetaFolder(saveFolder), "files.json");

    public static string PosterFor(string mediaPath)
    {
        var folder = Path.GetDirectoryName(mediaPath) ?? "";
        return Path.Combine(MetaFolder(folder), "posters", Path.GetFileName(mediaPath) + ".jpg");
    }

    public static string PreparePoster(string mediaPath)
    {
        var poster = PosterFor(mediaPath);
        EnsureMetaFolder(Path.GetDirectoryName(mediaPath) ?? "");
        Directory.CreateDirectory(Path.GetDirectoryName(poster)!);
        return poster;
    }

    public static void EnsureMetaFolder(string saveFolder)
    {
        var meta = MetaFolder(saveFolder);
        var info = Directory.CreateDirectory(meta);
        if (!info.Attributes.HasFlag(FileAttributes.Hidden))
        {
            info.Attributes |= FileAttributes.Hidden;
        }
    }

    // Older builds kept .luma-files.json and <name>.jpg next to the recordings.
    // Only posters of files Luma knew about move, so a user's own photo named
    // like an imported video stays where it is.
    public static void MigrateLegacy(string saveFolder)
    {
        var legacy = Path.Combine(saveFolder, LegacyIndexName);
        if (!File.Exists(legacy))
        {
            return;
        }

        lock (Gate)
        {
            if (!File.Exists(legacy))
            {
                return;
            }

            Dictionary<string, double> old;
            try
            {
                old = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(legacy)) ?? [];
            }
            catch (JsonException)
            {
                old = [];
            }
            catch (IOException)
            {
                return;
            }

            EnsureMetaFolder(saveFolder);
            LibraryFileInfo.Absorb(saveFolder, old);
            foreach (var media in Directory.EnumerateFiles(saveFolder))
            {
                var name = Path.GetFileName(media);
                if (!Settings.RecordingContainers.IsVideo(media)
                    || !(old.ContainsKey(name) || name.StartsWith("Luma-", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var loose = Path.ChangeExtension(media, ".jpg");
                var poster = PosterFor(media);
                if (!File.Exists(loose) || File.Exists(poster))
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(poster)!);
                    File.Move(loose, poster);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            try { File.Delete(legacy); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
