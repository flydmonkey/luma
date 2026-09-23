using Luma.App.Services;
using Luma.Core.Localization;
using Luma.Core.Session;
using Luma.Core.Settings;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Luma.App;

public sealed partial class MainWindow
{
    private void LoadSettingsIntoUi()
    {
        _loadingSettings = true;
        ThemeBox.SelectedIndex = Settings.Theme == AppThemeMode.Light ? 0 : 1;
        FillLanguageBox();
        FillFormat();
        SelectTag(FpsBox, Settings.Quality.FrameRate.ToString());
        SaveFolderText.Text = Settings.SaveFolder;
        LanPlaybackBox.IsOn = Settings.Lan.Enabled;
        LanPortBox.Text = Settings.Lan.Port.ToString();
        LanKeyBox.Password = Settings.Lan.AccessKey;
        HomeSystemAudioBox.IsOn = Settings.Audio.CaptureSystem;
        HomeMicBox.IsOn = Settings.Audio.CaptureMicrophone;
        SystemAudioBox.IsOn = Settings.Audio.CaptureSystem;
        MicBox.IsOn = Settings.Audio.CaptureMicrophone;
        AudioOnlyBox.IsOn = Settings.Audio.AudioOnly;
        HardwareBox.IsOn = Settings.Quality.HardwareEncoding;
        CameraBox.IsOn = Settings.Overlay.CameraEnabled;
        CameraXSlider.Value = Settings.Overlay.CameraX * 100;
        CameraYSlider.Value = Settings.Overlay.CameraY * 100;
        CameraWSlider.Value = Settings.Overlay.CameraWidth * 100;
        CameraHSlider.Value = Settings.Overlay.CameraHeight * 100;
        var textMark = Settings.Overlay.Watermarks.FirstOrDefault(item => item.Kind == WatermarkKind.Text);
        var imageMark = Settings.Overlay.Watermarks.FirstOrDefault(item => item.Kind == WatermarkKind.Image);
        var stamp = Settings.Overlay.Watermarks.Any(item => item.Kind == WatermarkKind.Timestamp);
        TimestampBox.IsOn = stamp;
        TextWatermarkBox.IsOn = textMark is not null;
        TextWatermarkText.Text = textMark?.Content ?? "";
        TextOpacitySlider.Value = (textMark?.Opacity ?? 1) * 100;
        ImageWatermarkBox.IsOn = imageMark is not null;
        _imageWatermarkPath = imageMark?.Content;
        ImageWatermarkPathText.Text = string.IsNullOrWhiteSpace(_imageWatermarkPath) ? "" : Path.GetFileName(_imageWatermarkPath);
        ImageOpacitySlider.Value = (imageMark?.Opacity ?? 0.9) * 100;
        WatermarkXSlider.Value = (textMark?.X ?? imageMark?.X ?? 0.02) * 100;
        WatermarkYSlider.Value = (textMark?.Y ?? imageMark?.Y ?? 0.1) * 100;
        WatermarkWSlider.Value = (textMark?.Width ?? 0.16) * 100;
        WatermarkHSlider.Value = (textMark?.Height ?? 0.11) * 100;
        SegmentBox.IsOn = Settings.Automation.SegmentEnabled;
        SegmentMinutesBox.Text = Settings.Automation.SegmentMinutes.ToString();
        SegmentMbBox.Text = Settings.Automation.SegmentMaxMegabytes.ToString();
        LaunchToTrayBox.IsOn = Settings.LaunchToTray;

        HotkeyEnabledBox.IsOn = Settings.Hotkeys.Enabled;
        StartHotkeyBox.Text = Settings.Hotkeys.Start;
        PauseHotkeyBox.Text = Settings.Hotkeys.Pause;
        StopHotkeyBox.Text = Settings.Hotkeys.Stop;
        ScreenshotHotkeyBox.Text = Settings.Hotkeys.Screenshot;
        CloseToTrayBox.IsOn = Settings.CloseToTray;
        HideTrayIconBox.IsOn = Settings.HideTrayIcon;
        RecordingBarBox.IsOn = Settings.ShowRecordingBar;
        SilentModeBox.IsOn = Settings.SilentMode;
        if (Settings.LastMode == CaptureMode.Game)
        {
            Settings.LastMode = CaptureMode.Display;
        }

        _mode = Settings.LastMode;
        _target = new CaptureTarget { Mode = _mode };
        ModeBox.SelectedIndex = _mode is CaptureMode.Display or CaptureMode.Region or CaptureMode.Window
            ? (int)_mode
            : 0;
        if (HomeMonitorBox.Items.Count > 0)
        {
            HomeMonitorBox.SelectedIndex = Math.Clamp(Settings.MonitorIndex, 0, HomeMonitorBox.Items.Count - 1);
        }

        RefreshLanAddress();
        PlaceOverlayRects();
        SettingsDependents_Changed(this, new RoutedEventArgs());
        SyncModeButtons();
        ApplyQualityCeiling();
        ApplyLanguage();
        _loadingSettings = false;
        _settingsReady = true;
        _tray.ApplyVisibility(Settings.HideTrayIcon);
    }

