using System.Drawing;
using System.Drawing.Imaging;
using Luma.Core.Capture;
using Luma.Core.Session;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class StillShotTests
{
    [Fact]
    public void Region_smaller_than_8_pixels_does_not_count()
    {
        Assert.False(StillShot.FormsRegion(7, 100));
        Assert.False(StillShot.FormsRegion(8, 7));
        Assert.True(StillShot.FormsRegion(8, 8));
    }

    [Fact]
    public void File_name_is_distinct_from_a_recording()
    {
        var name = StillShot.FileName(new DateTime(2026, 9, 23, 14, 5, 6));
        Assert.Equal("Luma-Shot-20260923-140506.png", name);
        Assert.DoesNotContain(".mp4", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void System_snip_chord_is_not_the_factory_hotkey()
    {
        Assert.Equal("Ctrl+Shift+S", StillShot.FactoryHotkey);
        Assert.False(StillShot.IsSystemSnip(StillShot.FactoryHotkey));
        Assert.True(StillShot.IsSystemSnip("Win+Shift+S"));
        Assert.Equal("Ctrl+Shift+S", StillShot.NormalizeHotkey(null));
        Assert.Equal("Ctrl+Shift+S", StillShot.NormalizeHotkey("Win+Shift+S"));
        Assert.Equal("Ctrl+Alt+S", StillShot.NormalizeHotkey("Ctrl+Alt+S"));
    }

    [Fact]
    public void Capture_png_matches_the_requested_rectangle()
    {
        var png = StillShot.CapturePng(0, 0, 32, 24);
        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);
        Assert.Equal(32, bitmap.Width);
        Assert.Equal(24, bitmap.Height);
        Assert.Equal(255, bitmap.GetPixel(0, 0).A);
    }

    [Fact]
    public void Desktop_frame_crop_matches_the_selection()
    {
        using var frame = StillShot.CaptureDesktop([(0, 0, 64, 48)]);
        var png = frame.CropPng(8, 8, 16, 16);
        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);
        Assert.Equal(64, frame.Width);
        Assert.Equal(48, frame.Height);
        Assert.Equal(16, bitmap.Width);
        Assert.Equal(16, bitmap.Height);
        var slice = frame.Slice(0, 0, 64, 48);
        Assert.Equal(64 * 48 * 4, slice.Bgra.Length);
        Assert.Equal(255, slice.Bgra[3]);
    }

    [Fact]
    public void Rectangle_mark_paints_a_red_stroke()
    {
        using var bitmap = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        SnipInk.Draw(graphics, 0, 0, [new SnipMark(SnipMarkKind.Rectangle, 8, 8, 30, 28, null, 0)]);
        var edge = bitmap.GetPixel(8, 18);
        var outside = bitmap.GetPixel(2, 2);
        Assert.True(edge.R > 180);
        Assert.Equal(0, outside.R);
    }

    [Fact]
    public void Pen_mark_uses_the_chosen_color()
    {
        using var bitmap = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        SnipInk.Draw(graphics, 0, 0, [new SnipMark(SnipMarkKind.Pen, 4, 20, 30, 20, null, 0, unchecked((int)0xFF0078D4), [4, 20, 30, 20])]);
        var pixel = bitmap.GetPixel(16, 20);
        Assert.True(pixel.B > pixel.R);
        Assert.True(pixel.B > 80);
    }

    [Fact]
    public void Save_writes_png_and_rejects_a_blocked_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "luma-shot-" + Guid.NewGuid().ToString("N"));
        var png = StillShot.CapturePng(0, 0, 16, 16);
        var path = StillShot.SavePng(folder, png, new DateTime(2026, 9, 23, 14, 5, 6));
        Assert.True(File.Exists(path));
        Assert.EndsWith("Luma-Shot-20260923-140506.png", path, StringComparison.Ordinal);

        var blocked = Path.Combine(folder, "not-a-directory.png");
        File.WriteAllBytes(blocked, png);
        Assert.ThrowsAny<Exception>(() => StillShot.SavePng(blocked, png, DateTime.Now));
    }

    [Fact]
    public void Game_mode_is_rejected_and_old_settings_fall_back_to_display()
    {
        Assert.True(SessionStartRules.IsGameMode("game"));
        Assert.True(SessionStartRules.IsGameMode("3"));
        Assert.Equal("游戏录制已关闭。", SessionStartRules.Reject(new CaptureTarget { Mode = CaptureMode.Game, WindowId = "a:b:c" }));

        var file = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """
            {"lastMode":3,"hotkeys":{"enabled":true,"start":"Ctrl+Alt+R","pause":"Ctrl+Alt+Shift+P","stop":"Ctrl+Alt+S"},"automation":{"schedules":[{"mode":3}]}}
            """);
        var loaded = new SettingsStore(file).Load();
        Assert.Equal(CaptureMode.Display, loaded.LastMode);
        Assert.Equal("Ctrl+Shift+S", loaded.Hotkeys.Screenshot);
        Assert.Equal(CaptureMode.Display, loaded.Automation.Schedules[0].Mode);

        var reset = new SettingsStore(file).Reset();
        Assert.Equal("Ctrl+Shift+S", reset.Hotkeys.Screenshot);
    }
}
