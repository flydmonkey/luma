using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Factory_default_uses_dark_theme_follow_system_language_and_port_12345()
    {
        var settings = new AppSettings();
        Assert.Equal(AppThemeMode.Dark, settings.Theme);
        Assert.Equal("system", settings.UiLanguage);
        Assert.Equal(12345, settings.Lan.Port);
        Assert.True(settings.Quality.HardwareEncoding);
    }

    [Fact]
    public void Save_and_load_keeps_folder_and_port()
    {
        var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.SaveFolder = Path.Combine(Path.GetTempPath(), "luma-recordings-test");
        settings.Lan.Port = 23456;
        store.Save(settings);

        var loaded = new SettingsStore(path).Load();
        Assert.Equal(settings.SaveFolder, loaded.SaveFolder);
        Assert.Equal(23456, loaded.Lan.Port);
    }

    [Fact]
    public void Reset_restores_dark_theme_system_language_and_default_port()
    {
        var path = Path.Combine(Path.GetTempPath(), "luma-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = store.Load();
        settings.Theme = AppThemeMode.Light;
        settings.UiLanguage = "en";
        settings.Lan.Port = 23456;
        store.Save(settings);

        var reset = store.Reset();
        Assert.Equal(AppThemeMode.Dark, reset.Theme);
        Assert.Equal("system", reset.UiLanguage);
        Assert.Equal(12345, reset.Lan.Port);
    }
}
