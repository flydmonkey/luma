using Luma.App.Services;
using Luma.Core.Lan;
using Luma.Core.Localization;
using Luma.Core.Os;
using Luma.Core.Settings;
using Microsoft.UI.Xaml;

namespace Luma.App;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsStore SettingsStore { get; } = new();
    public static AppSettings Settings { get; set; } = new();
    public static LanServer LanServer { get; } = new(SettingsStore, () => Settings);
    private SingleInstanceGate? _instance;

    public App()
    {
        Settings = SettingsStore.Load();
        UiCopy.SetLanguage(UiLanguages.ResolveEffective(Settings.UiLanguage));
        if (AppTheme.ApplicationOverride(Settings.Theme) is { } theme)
        {
            RequestedTheme = theme;
        }

        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "ui-crash.log"), e.Exception.ToString());
            }
            catch
            {
                // ignore logging failures
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instance = new SingleInstanceGate();
        if (!_instance.IsOwner)
        {
            _instance.SignalActivate();
            _instance.Dispose();
            _instance = null;
            Environment.Exit(0);
            return;
        }

        Directory.CreateDirectory(Settings.SaveFolder);
        if (Settings.Lan.Enabled)
        {
            LanServer.Start();
        }

        try
        {
            MainWindow = new MainWindow(OsVersionGate.Evaluate());
        }
        catch (Exception ex)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luma");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ui-crash.log"), ex.ToString());
            throw;
        }
        _instance.Activated += () =>
        {
            var window = MainWindow;
            window?.DispatcherQueue.TryEnqueue(window.RestoreFromSecondStart);
        };

        if (Settings.LaunchToTray && Environment.GetCommandLineArgs().Any(arg =>
                arg.Equals("--logon", StringComparison.OrdinalIgnoreCase)))
        {
            // Stay hidden until the user opens the tray item.
            return;
        }

        MainWindow.Activate();
    }
}