    private void PersistSettingsFromUi()
    {
        if (_loadingSettings) return;
        Settings.Theme = ThemeBox.SelectedIndex == 0 ? AppThemeMode.Light : AppThemeMode.Dark;
        Settings.UiLanguage = SelectedLanguage();
        Settings.Quality.HardwareEncoding = HardwareBox.IsOn;
        Settings.Quality.FrameRate = SelectedFps();
        Settings.RecordingFormat = SelectedFormat();
        if (HomeQualityBox.SelectedItem is ComboBoxItem home && Enum.TryParse<QualityLevel>(home.Tag as string, out var level))
        {
            var fps = Settings.Quality.FrameRate;
            Settings.Quality = QualitySettings.FromLevel(level, fps);
            Settings.Quality.HardwareEncoding = HardwareBox.IsOn;
        }

        Settings.Audio.CaptureSystem = SystemAudioBox.IsOn;
        Settings.Audio.CaptureMicrophone = MicBox.IsOn;
        Settings.Audio.AudioOnly = AudioOnlyBox.IsOn;
        Settings.Audio.MicrophoneDeviceId = SelectedMicId();
        Settings.Lan.Enabled = LanPlaybackBox.IsOn;
        Settings.Lan.AccessKey = LanKeyBox.Password ?? "";
        Settings.Overlay = BuildOverlay();
        Settings.Automation.SegmentEnabled = SegmentBox.IsOn;
        Settings.Automation.SegmentMinutes = int.TryParse(SegmentMinutesBox.Text, out var minutes) ? Math.Max(1, minutes) : 10;
        Settings.Automation.SegmentMaxMegabytes = int.TryParse(SegmentMbBox.Text, out var mb) ? Math.Max(0, mb) : 0;
        Settings.Automation.StartAtLogon = false;
        Settings.Automation.Schedules = [];
        Settings.LaunchToTray = LaunchToTrayBox.IsOn;
        Settings.Hotkeys.Enabled = HotkeyEnabledBox.IsOn;
        Settings.Hotkeys.Start = StartHotkeyBox.Text;
        Settings.Hotkeys.Pause = PauseHotkeyBox.Text;
        Settings.Hotkeys.Stop = StopHotkeyBox.Text;
        Settings.Hotkeys.Screenshot = ScreenshotHotkeyBox.Text;
        Settings.CloseToTray = CloseToTrayBox.IsOn;
        Settings.HideTrayIcon = HideTrayIconBox.IsOn;
        _tray.ApplyVisibility(Settings.HideTrayIcon);
        Settings.ShowRecordingBar = RecordingBarBox.IsOn;
        Settings.SilentMode = SilentModeBox.IsOn;
        Settings.LastMode = _mode;
        if (HomeMonitorBox.SelectedItem is ComboBoxItem { Tag: int monitor })
        {
            Settings.MonitorIndex = monitor;
        }

        SaveSettings();
        TryApplyHotkeys(silent: true);
        AutomationTaskService.Apply(Settings, Environment.ProcessPath ?? "");
    }

    private OverlaySettings BuildOverlay()
    {
        var marks = new List<WatermarkSettings>();
        if (TimestampBox.IsOn)
        {
            marks.Add(new WatermarkSettings { Kind = WatermarkKind.Timestamp, X = WatermarkXSlider.Value / 100, Y = WatermarkYSlider.Value / 100, Width = 0.18, Height = 0.06 });
        }

        if (TextWatermarkBox.IsOn && !string.IsNullOrWhiteSpace(TextWatermarkText.Text))
        {
            marks.Add(new WatermarkSettings
            {
                Kind = WatermarkKind.Text,
                Content = TextWatermarkText.Text,
                X = WatermarkXSlider.Value / 100,
                Y = WatermarkYSlider.Value / 100,
                Width = WatermarkWSlider.Value / 100,
                Height = WatermarkHSlider.Value / 100,
                Opacity = TextOpacitySlider.Value / 100
            });
        }

        if (ImageWatermarkBox.IsOn && File.Exists(_imageWatermarkPath))
        {
            marks.Add(new WatermarkSettings
            {
                Kind = WatermarkKind.Image,
                Content = _imageWatermarkPath,
                X = WatermarkXSlider.Value / 100,
                Y = WatermarkYSlider.Value / 100,
                Width = WatermarkWSlider.Value / 100,
                Height = WatermarkHSlider.Value / 100,
                Opacity = ImageOpacitySlider.Value / 100
            });
        }

        return new OverlaySettings
        {
            CameraEnabled = CameraBox.IsOn,
            CameraDeviceId = (CameraDeviceBox.SelectedItem as ComboBoxItem)?.Tag as string,
            CameraDeviceName = CameraDeviceBox.SelectedItem is ComboBoxItem camera && camera.Tag as string != "default"
                ? camera.Content?.ToString()
                : "",
            CameraX = CameraXSlider.Value / 100,
            CameraY = CameraYSlider.Value / 100,
            CameraWidth = CameraWSlider.Value / 100,
            CameraHeight = CameraHSlider.Value / 100,
            Watermarks = marks
        };
    }

    private bool _fillingLanguage;
    private bool _fillingDevices;
    private bool _settingsReady;

    private void FillLanguageBox()
    {
        if (LanguageBox is null)
        {
            return;
        }

        _fillingLanguage = true;
        try
        {
            if (LanguageBox.Items.Count == 0)
            {
                void Add(string tag, string labelKey)
                    => LanguageBox.Items.Add(new ComboBoxItem { Content = UiCopy.T(labelKey), Tag = tag });

                Add(UiLanguages.System, "settings.lang.system");
                Add(UiLanguages.English, "settings.lang.en");
                Add(UiLanguages.SimplifiedChinese, "settings.lang.zhHans");
                Add(UiLanguages.TraditionalChinese, "settings.lang.zhHant");
                Add(UiLanguages.Japanese, "settings.lang.ja");
                Add(UiLanguages.Korean, "settings.lang.ko");
            }
            else
            {
                foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
                {
                    item.Content = (item.Tag as string) switch
                    {
                        UiLanguages.English => UiCopy.T("settings.lang.en"),
                        UiLanguages.SimplifiedChinese => UiCopy.T("settings.lang.zhHans"),
                        UiLanguages.TraditionalChinese => UiCopy.T("settings.lang.zhHant"),
                        UiLanguages.Japanese => UiCopy.T("settings.lang.ja"),
                        UiLanguages.Korean => UiCopy.T("settings.lang.ko"),
                        _ => UiCopy.T("settings.lang.system")
                    };
                }
            }

            SelectTag(LanguageBox, UiLanguages.Normalize(Settings.UiLanguage));
        }
        finally
        {
            _fillingLanguage = false;
        }
    }

