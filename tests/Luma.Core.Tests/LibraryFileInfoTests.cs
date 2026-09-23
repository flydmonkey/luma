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
