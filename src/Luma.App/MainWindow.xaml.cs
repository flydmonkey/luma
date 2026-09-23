using System.Runtime.InteropServices;
using System.Text.Json;
using Luma.App.Services;
using Luma.App.Views;
using Luma.Core.Library;
using Luma.Core.Localization;
using Luma.Core.Os;
using Luma.Core.Session;
using Luma.Core.Settings;
using Luma.Core.Lan;
using Luma.Media;
using Luma.Obs;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Luma.App;

public sealed partial class MainWindow : Window
{
    private readonly OsCompatibilityResult _compatibility;
    private readonly LibraryCatalog _library = new();
    private readonly MediaEditor _editor = new();
    private readonly ObsHostClient _host = new();
    private readonly RecordingSession _session;
    private readonly HotkeyService _hotkeys = new();
    private readonly TrayService _tray;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    private readonly FileSystemWatcher? _watcher;
    private CaptureMode _mode = CaptureMode.Display;
    private CaptureTarget _target = new() { Mode = CaptureMode.Display };
    private bool _loadingSettings = true;
    private bool _busy;
    private bool _countdownCanceled;
    private string _page = "record";
    private string? _lastFile;
    private bool _micMuted;
    private bool _dropMic;
    private readonly Dictionary<string, TimeSpan> _durations = new(StringComparer.OrdinalIgnoreCase);
    private RecordingBarWindow? _bar;
    private SnipSession? _snip;
    private DateTime _segmentStart;
    private bool _hiddenForRecording;
    private bool _windowConcealed;
    private bool _centerOnLaunch = true;
    private PointInt32? _restorePos;
    private bool _draggingCamera;
    private bool _draggingMark;
    private string? _imageWatermarkPath;

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainWindow(OsCompatibilityResult compatibility)
    {
        _compatibility = compatibility;
        _session = new RecordingSession(_host);
        InitializeComponent();
        Title = "Luma";
        AppIcon.Apply(this);
        ApplySystemBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDrag);
        if (AppWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        if (AppWindow?.TitleBar is { } bar)
        {
            bar.ExtendsContentIntoTitleBar = true;
        }

        AppTheme.ApplyToWindow(this, App.Settings.Theme);
        NativeMenuTheme.Apply(App.Settings.Theme);
        SyncTitleBarInsets();

        UiCopy.SetLanguage(UiLanguages.ResolveEffective(App.Settings.UiLanguage));
        _tray = new TrayService(DispatcherQueue, ShowFromTray, () => ShowPage("library"), ExitApp, () => _ = StartWithCountdownAsync(), () => _ = PauseAsync(), () => _ = StopAsync());
        _hotkeys.StartPressed += () => DispatcherQueue.TryEnqueue(() => _ = StartWithCountdownAsync());
        _hotkeys.PausePressed += () => DispatcherQueue.TryEnqueue(() => _ = PauseAsync());
        _hotkeys.StopPressed += () => DispatcherQueue.TryEnqueue(() => _ = StopAsync());
        _hotkeys.ScreenshotPressed += () => DispatcherQueue.TryEnqueue(() => _ = BeginScreenshotAsync());
        FillMonitors();
        FillDevices();
        LoadSettingsIntoUi();
        SettingsPage.SizeChanged += (_, _) => LocalizeFrameworkChrome();
        TryApplyHotkeys(silent: true);
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
        try
        {
            Directory.CreateDirectory(App.Settings.SaveFolder);
            _watcher = new FileSystemWatcher(App.Settings.SaveFolder) { EnableRaisingEvents = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size };
            _watcher.Created += (_, _) => DispatcherQueue.TryEnqueue(RefreshLibrary);
            _watcher.Deleted += (_, _) => DispatcherQueue.TryEnqueue(RefreshLibrary);
            _watcher.Renamed += (_, _) => DispatcherQueue.TryEnqueue(RefreshLibrary);
        }
        catch
        {
            // library still refreshes after stop
        }

        if (Content is FrameworkElement root)
        {
            root.Loaded += (_, _) =>
            {
                _tray.Attach(root.XamlRoot);
                ApplyWindowSize(520, 352);
                _centerOnLaunch = false;
                ApplyLanguage();
                SyncTitleBarInsets();
            };
            root.SizeChanged += (_, _) => SyncTitleBarInsets();
        }

        if (AppWindow is { } appWindow)
        {
            appWindow.Closing += OnClosing;
            appWindow.Changed += (_, _) => DispatcherQueue.TryEnqueue(SyncTitleBarInsets);
        }

        Activated += (_, _) => SyncTitleBarInsets();

        Closed += (_, _) =>
        {
            _timer.Stop();
            _bar?.Close();
            _hotkeys.Dispose();
            _tray.Dispose();
            _host.Dispose();
            _watcher?.Dispose();
        };
        if (!_compatibility.IsSupported)
        {
            ShowBanner(_compatibility.Message, InfoBarSeverity.Error);
            StartButton.IsEnabled = false;
        }

        ApplyLibraryItemStretch();
        ShowPage("record");
        RefreshLibrary();
        App.LanServer.SessionCommand += HandleLanCommandAsync;
        App.LanServer.ListTargets = LanTargetCatalog.List;
        App.LanServer.CaptureWebStill = LanTargetCatalog.CaptureWebStill;
        App.LanServer.CaptureWindowStill = LanTargetCatalog.CaptureWindowStill;
        App.LanServer.RecognizeWebStill = StillShotOcr.RecognizePngAsync;
        App.LanServer.RunJob = RunLanJobAsync;
        App.LanServer.SettingsChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            UiCopy.SetLanguage(UiLanguages.ResolveEffective(App.Settings.UiLanguage));
            LoadSettingsIntoUi();
            AppTheme.ApplyToWindow(this, App.Settings.Theme);
            NativeMenuTheme.Apply(App.Settings.Theme);
            _tray.ApplyVisibility(App.Settings.HideTrayIcon);
            _tray.ApplyLanguage();
        });
    }

    private void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (App.Settings.CloseToTray)
        {
            args.Cancel = true;
            ConcealWindow();
        }
    }

    public void RestoreFromSecondStart() => ShowFromTray();

    public static ImageSource? PosterSource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return new BitmapImage(new Uri(path));
    }

    private AppSettings Settings
    {
        get => App.Settings;
        set => App.Settings = value;
    }

    private void SaveSettings() => App.SettingsStore.Save(Settings);

    private void ShowFromTray()
    {
        if (_session.Phase is SessionPhase.Recording or SessionPhase.Paused)
        {
            if (RecordingChrome.ShowBar(Settings.ShowRecordingBar, fromLan: false, Settings.SilentMode))
            {
                ShowBar();
            }

            return;
        }

        RevealWindow();
    }

    private void ConcealForRecording()
    {
        _hiddenForRecording = true;
        ConcealWindow();
    }

    private void ConcealWindow()
    {
        var pos = AppWindow.Position;
        if (pos.X > -8000 && pos.Y > -8000)
        {
            _restorePos = pos;
        }

        _windowConcealed = true;
        AppWindow.IsShownInSwitchers = false;
        SetCloaked(true);
        AppWindow.Move(new PointInt32(-32000, -32000));
    }

    private void RevealWindow()
    {
        _hiddenForRecording = false;
        _windowConcealed = false;
        SetCloaked(false);
        AppWindow.IsShownInSwitchers = true;
        if (_restorePos is { } pos)
        {
            try
            {
                AppWindow.Move(pos);
            }
            catch (ArgumentException)
            {
                // Keep the last accepted position.
            }
        }

        try
        {
            AppWindow.Show();
            Activate();
        }
        catch (ArgumentException)
        {
            Activate();
        }

        ApplySystemBackdrop();
    }

    private void SetCloaked(bool cloak)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = cloak ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 13, ref value, sizeof(int));
    }

    private void ExitApp()
    {
        Settings.CloseToTray = false;
        Close();
    }

    private void Poll()
    {
        TryScheduledStart();
        if (_session.Phase is not (SessionPhase.Recording or SessionPhase.Paused))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _statusPoll, 1, 0) == 0)
        {
            _ = RefreshElapsedAsync();
        }

        PaintElapsed();
    }

    private async Task RefreshElapsedAsync()
    {
        try
        {
            await _host.TryRefreshStatusAsync();
        }
        catch (OperationCanceledException)
        {
            // window is closing
        }
        finally
        {
            Interlocked.Exchange(ref _statusPoll, 0);
        }

        DispatcherQueue.TryEnqueue(PaintElapsed);
    }

    private void PaintElapsed()
    {
        if (_session.Phase is not (SessionPhase.Recording or SessionPhase.Paused))
        {
            return;
        }

        var status = _session.GetStatus();
        ElapsedText.Text = status.EncodedDuration.ToString(@"hh\:mm\:ss");
        TitleRecBadge.Visibility = Visibility.Visible;
        if (_session.Phase == SessionPhase.Recording && SegmentDue())
        {
            _ = RotateSegmentAsync();
        }
    }

    private DateTime _lastScheduleCheck = DateTime.MinValue;
    private bool _scheduleFired;
    private bool _scheduleOverlapNotified;
    private int _statusPoll;

    private void TryScheduledStart()
    {
        if (DateTime.UtcNow - _lastScheduleCheck < TimeSpan.FromSeconds(20)) return;
        _lastScheduleCheck = DateTime.UtcNow;
        var rule = Settings.Automation.Schedules.FirstOrDefault(item => item.Enabled);
        if (rule is null)
        {
            _scheduleFired = false;
            _scheduleOverlapNotified = false;
            return;
        }

        var active = _session.Phase is SessionPhase.Recording or SessionPhase.Paused;
        var decision = ScheduleGate.Evaluate(TimeOnly.FromDateTime(DateTime.Now), rule.Start, rule.End, rule.DurationMinutes, active, _scheduleFired);
        if (decision.Overlap && !_scheduleOverlapNotified)
        {
            _scheduleOverlapNotified = true;
            ShowBanner("已有录制在进行，这次定时开录已跳过。", InfoBarSeverity.Warning);
            return;
        }

        if (decision.Start)
        {
            _scheduleFired = true;
            _scheduleOverlapNotified = false;
            _ = StartAsync();
            return;
        }

        if (decision.Stop)
        {
            _scheduleFired = false;
            _ = StopAsync();
            return;
        }

        if (!active && !_scheduleFired)
        {
            _scheduleOverlapNotified = false;
        }
    }

    private async Task RotateSegmentAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var stopped = await _session.StopAsync();
            await Task.Run(() => MediaProbe.TryPoster(stopped.OutputPath));
            RememberDuration(stopped.OutputPath, stopped.Duration);
            _segmentStart = DateTime.UtcNow;
            var path = NewOutputPath();
            await _session.StartAsync(BuildRequest(path));
            _lastFile = path;
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
            SetRecordingUi(false);
        }
        finally
        {
            RefreshLibrary();
            _busy = false;
        }
    }

    private void ShowPage(string tag)
    {
        if (_page == "settings" && tag != "settings" && !_loadingSettings)
        {
            try
            {
                PersistSettingsFromUi();
            }
            catch
            {
                // leaving the page still has to show the destination
            }
        }

        _page = tag;
        RecordPage.Visibility = tag == "record" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = tag == "library" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = tag == "record" ? Visibility.Collapsed : Visibility.Visible;
        LibraryButton.Visibility = tag == "library" ? Visibility.Collapsed : Visibility.Visible;
        SettingsButton.Visibility = tag == "settings" ? Visibility.Collapsed : Visibility.Visible;
        TitleText.Text = tag switch
        {
            "library" => UiCopy.T("page.library"),
            "settings" => UiCopy.T("page.settings"),
            _ => UiCopy.T("page.record")
        };
        if (tag == "library")
        {
            ClosePreview();
            RefreshLibrary();
            ApplyWindowSize(760, 720);
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, LocalizeFrameworkChrome);
        }
        else if (tag == "settings")
        {
            ApplyWindowSize(600, 680);
            DispatcherQueue.TryEnqueue(PlaceOverlayRects);
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, LocalizeFrameworkChrome);
        }
        else
        {
            ApplyWindowSize(520, 352);
        }
    }

    private void ApplyWindowSize(int widthDip, int heightDip)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1;
        try
        {
            AppWindow.Resize(new SizeInt32(Math.Max(1, (int)(widthDip * scale)), Math.Max(1, (int)(heightDip * scale))));
        }
        catch (ArgumentException)
        {
            // A cloaked window rejects the size until it is back on screen.
        }

        if (_centerOnLaunch)
        {
            CenterOnWorkArea();
        }

        SyncTitleBarInsets();
    }

    private void CenterOnWorkArea()
    {
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)
            ?? DisplayArea.Primary;
        var work = display.WorkArea;
        var size = AppWindow.Size;
        try
        {
            AppWindow.Move(new PointInt32(
                work.X + Math.Max(0, (work.Width - size.Width) / 2),
                work.Y + Math.Max(0, (work.Height - size.Height) / 2)));
        }
        catch (ArgumentException)
        {
            // A cloaked window rejects the move until it is back on screen.
        }
    }

    private void SyncTitleBarInsets()
    {
        if (TitleBarRightPad is null || AppWindow?.TitleBar is not { } bar)
        {
            return;
        }

        var scale = Content is FrameworkElement { XamlRoot.RasterizationScale: > 0 and var rootScale }
            ? rootScale
            : GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        var insetDip = Math.Max(bar.RightInset, 0) / scale;
        const double floorDip = 46 * 3;
        TitleBarRightPad.Width = Math.Max(insetDip, floorDip);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_page == "library" && LibraryViewer.Visibility == Visibility.Visible)
        {
            ClosePreview();
            return;
        }

        ShowPage("record");
    }

    private void LibraryNav_Click(object sender, RoutedEventArgs e) => ShowPage("library");
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage("settings");
    private void EmptyStart_Click(object sender, RoutedEventArgs e) => ShowPage("record");

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button)
        {
            return;
        }

        var tag = Convert.ToString(button.Tag);
        _mode = tag switch
        {
            "1" => CaptureMode.Region,
            "2" => CaptureMode.Window,
            "audio" => CaptureMode.AudioOnly,
            _ => CaptureMode.Display
        };
        Settings.LastMode = _mode == CaptureMode.AudioOnly ? CaptureMode.Display : _mode;
        Settings.Audio.AudioOnly = _mode == CaptureMode.AudioOnly;
        AudioOnlyBox.IsOn = _mode == CaptureMode.AudioOnly;
        if (_mode != CaptureMode.AudioOnly)
        {
            _target = new CaptureTarget { Mode = _mode };
        }

        SaveSettings();
        SyncModeButtons();
        ApplyQualityCeiling();
        if (_mode is CaptureMode.Region or CaptureMode.Window)
        {
            _ = PickTargetAsync();
        }
    }

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _mode = (CaptureMode)Math.Clamp(ModeBox.SelectedIndex, 0, 2);
        SyncModeButtons();
    }

    private void SyncModeButtons()
    {
        ModeFullButton.IsChecked = _mode == CaptureMode.Display;
        ModeRegionButton.IsChecked = _mode == CaptureMode.Region;
        ModeWindowButton.IsChecked = _mode == CaptureMode.Window;
        ModeAudioButton.IsChecked = _mode == CaptureMode.AudioOnly;
        PickTargetButton.Visibility = _mode is CaptureMode.Region or CaptureMode.Window
            ? Visibility.Visible
            : Visibility.Collapsed;
        HomeMonitorBox.Visibility = _mode == CaptureMode.Display ? Visibility.Visible : Visibility.Collapsed;
        PickTargetButton.Content = UiCopy.T("pick.target");
        UpdateIdleSummary();
    }

    private void UpdateIdleSummary()
    {
        if (StatusText is null || _session.Phase is not SessionPhase.Idle)
        {
            return;
        }

        var target = _mode switch
        {
            CaptureMode.Display => MonitorSummary(),
            CaptureMode.Region when _target.CropWidth > 0 => UiCopy.Tf("sum.region", _target.CropWidth, _target.CropHeight),
            CaptureMode.Region => UiCopy.T("sum.region.none"),
            CaptureMode.Window when !string.IsNullOrWhiteSpace(_target.WindowTitle) => UiCopy.Tf("sum.window", _target.WindowTitle!),
            CaptureMode.Window => UiCopy.T("sum.window.none"),
            CaptureMode.AudioOnly => UiCopy.T("sum.audio"),
            _ => UiCopy.T("sum.pick")
        };

        var system = (HomeSystemAudioBox?.IsOn ?? SystemAudioBox?.IsOn ?? Settings.Audio.CaptureSystem)
            ? UiCopy.T("sum.sys.on")
            : UiCopy.T("sum.sys.off");
        var mic = (HomeMicBox?.IsOn ?? MicBox?.IsOn ?? Settings.Audio.CaptureMicrophone)
            ? UiCopy.T("sum.mic.on")
            : UiCopy.T("sum.mic.off");
        StatusText.Text = target + "  ·  " + system + " · " + mic;
        var ready = _mode is CaptureMode.Display or CaptureMode.AudioOnly || _target.IsReady;
        StartButton.IsEnabled = _compatibility.IsSupported && ready;
    }

    private string MonitorSummary()
    {
        var displays = DisplayCatalog.ListDisplays();
        var index = Settings.MonitorIndex;
        if (HomeMonitorBox?.SelectedItem is ComboBoxItem { Tag: int selected })
        {
            index = selected;
        }

        var display = displays.FirstOrDefault(d => d.Index == index) ?? displays.ElementAtOrDefault(index);
        if (display is not null)
        {
            return DisplayCaption(display);
        }

        return UiCopy.Tf("sum.display", "1");
    }

    private static string DisplayCaption(DisplayInfo display)
    {
        var name = display.DeviceName?.Trim();
        var generic = string.IsNullOrWhiteSpace(name)
            || name.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase);
        var body = generic
            ? $"{display.Index + 1}  {display.Width}x{display.Height}"
            : $"{display.Index + 1}  {name}  {display.Width}x{display.Height}";
        return UiCopy.Tf("sum.display", body);
    }

    private void ApplySystemBackdrop()
    {
        SystemBackdrop = null;
        SystemBackdrop = new MicaBackdrop();
        AppTheme.ApplyToWindow(this, App.Settings.Theme);
        NativeMenuTheme.Apply(App.Settings.Theme);
    }

    private static string ModeLabel(CaptureMode mode) => mode switch
    {
        CaptureMode.Region => UiCopy.T("home.mode.region"),
        CaptureMode.Window => UiCopy.T("home.mode.window"),
        CaptureMode.AudioOnly => UiCopy.T("home.mode.audio"),
        _ => UiCopy.T("home.mode.display")
    };

    private void HomeQuick_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        Settings.Audio.CaptureSystem = HomeSystemAudioBox.IsOn;
        Settings.Audio.CaptureMicrophone = HomeMicBox.IsOn;
        SystemAudioBox.IsOn = HomeSystemAudioBox.IsOn;
        MicBox.IsOn = HomeMicBox.IsOn;
        SaveSettings();
        UpdateIdleSummary();
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await StartWithCountdownAsync();
    private async void Screenshot_Click(object sender, RoutedEventArgs e) => await BeginScreenshotAsync();
    private void CancelCountdown_Click(object sender, RoutedEventArgs e) => _countdownCanceled = true;
    private async void Pause_Click(object sender, RoutedEventArgs e) => await PauseAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await StopAsync();
    private async void PickTarget_Click(object sender, RoutedEventArgs e) => await PickTargetAsync();

    private async Task BeginScreenshotAsync()
    {
        if (_snip is not null)
        {
            return;
        }

        var session = new SnipSession();
        _snip = session;
        try
        {
            var result = await session.RunAsync(Settings.SaveFolder, WindowNative.GetWindowHandle(this));
            if (result.Kind is SnipKind.SaveFailed or SnipKind.CopyFailed)
            {
                TellScreenshot(result.Message ?? UiCopy.T("shot.save.fail"), InfoBarSeverity.Error);
            }
            else if (result.Kind is SnipKind.Saved or SnipKind.Copied)
            {
                TellScreenshot(result.Message ?? "", InfoBarSeverity.Success);
            }
        }
        finally
        {
            if (ReferenceEquals(_snip, session))
            {
                _snip = null;
            }
        }
    }

    private void TellScreenshot(string message, InfoBarSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_hiddenForRecording)
        {
            if (severity == InfoBarSeverity.Error)
            {
                SnipNotice.Show(message);
            }

            return;
        }

        ShowBanner(message, severity);
    }

    private async Task StartWithCountdownAsync()
    {
        if (_busy || _session.Phase is SessionPhase.Recording or SessionPhase.Paused) return;
        if (!_target.IsReady && _mode is not (CaptureMode.Display or CaptureMode.AudioOnly))
        {
            ShowBanner(UiCopy.T("sum.needTarget"), InfoBarSeverity.Warning);
            return;
        }

        _countdownCanceled = false;
        ShowOverlay("count");
        for (var n = 3; n >= 1; n--)
        {
            CountdownText.Text = n.ToString();
            await Task.Delay(1000);
            if (_countdownCanceled)
            {
                HideOverlay();
                return;
            }
        }

        HideOverlay();
        ConcealForRecording();
        await Task.Delay(200);
        await StartAsync();
    }

    private bool SegmentDue()
    {
        long bytes = 0;
        if (_lastFile is not null && File.Exists(_lastFile))
        {
            bytes = new FileInfo(_lastFile).Length;
        }

        return SegmentGate.Due(
            Settings.Automation.SegmentEnabled,
            DateTime.UtcNow - _segmentStart,
            bytes,
            Settings.Automation.SegmentMinutes,
            Settings.Automation.SegmentMaxMegabytes);
    }

    private async Task StartAsync(bool fromLan = false)
    {
        _busy = true;
        try
        {
            var rejection = SessionStartRules.Reject(CurrentTarget());
            if (rejection is not null)
            {
                if (_hiddenForRecording || !fromLan)
                {
                    RevealWindow();
                }

                ShowBanner(rejection, InfoBarSeverity.Warning);
                return;
            }

            RememberMissingMic();
            Directory.CreateDirectory(Settings.SaveFolder);
            var path = NewOutputPath();
            await _session.StartAsync(BuildRequest(path));
            _lastFile = path;
            _segmentStart = DateTime.UtcNow;
            SetRecordingUi(true);
            if (RecordingChrome.ShowBar(Settings.ShowRecordingBar, fromLan, Settings.SilentMode))
            {
                if (!_hiddenForRecording)
                {
                    ConcealForRecording();
                }

                ShowBar();
            }

            var status = _session.GetStatus();
            StatusText.Text = $"{status.EncoderName}  {status.Width}×{status.Height}";
            HideOverlay();
        }
        catch (Exception ex)
        {
            SetRecordingUi(false);
            RevealWindow();
            HideOverlay();
            ShowBanner(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private (int Width, int Height) CurrentQualityLimit()
    {
        var displays = DisplayCatalog.ListDisplays();
        var display = displays.FirstOrDefault(item => item.Index == Settings.MonitorIndex) ?? displays.FirstOrDefault();
        var width = display?.Width ?? 1920;
        var height = display?.Height ?? 1080;
        if (_mode == CaptureMode.Region && _target.CropWidth > 1 && _target.CropHeight > 1)
        {
            return (_target.CropWidth, _target.CropHeight);
        }

        if (_mode == CaptureMode.Window && !string.IsNullOrWhiteSpace(_target.WindowId))
        {
            var window = DisplayCatalog.ListWindows(false)
                .FirstOrDefault(item => string.Equals(item.ObsWindowId, _target.WindowId, StringComparison.Ordinal));
            if (window is { Width: > 1, Height: > 1 })
            {
                return (window.Width, window.Height);
            }
        }

        if (_mode == CaptureMode.AudioOnly)
        {
            return (1280, 720);
        }

        return (width, height);
    }

    private CaptureTarget CurrentTarget() => new()
    {
        Mode = _mode,
        MonitorIndex = Settings.MonitorIndex,
        WindowId = _target.WindowId,
        WindowTitle = _target.WindowTitle,
        CropX = _target.CropX,
        CropY = _target.CropY,
        CropWidth = _target.CropWidth,
        CropHeight = _target.CropHeight
    };

    private void RememberMissingMic()
    {
        _dropMic = false;
        var id = SelectedMicId();
        if (!Settings.Audio.CaptureMicrophone || _micMuted || string.IsNullOrEmpty(id) || id == "default")
        {
            return;
        }

        var found = MicDeviceBox.Items.OfType<ComboBoxItem>().Any(item => string.Equals(item.Tag as string, id, StringComparison.Ordinal));
        if (!found)
        {
            _dropMic = true;
            ShowBanner("之前选择的麦克风已断开，本次不采集麦克风。", InfoBarSeverity.Warning);
        }
    }

    private RecordingRequest BuildRequest(string path) => new()
    {
        OutputPath = path,
        Target = CurrentTarget(),
        Quality = Settings.Quality,
        CaptureSystemAudio = Settings.Audio.CaptureSystem,
        CaptureMicrophone = Settings.Audio.CaptureMicrophone && !_micMuted && !_dropMic,
        MicrophoneDeviceId = SelectedMicId(),
        Overlay = BuildOverlay()
    };

    private string NewOutputPath()
    {
        var ext = _mode == CaptureMode.AudioOnly ? ".m4a" : RecordingContainers.VideoExtension(Settings.RecordingFormat);
        return Path.Combine(Settings.SaveFolder, $"Luma-{DateTime.Now:yyyyMMdd-HHmmss}{ext}");
    }

    private async Task PauseAsync()
    {
        if (_session.Phase == SessionPhase.Recording)
        {
            await _session.PauseAsync();
            PauseButton.Content = UiCopy.T("home.resume");
        }
        else if (_session.Phase == SessionPhase.Paused)
        {
            await _session.ResumeAsync();
            PauseButton.Content = UiCopy.T("home.pause");
        }

        _tray.SetSession(true, _session.Phase == SessionPhase.Paused);
    }

    private async Task StopAsync(bool fromLan = false)
    {
        if (_busy || _session.Phase == SessionPhase.Idle) return;
        _busy = true;
        var silentLan = fromLan && Settings.SilentMode;
        var bringBack = _hiddenForRecording && !silentLan;
        if (bringBack)
        {
            HideBar();
            RevealWindow();
            ShowBusyMask();
        }
        else if (!silentLan && !_windowConcealed)
        {
            ShowBusyMask();
        }

        try
        {
            var frozen = _session.GetStatus().EncodedDuration;
            ElapsedText.Text = frozen.ToString(@"hh\:mm\:ss");
            var result = await _session.StopAsync();
            _lastFile = result.OutputPath;
            await Task.Run(() => MediaProbe.TryPoster(result.OutputPath));
            RememberDuration(result.OutputPath, result.Duration);
            SetRecordingUi(false);
            HideBar();
            RefreshLibrary();
            SavedFileText.Text = Path.GetFileName(result.OutputPath);
            SavedWarningText.Visibility = string.IsNullOrWhiteSpace(result.Status.Warning) ? Visibility.Collapsed : Visibility.Visible;
            SavedWarningText.Text = result.Status.Warning ?? "";
            if (silentLan || (_windowConcealed && !bringBack))
            {
                HideBusyMask();
                return;
            }

            await Play(result.OutputPath, Path.GetFileNameWithoutExtension(result.OutputPath));
            if (!string.IsNullOrWhiteSpace(result.Status.Warning))
            {
                ShowBanner(result.Status.Warning, InfoBarSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (bringBack && _windowConcealed)
                {
                    RevealWindow();
                }
            }
            catch (Exception)
            {
                // The window must stay reachable even if the move fails.
            }

            HideOverlay();
            HideBusyMask();
            ShowBanner(ex.Message, InfoBarSeverity.Error);
            SetRecordingUi(false);
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetRecordingUi(bool recording)
    {
        StartButton.Visibility = Visibility.Visible;
        RecordingButtons.Visibility = Visibility.Collapsed;
        ElapsedText.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        TitleRecBadge.Visibility = Visibility.Collapsed;
        PauseButton.Content = UiCopy.T("home.pause");
        _tray.SetSession(recording, recording && _session.Phase == SessionPhase.Paused);
        if (!recording)
        {
            UpdateIdleSummary();
        }
    }

    private void ShowBusyMask()
    {
        BusyText.Text = UiCopy.T("rec.processing");
        BusyMask.Visibility = Visibility.Visible;
        BusyRing.IsActive = true;
    }

    private void HideBusyMask()
    {
        BusyRing.IsActive = false;
        BusyMask.Visibility = Visibility.Collapsed;
    }

    private void HideOverlay()
    {
        CountdownOverlay.Visibility = Visibility.Collapsed;
        ProcessingRing.IsActive = false;
        if (_session.Phase == SessionPhase.Idle)
        {
            SetRecordingUi(false);
        }
    }

    private void DismissSavedOverlay_Click(object sender, RoutedEventArgs e) => HideOverlay();

    private void ShowBar()
    {
        if (_bar is null)
        {
            var bar = new RecordingBarWindow(
                () => _session.GetStatus().EncodedDuration,
                () => _session.Phase == SessionPhase.Paused,
                () => PauseAsync(),
                () => StopAsync(),
                muted =>
                {
                    _micMuted = muted;
                    if (_session.Phase is SessionPhase.Recording or SessionPhase.Paused)
                    {
                        _ = _host.MuteAsync(muted);
                    }
                },
                Settings.Audio.CaptureMicrophone);
            bar.Closed += (_, _) =>
            {
                if (ReferenceEquals(_bar, bar))
                {
                    _bar = null;
                }
            };
            _bar = bar;
        }

        _bar.Reveal();
    }

    private void HideBar()
    {
        _bar?.Close();
        _bar = null;
    }

    private void ShowOverlay(string kind)
    {
        CountdownOverlay.Visibility = Visibility.Visible;
        CountdownPanel.Visibility = kind == "count" ? Visibility.Visible : Visibility.Collapsed;
        ProcessingPanel.Visibility = kind == "processing" ? Visibility.Visible : Visibility.Collapsed;
        SavedPanel.Visibility = kind == "saved" ? Visibility.Visible : Visibility.Collapsed;
        ProcessingRing.IsActive = kind == "processing";
    }

    private async void PreviewSaved_Click(object sender, RoutedEventArgs e)
    {
        HideOverlay();
        if (string.IsNullOrWhiteSpace(_lastFile) || !File.Exists(_lastFile))
        {
            ShowBanner("找不到刚录的文件。", InfoBarSeverity.Error);
            return;
        }

        ShowPage("library");
        await Play(_lastFile, Path.GetFileNameWithoutExtension(_lastFile));
    }

    private async Task PickTargetAsync()
    {
        if (_mode == CaptureMode.Region)
        {
            var display = DisplayCatalog.ListDisplays().ElementAtOrDefault(Settings.MonitorIndex)
                          ?? DisplayCatalog.ListDisplays().First();
            var shot = DisplayCatalog.CaptureMonitorPng(display);
            var picker = new RegionPickerWindow(display, shot);
            picker.Activate();
            var picked = await picker.PickAsync();
            if (picked is not null)
            {
                _target = picked;
            }
        }
        else if (_mode == CaptureMode.Window)
        {
            var systemResult = await PickWithSystemCaptureAsync();
            if (systemResult == SystemPickResult.Unavailable)
            {
                await PickWindowFromListAsync();
            }
        }

        ApplyQualityCeiling();
        UpdateIdleSummary();
    }

    private enum SystemPickResult
    {
        Picked,
        Canceled,
        Unavailable
    }

    private async Task<SystemPickResult> PickWithSystemCaptureAsync()
    {
        try
        {
            var picker = new GraphicsCapturePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var item = await picker.PickSingleItemAsync();
            if (item is null)
            {
                return SystemPickResult.Canceled;
            }

            var match = DisplayCatalog.ListWindows(gamesOnly: false)
                .FirstOrDefault(window => string.Equals(window.Title, item.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                ShowBanner(UiCopy.T("rec.need.target"), InfoBarSeverity.Warning);
                return SystemPickResult.Canceled;
            }

            _target = new CaptureTarget
            {
                Mode = _mode,
                WindowId = match.ObsWindowId,
                WindowTitle = match.Title
            };
            return SystemPickResult.Picked;
        }
        catch (Exception ex)
        {
            ShowBanner(UiCopy.Tf("pick.system.fail", ex.Message), InfoBarSeverity.Error);
            return SystemPickResult.Unavailable;
        }
    }

    private async Task PickWindowFromListAsync()
    {
        var windows = DisplayCatalog.ListWindows(gamesOnly: false);
        if (windows.Count == 0)
        {
            ShowBanner(UiCopy.T("sum.window.none"), InfoBarSeverity.Warning);
            return;
        }

        var list = new ListView
        {
            ItemsSource = windows.Select(window => window.Title).ToList(),
            SelectionMode = ListViewSelectionMode.Single,
            Height = 320
        };
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("pick.window"),
            Content = list,
            PrimaryButtonText = UiCopy.T("common.ok"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        ContentDialogResult choice;
        try
        {
            choice = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
            return;
        }

        if (choice == ContentDialogResult.Primary && list.SelectedIndex >= 0)
        {
            var info = windows[list.SelectedIndex];
            _target = new CaptureTarget
            {
                Mode = _mode,
                WindowId = info.ObsWindowId,
                WindowTitle = info.Title
            };
        }
    }

    private void ShowBanner(string? text, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        var open = !string.IsNullOrWhiteSpace(text);
        HomeInfoBar.Message = text ?? "";
        HomeInfoBar.Severity = severity;
        HomeInfoBar.IsOpen = open;
        HomeInfoBar.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => HomeInfoBar.Visibility = Visibility.Collapsed;

    private void TryApplyHotkeys(bool silent)
    {
        try
        {
            _hotkeys.Apply(Settings.Hotkeys);
            HotkeyErrorText.Text = "";
        }
        catch (Exception ex)
        {
            HotkeyErrorText.Text = ex.Message;
            if (!silent) ShowBanner(ex.Message, InfoBarSeverity.Warning);
        }
    }

    private static string? RequestedLanMode(JsonElement payload)
    {
        if (!payload.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!body.TryGetProperty("mode", out var mode))
        {
            return null;
        }

        return mode.ValueKind switch
        {
            JsonValueKind.String => mode.GetString(),
            JsonValueKind.Number => mode.GetRawText(),
            _ => null
        };
    }

    private Task<JsonElement> HandleLanCommandAsync(JsonElement payload)
    {
        var done = new TaskCompletionSource<JsonElement>();
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                done.SetResult(await HandleLanOnUiAsync(payload));
            }
            catch (Exception ex)
            {
                done.SetResult(JsonSerializer.SerializeToElement(new { ok = false, error = ex.Message }));
            }
        }))
        {
            done.SetResult(JsonSerializer.SerializeToElement(new { ok = false, error = "界面线程不可用。" }));
        }

        return done.Task;
    }

    private Task<string> RunLanJobAsync(string kind, string path, string body)
    {
        return kind.ToLowerInvariant() switch
        {
            "compress" => _editor.CompressAsync(path),
            "repair" => _editor.RepairAsync(path),
            "trim" => TrimLanJobAsync(path, body),
            _ => throw new InvalidOperationException("不支持的作业。")
        };
    }

    private Task<string> TrimLanJobAsync(string path, string body)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var start = doc.RootElement.TryGetProperty("startSeconds", out var s) ? s.GetDouble() : 0;
        var end = doc.RootElement.TryGetProperty("endSeconds", out var e) ? e.GetDouble() : 0;
        return _editor.TrimAsync(path, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
    }

    private async Task<JsonElement> HandleLanOnUiAsync(JsonElement payload)
    {
        var path = payload.TryGetProperty("path", out var pathValue) ? pathValue.GetString() ?? "" : "";
        var method = payload.TryGetProperty("method", out var methodValue) ? methodValue.GetString() ?? "GET" : "GET";
        if (path.EndsWith("/target", StringComparison.OrdinalIgnoreCase) && method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyLanTarget(payload);
        }

        if (path.EndsWith("/session", StringComparison.OrdinalIgnoreCase) && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(new
            {
                session = LanSessionSnapshot(),
                target = LanTargetSummary()
            }));
        }

        var action = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        switch (action)
        {
            case "start":
                if (SessionStartRules.IsGameMode(RequestedLanMode(payload)) || _mode == CaptureMode.Game)
                {
                    return FailLan("游戏录制已关闭。");
                }

                if (_session.Phase is not SessionPhase.Idle)
                {
                    return FailLan("已有录制正在进行。");
                }

                await StartAsync(fromLan: true);
                if (_session.Phase == SessionPhase.Idle)
                {
                    return FailLan(string.IsNullOrWhiteSpace(HomeInfoBar.Message) ? "请先选择录制目标。" : HomeInfoBar.Message);
                }

                return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(LanSessionSnapshot()));
            case "pause":
                if (_session.Phase is SessionPhase.Recording or SessionPhase.Paused)
                {
                    await PauseAsync();
                }

                return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(LanSessionSnapshot()));
            case "stop":
                if (_session.Phase != SessionPhase.Idle)
                {
                    _ = StopAsync(fromLan: true);
                }

                return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(LanSessionSnapshot()));
            default:
                return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(new
                {
                    session = LanSessionSnapshot(),
                    target = LanTargetSummary()
                }));
        }
    }

    private JsonElement ApplyLanTarget(JsonElement payload)
    {
        var mode = RequestedLanMode(payload);
        if (SessionStartRules.IsGameMode(mode))
        {
            return FailLan("游戏录制已关闭。");
        }

        _mode = mode?.ToLowerInvariant() switch
        {
            "region" => CaptureMode.Region,
            "window" => CaptureMode.Window,
            "audio" => CaptureMode.AudioOnly,
            _ => CaptureMode.Display
        };
        Settings.Audio.AudioOnly = _mode == CaptureMode.AudioOnly;
        Settings.LastMode = _mode == CaptureMode.AudioOnly ? CaptureMode.Display : _mode;
        if (payload.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Object)
        {
            if (body.TryGetProperty("displayId", out var display))
            {
                var text = display.ValueKind == JsonValueKind.String ? display.GetString() : display.GetRawText();
                if (int.TryParse(text, out var index))
                {
                    Settings.MonitorIndex = index;
                }
            }

            if (body.TryGetProperty("region", out var region) && region.ValueKind == JsonValueKind.Object)
            {
                _target = new CaptureTarget
                {
                    Mode = CaptureMode.Region,
                    MonitorIndex = Settings.MonitorIndex,
                    CropX = region.TryGetProperty("x", out var x) ? x.GetInt32() : 0,
                    CropY = region.TryGetProperty("y", out var y) ? y.GetInt32() : 0,
                    CropWidth = region.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                    CropHeight = region.TryGetProperty("height", out var h) ? h.GetInt32() : 0
                };
            }
            else if (_mode == CaptureMode.Window)
            {
                _target = new CaptureTarget
                {
                    Mode = CaptureMode.Window,
                    WindowId = body.TryGetProperty("windowId", out var id) ? id.GetString() : null,
                    WindowTitle = body.TryGetProperty("windowTitle", out var title) ? title.GetString() : null
                };
            }
            else
            {
                _target = new CaptureTarget { Mode = _mode, MonitorIndex = Settings.MonitorIndex };
            }
        }

        AudioOnlyBox.IsOn = _mode == CaptureMode.AudioOnly;
        SyncModeButtons();
        ApplyQualityCeiling();
        SaveSettings();
        return JsonSerializer.Deserialize<JsonElement>(LanControlApi.Ok(LanTargetSummary()));
    }

    private object LanSessionSnapshot()
    {
        var status = _session.GetStatus();
        var state = StopFileRules.WireState(_session.Phase);
        return new
        {
            state,
            elapsed = status.EncodedDuration.ToString(@"hh\:mm\:ss"),
            lastSaved = string.IsNullOrWhiteSpace(_lastFile) ? null : new { name = Path.GetFileName(_lastFile), warning = (string?)null },
            encoderName = status.EncoderName,
            actualWidth = status.Width,
            actualHeight = status.Height,
            stopForced = status.StopForced
        };
    }

    private object LanTargetSummary() => new
    {
        mode = _mode switch
        {
            CaptureMode.Region => "region",
            CaptureMode.Window => "window",
            CaptureMode.AudioOnly => "audio",
            _ => "fullscreen"
        },
        monitorIndex = Settings.MonitorIndex,
        region = _target.CropWidth > 0 ? new { x = _target.CropX, y = _target.CropY, width = _target.CropWidth, height = _target.CropHeight } : null,
        windowId = _target.WindowId,
        windowTitle = _target.WindowTitle
    };

    private static JsonElement FailLan(string error) =>
        JsonSerializer.Deserialize<JsonElement>(LanControlApi.Fail(error));
}