    private string SelectedLanguage() =>
        LanguageBox.SelectedItem is ComboBoxItem item ? item.Tag as string ?? UiLanguages.System : UiLanguages.System;

    private void ApplyQualityCeiling()
    {
        if (HomeQualityBox is null)
        {
            return;
        }

        var (width, height) = CurrentQualityLimit();
        var choices = QualitySettings.ChoicesFitting(width, height);
        var clamped = QualitySettings.Clamp(Settings.Quality.Level, width, height);
        var loading = _loadingSettings;
        _loadingSettings = true;
        try
        {
            HomeQualityBox.Items.Clear();
            if (choices.Length == 0)
            {
                HomeQualityBox.Items.Add(new ComboBoxItem { Content = $"{width}×{height}", Tag = "native" });
                HomeQualityBox.SelectedIndex = 0;
            }
            else
            {
                foreach (var level in choices)
                {
                    HomeQualityBox.Items.Add(new ComboBoxItem { Content = QualityLabel(level), Tag = level.ToString() });
                }

                SelectTag(HomeQualityBox, clamped.ToString());
            }
        }
        finally
        {
            _loadingSettings = loading;
        }

        if (choices.Length == 0 || clamped == Settings.Quality.Level)
        {
            return;
        }

        var frameRate = Settings.Quality.FrameRate;
        var hardware = Settings.Quality.HardwareEncoding;
        Settings.Quality = QualitySettings.FromLevel(clamped, frameRate);
        Settings.Quality.HardwareEncoding = hardware;
        if (!loading)
        {
            SaveSettings();
        }
    }

    private static string QualityLabel(QualityLevel level) => level switch
    {
        QualityLevel.Sd => "720p",
        QualityLevel.Hd => "1080p",
        QualityLevel.ExtraHd => "1440p",
        QualityLevel.FourK => "4K",
        _ => "1080p"
    };

    private void FillFormat()
    {
        var selected = RecordingContainers.Normalize(Settings.RecordingFormat);
        FormatBox.Items.Clear();
        foreach (var format in RecordingContainers.Choices)
        {
            FormatBox.Items.Add(new ComboBoxItem { Content = format.ToUpperInvariant(), Tag = format });
        }

        SelectTag(FormatBox, selected);
    }

    private string SelectedFormat() =>
        FormatBox.SelectedItem is ComboBoxItem item
            ? RecordingContainers.Normalize(item.Tag as string)
            : RecordingContainers.Mp4;

    private void FillMonitors()
    {
        if (HomeMonitorBox is null)
        {
            return;
        }

        HomeMonitorBox.Items.Clear();
        foreach (var display in DisplayCatalog.ListDisplays())
        {
            HomeMonitorBox.Items.Add(new ComboBoxItem
            {
                Content = DisplayCaption(display),
                Tag = display.Index
            });
        }

        for (var i = 0; i < HomeMonitorBox.Items.Count; i++)
        {
            if (HomeMonitorBox.Items[i] is ComboBoxItem item && item.Tag is int index && index == Settings.MonitorIndex)
            {
                HomeMonitorBox.SelectedIndex = i;
                return;
            }
        }

        if (HomeMonitorBox.Items.Count > 0)
        {
            HomeMonitorBox.SelectedIndex = 0;
        }
    }

    private void FillDevices()
    {
        _ = FillDevicesAsync();
    }

    private async Task FillDevicesAsync()
    {
        try
        {
            _fillingDevices = true;
            var mics = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.AudioCapture);
            MicDeviceBox.Items.Clear();
            MicDeviceBox.Items.Add(new ComboBoxItem { Content = UiCopy.T("settings.audio.micdev.ph"), Tag = "default" });
            foreach (var mic in mics)
            {
                MicDeviceBox.Items.Add(new ComboBoxItem { Content = mic.Name, Tag = mic.Id });
            }

            SelectTag(MicDeviceBox, Settings.Audio.MicrophoneDeviceId ?? "default");
            var cameras = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);
            CameraDeviceBox.Items.Clear();
            CameraDeviceBox.Items.Add(new ComboBoxItem { Content = UiCopy.T("settings.camera.dev.ph"), Tag = "default" });
            foreach (var camera in cameras)
            {
                CameraDeviceBox.Items.Add(new ComboBoxItem { Content = camera.Name, Tag = camera.Id });
            }

