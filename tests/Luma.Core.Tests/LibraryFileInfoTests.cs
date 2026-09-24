using System.Text.Json;
using Luma.Core.Lan;
using Luma.Core.Library;

namespace Luma.Core.Tests;

public sealed class LibraryFileInfoTests
{
    [Fact]
    public void Saved_duration_is_read_back_without_probing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "Luma-take.mp4");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            LibraryFileInfo.Remember(media, TimeSpan.FromSeconds(12.5));

            var listed = new LibraryCatalog().List(folder).Single();
            Assert.Equal(TimeSpan.FromSeconds(12.5), listed.Duration);
            Assert.Equal(TimeSpan.FromSeconds(12.5), LibraryFileInfo.TryGet(media));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Missing_record_stays_empty_until_a_duration_is_saved()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "imported.mkv");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            Assert.Null(LibraryFileInfo.TryGet(media));
            Assert.Equal(TimeSpan.Zero, new LibraryCatalog().List(folder).Single().Duration);

            LibraryFileInfo.Remember(media, TimeSpan.FromSeconds(3));
            Assert.Equal(TimeSpan.FromSeconds(3), new LibraryCatalog().List(folder).Single().Duration);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Legacy_index_and_posters_move_into_the_hidden_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "Luma-old.mp4");
            var imported = Path.Combine(folder, "holiday.mp4");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            File.WriteAllBytes(imported, [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(folder, "Luma-old.jpg"), [9]);
            File.WriteAllBytes(Path.Combine(folder, "holiday.jpg"), [9]);
            File.WriteAllText(Path.Combine(folder, ".luma-files.json"), "{\"Luma-old.mp4\": 6}");

            var listed = new LibraryCatalog().List(folder).Single(item => item.Path == media);

            Assert.Equal(TimeSpan.FromSeconds(6), listed.Duration);
            Assert.Equal(LibraryPaths.PosterFor(media), listed.PosterPath);
            Assert.False(File.Exists(Path.Combine(folder, ".luma-files.json")));
            Assert.False(File.Exists(Path.Combine(folder, "Luma-old.jpg")));
            Assert.True(File.Exists(Path.Combine(folder, "holiday.jpg")));
            Assert.True(new DirectoryInfo(LibraryPaths.MetaFolder(folder)).Attributes.HasFlag(FileAttributes.Hidden));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Rename_carries_the_poster()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "before.mp4");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            File.WriteAllBytes(LibraryPaths.PreparePoster(media), [9]);

            var renamed = new LibraryCatalog().Rename(media, "after");

            Assert.True(File.Exists(LibraryPaths.PosterFor(renamed)));
            Assert.False(File.Exists(LibraryPaths.PosterFor(media)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void File_removed_outside_the_app_stays_listed_as_missing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "Luma-take.mp4");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            LibraryFileInfo.Remember(media, TimeSpan.FromSeconds(9));
            File.Delete(media);

            var catalog = new LibraryCatalog();
            var listed = catalog.List(folder).Single();
            Assert.True(listed.Missing);
            Assert.Equal(TimeSpan.FromSeconds(9), listed.Duration);
            Assert.Equal("", listed.SizeText);

            catalog.Delete(listed.Path);
            Assert.Empty(catalog.List(folder));

            File.WriteAllBytes(media, [1, 2, 3, 4]);
            LibraryFileInfo.Remember(media, TimeSpan.FromSeconds(9));
            File.Delete(media);
            var json = JsonSerializer.Serialize(LanControlApi.LibraryList(folder));
            Assert.Contains("\"missing\":true", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Rename_and_delete_keep_the_saved_duration_with_the_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-file-info-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var media = Path.Combine(folder, "before.mp4");
            File.WriteAllBytes(media, [1, 2, 3, 4]);
            LibraryFileInfo.Remember(media, TimeSpan.FromSeconds(8));

            var catalog = new LibraryCatalog();
            var renamed = catalog.Rename(media, "after");
            Assert.Equal(TimeSpan.FromSeconds(8), LibraryFileInfo.TryGet(renamed));
            Assert.Null(LibraryFileInfo.TryGet(media));

            catalog.Delete(renamed);
            Assert.Null(LibraryFileInfo.TryGet(renamed));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