            SelectTag(CameraDeviceBox, Settings.Overlay.CameraDeviceId ?? "default");
        }
        catch
        {
            // device enumeration is optional
        }
        finally
        {
            _fillingDevices = false;
        }
    }

    private void SettingEdited(object sender, RoutedEventArgs e)
    {
        if (_settingsReady && !_loadingSettings)
        {
            PersistSettingsFromUi();
        }
    }

    private void DeviceBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsReady || _loadingSettings || _fillingDevices)
        {
            return;
        }

        PersistSettingsFromUi();
    }

    private void OpacitySlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_settingsReady && !_loadingSettings)
        {
            PersistSettingsFromUi();
        }
    }

    private string? SelectedMicId() => (MicDeviceBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private int SelectedFps() => FpsBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag as string, out var fps) ? fps : 30;

    private static void SelectTag(ComboBox box, string tag)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                box.SelectedIndex = i;
                return;
            }
        }
    }

    private void ApplyLanguage()
    {
        UiCopy.SetLanguage(UiLanguages.ResolveEffective(Settings.UiLanguage));
        TitleText.Text = _page switch
        {
            "library" => UiCopy.T("page.library"),
            "settings" => UiCopy.T("page.settings"),
            _ => UiCopy.T("page.record")
        };
        SetTile(ModeFullButton, UiCopy.T("home.mode.display"));
        SetTile(ModeRegionButton, UiCopy.T("home.mode.region"));
        SetTile(ModeWindowButton, UiCopy.T("home.mode.window"));
        SetTile(ScreenshotButton, UiCopy.T("home.mode.shot"));
        SetTile(ModeAudioButton, UiCopy.T("home.mode.audio"));
        StartButton.Content = UiCopy.T("home.start");
        PauseButton.Content = UiCopy.T("home.pause");
        StopButton.Content = UiCopy.T("home.stop");
        HomeSystemAudioBox.Header = UiCopy.T("home.system");
        HomeMicBox.Header = UiCopy.T("home.mic");
        HomeQualityBox.Header = UiCopy.T("home.quality");
        HomeMonitorBox.PlaceholderText = UiCopy.T("settings.monitor.ph");
        RefreshMonitorLabels();
        FillLanguageBox();
        SecAppearance.Text = UiCopy.T("sec.appearance");
        SecFiles.Text = UiCopy.T("sec.files");
        SecLan.Text = UiCopy.T("sec.lan");
        SecAudio.Text = UiCopy.T("sec.audio");
        SecQuality.Text = UiCopy.T("sec.quality");
        SecCamera.Text = UiCopy.T("sec.camera");
        SecWatermark.Text = UiCopy.T("sec.watermark");
        SecAuto.Text = UiCopy.T("sec.auto");
        SecSystem.Text = UiCopy.T("sec.system");
        SecHotkeys.Text = UiCopy.T("sec.hotkeys");
        SecReset.Text = UiCopy.T("sec.reset");
        SecAbout.Text = UiCopy.T("sec.about");
        ThemeCard.Header = UiCopy.T("settings.theme");
        ThemeCard.Description = UiCopy.T("settings.theme.desc");
        ThemeLightItem.Content = UiCopy.T("settings.theme.light");
        ThemeDarkItem.Content = UiCopy.T("settings.theme.dark");
        LanguageCard.Header = UiCopy.T("settings.language");
        LanguageCard.Description = UiCopy.T("settings.language.desc");
        SaveFolderCard.Header = UiCopy.T("settings.save");
        LanCard.Header = UiCopy.T("settings.lan");
        LanCard.Description = UiCopy.T("settings.lan.desc");
        LanKeyCard.Header = UiCopy.T("settings.key");
        LanKeyDesc.Text = UiCopy.T("settings.key.desc");
        LanAddressCard.Header = UiCopy.T("settings.address");
        LanCopyButton.Content = UiCopy.T("settings.copy");
        SystemAudioCard.Header = UiCopy.T("settings.audio.system");
        MicCard.Header = UiCopy.T("settings.audio.mic");
        MicDeviceCard.Header = UiCopy.T("settings.audio.micdev");
        AudioOnlyCard.Header = UiCopy.T("settings.audio.only");
        AudioOnlyCard.Description = UiCopy.T("settings.audio.only.desc");
        FormatCard.Header = UiCopy.T("settings.format");
        FormatCard.Description = UiCopy.T("settings.format.desc");
        FpsCard.Header = UiCopy.T("settings.fps");
        FpsCard.Description = UiCopy.T("settings.fps.desc");
        HardwareCard.Header = UiCopy.T("settings.hw");
        CameraCard.Header = UiCopy.T("settings.camera");
        CameraDeviceCard.Header = UiCopy.T("settings.camera.dev");
        TimestampCard.Header = UiCopy.T("settings.stamp");
        TextWatermarkCard.Header = UiCopy.T("settings.wm.text");
        TextWatermarkTextCard.Header = UiCopy.T("settings.wm.text.value");
        ImageWatermarkCard.Header = UiCopy.T("settings.wm.image");
        SegmentCard.Header = UiCopy.T("settings.seg");
        SegmentMinutesCard.Header = UiCopy.T("settings.seg.min");
        SegmentMinutesBox.PlaceholderText = UiCopy.T("settings.seg.min.ph");
        SegmentMbCard.Header = UiCopy.T("settings.seg.mb");
        LaunchToTrayCard.Header = UiCopy.T("settings.launchTray");
        LaunchToTrayCard.Description = UiCopy.T("settings.launchTray.desc");
        HotkeyEnabledCard.Header = UiCopy.T("settings.hotkey");
        StartHotkeyCard.Header = UiCopy.T("settings.hotkey.start");
        StartHotkeyCard.Description = UiCopy.T("settings.hotkey.press.desc");
        StartHotkeyBox.PlaceholderText = UiCopy.T("settings.hotkey.press");
        PauseHotkeyCard.Header = UiCopy.T("settings.hotkey.pause");
        PauseHotkeyBox.PlaceholderText = UiCopy.T("settings.hotkey.press");
        StopHotkeyCard.Header = UiCopy.T("settings.hotkey.stop");
        StopHotkeyBox.PlaceholderText = UiCopy.T("settings.hotkey.press");
        ScreenshotHotkeyCard.Header = UiCopy.T("settings.hotkey.shot");
        ScreenshotHotkeyBox.PlaceholderText = UiCopy.T("settings.hotkey.press");
        HotkeyStatusCard.Header = UiCopy.T("settings.hotkey.status");
        CloseToTrayCard.Header = UiCopy.T("settings.tray");
        HideTrayIconCard.Header = UiCopy.T("settings.hideTray");
        HideTrayIconCard.Description = UiCopy.T("settings.hideTray.desc");
        RecordingBarCard.Header = UiCopy.T("settings.bar");
        RecordingBarCard.Description = UiCopy.T("settings.bar.desc");
        SilentModeCard.Header = UiCopy.T("settings.silent");
        SilentModeCard.Description = UiCopy.T("settings.silent.desc");
        PreviewBarButton.Label = UiCopy.T("lib.preview");
        RenameBarButton.Label = UiCopy.T("lib.rename");
        DeleteBarButton.Label = UiCopy.T("lib.delete");
        RepairBarButton.Text = UiCopy.T("lib.repair");
        MergeMenuItem.Text = UiCopy.T("lib.merge");
        SubtitleBarButton.Text = UiCopy.T("lib.subtitle");
        MusicBarButton.Text = UiCopy.T("lib.music");
        CompressBarButton.Label = UiCopy.T("lib.compress");
        LibraryEmptyTitle.Text = UiCopy.T("lib.empty");
        LibraryEmptyDesc.Text = UiCopy.T("lib.empty.desc");
        LibraryEmptyStartButton.Content = UiCopy.T("home.start");
        LibraryEmptyFolderButton.Content = UiCopy.T("lib.folder");
        LibraryColName.Text = UiCopy.T("lib.col.name");
        LibraryColSize.Text = UiCopy.T("lib.col.size");
        LibraryColDuration.Text = UiCopy.T("lib.col.duration");
        LibraryColDate.Text = UiCopy.T("lib.col.date");
        ToolTipService.SetToolTip(BackButton, UiCopy.T("nav.back"));
        ToolTipService.SetToolTip(LibraryButton, UiCopy.T("nav.library"));
        AutomationProperties.SetName(LibraryButton, UiCopy.T("nav.library"));
        ToolTipService.SetToolTip(SettingsButton, UiCopy.T("nav.settings"));
        AutomationProperties.SetName(SettingsButton, UiCopy.T("nav.settings"));
        ToolTipService.SetToolTip(ModeFullButton, UiCopy.T("home.mode.display"));
        ToolTipService.SetToolTip(ModeRegionButton, UiCopy.T("home.mode.region"));
        ToolTipService.SetToolTip(ModeWindowButton, UiCopy.T("home.mode.window"));
        ToolTipService.SetToolTip(ScreenshotButton, UiCopy.T("home.mode.shot"));
        AutomationProperties.SetName(ScreenshotButton, UiCopy.T("home.mode.shot"));
        ToolTipService.SetToolTip(ModeAudioButton, UiCopy.T("home.mode.audio.tip"));
        if (ModeBox.Items.Count >= 3)
        {
            if (ModeBox.Items[0] is ComboBoxItem display) display.Content = UiCopy.T("home.mode.display");
            if (ModeBox.Items[1] is ComboBoxItem region) region.Content = UiCopy.T("home.mode.region");
            if (ModeBox.Items[2] is ComboBoxItem window) window.Content = UiCopy.T("home.mode.window");
        }

        PreviewSavedButton.Content = UiCopy.T("home.preview");
        CountdownCancelButton.Content = UiCopy.T("common.cancel");
        ProcessingText.Text = UiCopy.T("rec.processing");
        SavedTitleText.Text = UiCopy.T("rec.saved");
        SavedPreviewButton.Content = UiCopy.T("home.preview");
        SavedCloseButton.Content = UiCopy.T("common.close");
        FolderBarButton.Label = UiCopy.T("lib.folder");
        ToolTipService.SetToolTip(MoreBarButton, UiCopy.T("lib.more"));
        AutomationProperties.SetName(MoreBarButton, UiCopy.T("lib.more"));
        RefreshMetadataItem.Text = UiCopy.T("lib.refresh");
        PreviewFullScreenButton.Label = _previewFullScreen ? UiCopy.T("lib.fullscreen.exit") : UiCopy.T("lib.fullscreen");
        ToolTipService.SetToolTip(PreviewFullScreenButton, _previewFullScreen ? UiCopy.T("lib.fullscreen.exit") : UiCopy.T("lib.fullscreen.tip"));
        TrimBarButton.Label = UiCopy.T("lib.trim");
        ChangeFolderButton.Content = UiCopy.T("settings.change");
        LanPortCard.Header = UiCopy.T("settings.lan.port");
        LanPortCard.Description = UiCopy.T("settings.lan.port.desc");
        LanPortApplyButton.Content = UiCopy.T("settings.apply");
        LanKeyBox.PlaceholderText = UiCopy.T("settings.optional");
        var keyVisible = LanKeyBox.PasswordRevealMode == PasswordRevealMode.Visible;
        LanKeyRevealButton.Content = keyVisible ? UiCopy.T("settings.hide") : UiCopy.T("settings.view");
        ToolTipService.SetToolTip(LanKeyRevealButton, keyVisible ? UiCopy.T("settings.hide.key") : UiCopy.T("settings.view.key"));
        MicDeviceBox.PlaceholderText = UiCopy.T("settings.audio.micdev.ph");
        SetDefaultItem(MicDeviceBox, UiCopy.T("settings.audio.micdev.ph"));
        Fps60Item.Content = UiCopy.T("settings.fps.60");
        CameraDeviceBox.PlaceholderText = UiCopy.T("settings.camera.dev.ph");
        SetDefaultItem(CameraDeviceBox, UiCopy.T("settings.camera.dev.ph"));
        PipTitle.Text = UiCopy.T("settings.pip");
        PipHint.Text = UiCopy.T("settings.pip.hint");
        OverlayCameraText.Text = UiCopy.T("settings.overlay.camera");
        CameraXLabel.Text = UiCopy.T("settings.pos.x");
        CameraYLabel.Text = UiCopy.T("settings.pos.y");
        CameraWLabel.Text = UiCopy.T("settings.size.w");
        CameraHLabel.Text = UiCopy.T("settings.size.h");
        TextWatermarkText.PlaceholderText = UiCopy.T("settings.wm.text.ph");
        TextOpacityLabel.Text = UiCopy.T("settings.wm.text.op");
        ImageWatermarkPickCard.Header = UiCopy.T("settings.wm.image.file");
        PickImageWatermarkButton.Content = UiCopy.T("settings.wm.pick");
        ImageOpacityLabel.Text = UiCopy.T("settings.wm.image.op");
        WatermarkXLabel.Text = UiCopy.T("settings.pos.x");
        WatermarkYLabel.Text = UiCopy.T("settings.pos.y");
        WatermarkWLabel.Text = UiCopy.T("settings.size.w");
        WatermarkHLabel.Text = UiCopy.T("settings.size.h");
        ResetCard.Header = UiCopy.T("settings.reset.header");
        ResetCard.Description = UiCopy.T("settings.reset.desc");
        ResetButton.Content = UiCopy.T("settings.reset");
        AboutExpander.Description = "1.0.0 · " + UiCopy.T("about.tagline");
        PrivacyCard.Header = UiCopy.T("about.privacy");
        TermsCard.Header = UiCopy.T("about.terms");
        ProjectCard.Header = UiCopy.T("about.project");
        ApiDocsCard.Header = UiCopy.T("about.docs");
        SkillCard.Header = UiCopy.T("about.skill");
        PlaceOverlayRects();
        SyncModeButtons();
        UpdateIdleSummary();
        _tray?.ApplyLanguage();
        RefreshLanAddress();
        LocalizeFrameworkChrome();
    }

    private void LocalizeFrameworkChrome()
    {
        if (Content is DependencyObject root)
        {
            NameScrollBars(root);
        }
    }

    private static void NameScrollBars(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollBar bar)
            {
                AutomationProperties.SetName(
                    bar,
                    bar.Orientation == Orientation.Vertical ? UiCopy.T("a11y.scroll.v") : UiCopy.T("a11y.scroll.h"));
            }

            NameScrollBars(child);
        }
    }

    private static void SetDefaultItem(ComboBox box, string label)
    {
        if (box.Items.Count > 0 && box.Items[0] is ComboBoxItem item && string.Equals(item.Tag as string, "default", StringComparison.Ordinal))
        {
            item.Content = label;
        }
    }

    private static void SetTile(Button button, string label)
    {
        if (button.Content is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock text)
        {
            text.Text = label;
        }
    }

    private static void SetTile(ToggleButton button, string label)
    {
        if (button.Content is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock text)
        {
            text.Text = label;
        }
    }

    private void ThemeBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        PersistSettingsFromUi();
        ApplySystemBackdrop();
        _bar?.ApplyTheme();
    }

    private void LanguageBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_fillingLanguage || _loadingSettings || LanguageBox is null)
        {
            return;
        }

        Settings.UiLanguage = SelectedLanguage();
        SaveSettings();
        UiCopy.SetLanguage(UiLanguages.ResolveEffective(Settings.UiLanguage));
        DispatcherQueue.TryEnqueue(ApplyLanguage);
    }

    private void HomeQuality_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || HomeQualityBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (Enum.TryParse<QualityLevel>(tag, out var level))
        {
            Settings.Quality = QualitySettings.FromLevel(level, SelectedFps());
            SaveSettings();
        }
    }

    private void FormatBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        Settings.RecordingFormat = SelectedFormat();
        SaveSettings();
    }

    private void FpsBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        Settings.Quality.FrameRate = SelectedFps();
        SaveSettings();
    }

    private void HomeMonitor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || HomeMonitorBox.SelectedItem is not ComboBoxItem item || item.Tag is not int index)
        {
            return;
        }

        Settings.MonitorIndex = index;
        SaveSettings();
        ApplyQualityCeiling();
        UpdateIdleSummary();
    }

    private void RefreshMonitorLabels()
    {
        if (HomeMonitorBox is null)
        {
            return;
        }

        foreach (var item in HomeMonitorBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is not int index)
            {
                continue;
            }

            var display = DisplayCatalog.ListDisplays().FirstOrDefault(d => d.Index == index);
            if (display is not null)
            {
                item.Content = DisplayCaption(display);
            }
        }
    }

    private void SettingsDependents_Changed(object sender, RoutedEventArgs e)
    {
        if (MicDeviceBox is null)
        {
            return;
        }

        MicDeviceBox.IsEnabled = MicBox.IsOn;
        CameraDeviceBox.IsEnabled = CameraBox.IsOn;
        var markOn = TextWatermarkBox.IsOn || ImageWatermarkBox.IsOn;
        OverlayCanvas.IsHitTestVisible = true;
        CameraPreviewRect.Opacity = CameraBox.IsOn ? 0.9 : 0.65;
        CameraPreviewRect.IsHitTestVisible = true;
        WatermarkPreviewRect.Opacity = markOn ? 0.95 : 0.55;
        WatermarkPreviewRect.IsHitTestVisible = true;
        CameraXSlider.IsEnabled = true;
        CameraYSlider.IsEnabled = true;
        CameraWSlider.IsEnabled = true;
        CameraHSlider.IsEnabled = true;
        SegmentMinutesBox.IsEnabled = SegmentBox.IsOn;
        SegmentMbBox.IsEnabled = SegmentBox.IsOn;
        if (!_loadingSettings && ReferenceEquals(sender, MicBox))
        {
            HomeMicBox.IsOn = MicBox.IsOn;
        }

        if (!_loadingSettings && ReferenceEquals(sender, SystemAudioBox))
        {
            HomeSystemAudioBox.IsOn = SystemAudioBox.IsOn;
        }

        if (_loadingSettings)
        {
            return;
        }

        if (AudioOnlyBox.IsOn)
        {
            _mode = CaptureMode.AudioOnly;
        }

        try
        {
            PersistSettingsFromUi();
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
        }

        SyncModeButtons();
        UpdateIdleSummary();
    }

    private void LanPlaybackBox_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        Settings.Lan.Enabled = LanPlaybackBox.IsOn;
        SaveSettings();
        if (Settings.Lan.Enabled) App.LanServer.Start();
        else App.LanServer.Stop();
        if (Settings.Lan.Enabled && !App.LanServer.IsRunning) ShowBanner(App.LanServer.LastError ?? "无法启动局域网服务。", InfoBarSeverity.Error);
        RefreshLanAddress();
    }

    private void LanPortApply_Click(object sender, RoutedEventArgs e) => ApplyLanPort();
    private void LanPortBox_LostFocus(object sender, RoutedEventArgs e) => ApplyLanPort();

    private void ApplyLanPort()
    {
        if (_loadingSettings) return;
        if (!LanPort.TryParse(LanPortBox.Text, out var port, out var error))
        {
            ShowBanner(error, InfoBarSeverity.Warning);
            LanPortBox.Text = Settings.Lan.Port.ToString();
            return;
        }

        Settings.Lan.Port = port;
        SaveSettings();
        if (Settings.Lan.Enabled)
        {
            App.LanServer.Start();
            if (!App.LanServer.IsRunning) ShowBanner(App.LanServer.LastError ?? "无法绑定该端口。", InfoBarSeverity.Error);
        }

        RefreshLanAddress();
    }

    private void LanKeyBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        Settings.Lan.AccessKey = LanKeyBox.Password ?? "";
        SaveSettings();
    }

    private void LanKeyReveal_Click(object sender, RoutedEventArgs e)
    {
        var show = LanKeyBox.PasswordRevealMode != PasswordRevealMode.Visible;
        LanKeyBox.PasswordRevealMode = show ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        LanKeyRevealButton.Content = show ? UiCopy.T("settings.hide") : UiCopy.T("settings.view");
        ToolTipService.SetToolTip(LanKeyRevealButton, show ? UiCopy.T("settings.hide.key") : UiCopy.T("settings.view.key"));
    }

    private void LanCopy_Click(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(LanAddressText.Text);
        Clipboard.SetContent(data);
    }

    private void RefreshLanAddress()
    {
        var lan = App.LanServer.BoundUrls.FirstOrDefault(url => !url.Contains("127.0.0.1", StringComparison.Ordinal) && !url.Contains("localhost", StringComparison.Ordinal));
        LanAddressText.Text = !Settings.Lan.Enabled
            ? UiCopy.T("settings.lan.off")
            : App.LanServer.IsRunning
                ? lan ?? $"http://127.0.0.1:{App.LanServer.BoundPort}"
                : App.LanServer.LastError ?? $"http://127.0.0.1:{Settings.Lan.Port}";
    }

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        Settings.SaveFolder = folder.Path;
        SaveFolderText.Text = folder.Path;
        SaveSettings();
        RefreshLibrary();
    }

    private void LaunchToTrayBox_Toggled(object sender, RoutedEventArgs e) { if (!_loadingSettings) PersistSettingsFromUi(); }
    private void HideTrayIconBox_Toggled(object sender, RoutedEventArgs e) { if (!_loadingSettings) PersistSettingsFromUi(); }
    private void RecordingBarBox_Toggled(object sender, RoutedEventArgs e) { if (!_loadingSettings) PersistSettingsFromUi(); }
    private void SilentModeBox_Toggled(object sender, RoutedEventArgs e) { if (!_loadingSettings) PersistSettingsFromUi(); }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        Settings = App.SettingsStore.Reset();
        LoadSettingsIntoUi();
        RefreshLibrary();
    }

    private void HotkeyBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        e.Handled = true;
        if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            return;
        }

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var win = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        box.Text = HotkeyService.FormatGesture(ctrl, alt, shift, win, e.Key.ToString());
        PersistSettingsFromUi();
    }

    private void PickImageWatermark_Click(object sender, RoutedEventArgs e)
    {
        var file = NativeFilePicker.Pick(WindowNative.GetWindowHandle(this), UiCopy.T("settings.wm.pick"), UiCopy.T("settings.wm.image.file"), "*.png;*.jpg;*.jpeg");
        if (file is null) return;
        _imageWatermarkPath = file;
        ImageWatermarkPathText.Text = Path.GetFileName(file);
        ImageWatermarkBox.IsOn = true;
        PersistSettingsFromUi();
    }

    private void PipFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width < 1)
        {
            return;
        }

        var height = Math.Round(e.NewSize.Width * 9 / 16);
        if (Math.Abs(PipFrame.Height - height) > 0.5)
        {
            PipFrame.Height = height;
        }
    }

    private void CameraRect_Pressed(object sender, PointerRoutedEventArgs e)
    {
        _draggingCamera = true;
        OverlayCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void WatermarkRect_Pressed(object sender, PointerRoutedEventArgs e)
    {
        _draggingMark = true;
        OverlayCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OverlayCanvas_Moved(object sender, PointerRoutedEventArgs e)
    {
        if (!_draggingCamera && !_draggingMark)
        {
            return;
        }

        var target = _draggingCamera ? CameraPreviewRect : WatermarkPreviewRect;
        var point = e.GetCurrentPoint(OverlayCanvas).Position;
        var maxX = Math.Max(0, OverlayCanvas.ActualWidth - target.Width);
        var maxY = Math.Max(0, OverlayCanvas.ActualHeight - target.Height);
        var x = Math.Clamp(point.X - target.Width / 2, 0, maxX);
        var y = Math.Clamp(point.Y - target.Height / 2, 0, maxY);
        target.Margin = new Thickness(x, y, 0, 0);
        if (_draggingCamera)
        {
            CameraXSlider.Value = maxX > 0 ? x / maxX * 100 : 0;
            CameraYSlider.Value = maxY > 0 ? y / maxY * 100 : 0;
        }
        else
        {
            WatermarkXSlider.Value = maxX > 0 ? x / maxX * 100 : 0;
            WatermarkYSlider.Value = maxY > 0 ? y / maxY * 100 : 0;
        }
    }

    private void OverlayCanvas_Released(object sender, PointerRoutedEventArgs e)
    {
        if (!_draggingCamera && !_draggingMark)
        {
            return;
        }

        _draggingCamera = false;
        _draggingMark = false;
        OverlayCanvas.ReleasePointerCapture(e.Pointer);
        if (!_loadingSettings)
        {
            PersistSettingsFromUi();
        }
    }

    private void OverlayCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => PlaceOverlayRects();

    private void OverlaySlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_draggingCamera || _draggingMark)
        {
            return;
        }

        if (_loadingSettings || OverlayCanvas is null)
        {
            return;
        }

        PlaceOverlayRects();
        PersistSettingsFromUi();
    }

    private void PlaceOverlayRects()
    {
        if (OverlayCanvas is null || CameraPreviewRect is null || WatermarkPreviewRect is null
            || CameraXSlider is null || CameraYSlider is null || CameraWSlider is null || CameraHSlider is null
            || WatermarkXSlider is null || WatermarkYSlider is null || WatermarkWSlider is null || WatermarkHSlider is null)
        {
            return;
        }

        var width = OverlayCanvas.ActualWidth > 1 ? OverlayCanvas.ActualWidth : 448;
        var height = OverlayCanvas.ActualHeight > 1 ? OverlayCanvas.ActualHeight : 252;
        CameraPreviewRect.Width = Math.Max(48, CameraWSlider.Value / 100 * width);
        CameraPreviewRect.Height = Math.Max(28, CameraHSlider.Value / 100 * height);
        CameraPreviewRect.Margin = new Thickness(
            CameraXSlider.Value / 100 * Math.Max(0, width - CameraPreviewRect.Width),
            CameraYSlider.Value / 100 * Math.Max(0, height - CameraPreviewRect.Height), 0, 0);
        WatermarkPreviewRect.Width = Math.Max(48, WatermarkWSlider.Value / 100 * width);
        WatermarkPreviewRect.Height = Math.Max(28, WatermarkHSlider.Value / 100 * height);
        WatermarkPreviewRect.Margin = new Thickness(
            WatermarkXSlider.Value / 100 * Math.Max(0, width - WatermarkPreviewRect.Width),
            WatermarkYSlider.Value / 100 * Math.Max(0, height - WatermarkPreviewRect.Height), 0, 0);
        if (OverlayMarkText is not null && TextWatermarkBox is not null)
        {
            OverlayMarkText.Text = TextWatermarkBox.IsOn && !string.IsNullOrWhiteSpace(TextWatermarkText.Text)
                ? TextWatermarkText.Text
                : UiCopy.T("settings.overlay.mark");
        }
    }

    private async void PrivacyCard_Click(object sender, RoutedEventArgs e) => await OpenSiteAsync($"luma/docs/legal/{DocLocale()}/privacy.html");
    private async void TermsCard_Click(object sender, RoutedEventArgs e) => await OpenSiteAsync($"luma/docs/legal/{DocLocale()}/terms.html");
    private async void ProjectCard_Click(object sender, RoutedEventArgs e) => await OpenSiteAsync("luma/");
    private async void ApiDocsCard_Click(object sender, RoutedEventArgs e) => await OpenSiteAsync("luma/docs/openapi/openapi.json");
    private async void SkillCard_Click(object sender, RoutedEventArgs e) => await OpenSiteAsync("luma-website/skill/");

    private static string DocLocale()
    {
        var lang = UiCopy.Lang;
        return lang is UiLanguages.English or UiLanguages.SimplifiedChinese or UiLanguages.TraditionalChinese or UiLanguages.Japanese or UiLanguages.Korean
            ? lang
            : UiLanguages.English;
    }

    private static Task OpenSiteAsync(string relative)
        => Launcher.LaunchUriAsync(new Uri("https://flydmonkey.github.io/" + relative)).AsTask();
}
