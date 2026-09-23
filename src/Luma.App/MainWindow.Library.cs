using Luma.App.Services;
using Luma.Core.Library;
using Luma.Core.Session;
using Luma.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using WinRT.Interop;

namespace Luma.App;

public sealed partial class MainWindow
{
    private void RefreshLibrary()
    {
        var items = _library.List(Settings.SaveFolder).ToList();
        foreach (var item in items)
        {
            if (_durations.TryGetValue(item.Path, out var duration))
            {
                item.Duration = duration;
                continue;
            }

            if (item.Duration > TimeSpan.Zero)
            {
                _durations[item.Path] = item.Duration;
                continue;
            }

            duration = MediaProbe.TryDuration(item.Path);
            if (duration <= TimeSpan.Zero)
            {
                continue;
            }

            item.Duration = duration;
            _durations[item.Path] = duration;
            LibraryFileInfo.Remember(item.Path, duration);
        }

        LibraryEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibraryListHost.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        LibraryList.ItemsSource = items;
        if (!string.IsNullOrWhiteSpace(_lastFile))
        {
            var match = items.FirstOrDefault(item => string.Equals(item.Path, _lastFile, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                LibraryList.SelectedItem = match;
            }
        }

        UpdateLibraryCommands();
        DispatcherQueue.TryEnqueue(ApplyLibrarySelectionChrome);
    }

    private void RememberDuration(string path, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _durations[path] = duration;
        LibraryFileInfo.Remember(path, duration);
    }

    private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLibraryCommands();
        ApplyLibrarySelectionChrome();
    }

    private void LibraryList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem row)
        {
            ApplyLibraryRowChrome(row, LibraryList.SelectedItems.Contains(args.Item));
        }
    }

    private void ApplyLibraryItemStretch()
    {
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(ListViewItem.MinHeightProperty, 80d));
        style.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(8, 4, 8, 4)));
        style.Setters.Add(new Setter(ListViewItem.MarginProperty, new Thickness(0, 3, 0, 3)));
        style.Setters.Add(new Setter(ListViewItem.CornerRadiusProperty, new CornerRadius(8)));
        LibraryList.ItemContainerStyle = style;
    }

    private void ApplyLibrarySelectionChrome()
    {
        foreach (var item in LibraryList.Items)
        {
            if (LibraryList.ContainerFromItem(item) is ListViewItem row)
            {
                ApplyLibraryRowChrome(row, LibraryList.SelectedItems.Contains(item));
            }
        }
    }

    private void ApplyLibraryRowChrome(ListViewItem row, bool selected)
    {
        row.CornerRadius = new CornerRadius(8);
        row.BorderThickness = selected ? new Thickness(2) : new Thickness(0);
        row.BorderBrush = selected ? ThemeBrush("AccentFillColorDefaultBrush") : null;
        row.Background = selected ? ThemeBrush("AccentFillColorTertiaryBrush") : null;
    }

    private static Brush? ThemeBrush(string key)
        => Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush ? brush : null;

    private void LibraryList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => _ = OpenSelectedAsync();

    private void LibraryList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) _ = OpenSelectedAsync();
        if (e.Key == VirtualKey.Delete) Delete_Click(sender, e);
    }

    private void UpdateLibraryCommands()
    {
        var count = LibraryList.SelectedItems.Count;
        PreviewBarButton.IsEnabled = count == 1;
        RenameBarButton.IsEnabled = count == 1;
        DeleteBarButton.IsEnabled = count > 0;
        RepairBarButton.IsEnabled = count == 1;
        MergeMenuItem.IsEnabled = count > 1;
        SubtitleBarButton.IsEnabled = count == 1;
        MusicBarButton.IsEnabled = count == 1;
    }

    private LibraryItem? SelectedItem() => LibraryList.SelectedItem as LibraryItem;

    private void Preview_Click(object sender, RoutedEventArgs e) => _ = OpenSelectedAsync();

    private async Task OpenSelectedAsync()
    {
        if (SelectedItem() is not { } item || !File.Exists(item.Path)) return;
        if (_session.Phase == SessionPhase.Processing && IsCurrentCut(item.Path))
        {
            if (!_windowConcealed)
            {
                ShowBusyMask();
            }

            return;
        }

        await Play(item.Path, item.Name);
    }

    private bool IsCurrentCut(string path)
    {
        if (string.IsNullOrWhiteSpace(_lastFile) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (string.Equals(path, _lastFile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var partial = _lastFile + ".partial.mkv";
        return string.Equals(path, partial, StringComparison.OrdinalIgnoreCase);
    }

    private async Task Play(string path, string title)
    {
        if (_windowConcealed)
        {
            return;
        }

        if (!File.Exists(path))
        {
            HideBusyMask();
            ShowBanner(UiCopy.T("lib.rename.missing"), InfoBarSeverity.Error);
            return;
        }

        ShowBusyMask();
        _page = "library";
        RecordPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        LibraryPage.Visibility = Visibility.Visible;
        LibraryListPane.Visibility = Visibility.Collapsed;
        LibraryViewer.Visibility = Visibility.Collapsed;
        LibraryButton.Visibility = Visibility.Collapsed;
        SettingsButton.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        TitleText.Text = UiCopy.T("page.preview");
        PreviewTitle.Text = title;
        ApplyWindowSize(960, 720);
        var opened = new TaskCompletionSource<bool>();
        MediaPlayer? player = null;
        void OnOpened(MediaPlayer sender, object args) => opened.TrySetResult(true);
        void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => opened.TrySetResult(false);
        try
        {
            player = PreviewPlayer.MediaPlayer ?? new MediaPlayer();
            if (PreviewPlayer.MediaPlayer is null)
            {
                PreviewPlayer.SetMediaPlayer(player);
            }

            player.MediaOpened += OnOpened;
            player.MediaFailed += OnFailed;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                PreviewPlayer.Source = MediaSource.CreateFromStorageFile(file);
            }
            catch (Exception)
            {
                PreviewPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
            }

            var finished = await Task.WhenAny(opened.Task, Task.Delay(TimeSpan.FromSeconds(20)));
            if (_windowConcealed)
            {
                HideBusyMask();
                return;
            }

            if (finished != opened.Task || !opened.Task.Result)
            {
                HideBusyMask();
                LibraryViewer.Visibility = Visibility.Collapsed;
                LibraryListPane.Visibility = Visibility.Visible;
                TitleText.Text = UiCopy.T("page.library");
                ShowBanner(UiCopy.T("lib.no.preview"), InfoBarSeverity.Error);
                return;
            }

            LibraryViewer.Visibility = Visibility.Visible;
            HideBusyMask();
            player.Play();
        }
        catch (Exception ex)
        {
            HideBusyMask();
            ShowBanner(UiCopy.Tf("lib.preview.fail", ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            if (player is not null)
            {
                player.MediaOpened -= OnOpened;
                player.MediaFailed -= OnFailed;
            }
        }
    }

    private void ClosePreview()
    {
        if (_previewFullScreen)
        {
            _previewFullScreen = false;
            try
            {
                PreviewPlayer.IsFullWindow = false;
                AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
                if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.IsMaximizable = false;
                }
            }
            catch (ArgumentException)
            {
            }

            PreviewFullScreenButton.Label = UiCopy.T("lib.fullscreen");
        }

        HideBusyMask();
        PreviewPlayer.MediaPlayer?.Pause();
        PreviewPlayer.Source = null;
        LibraryViewer.Visibility = Visibility.Collapsed;
        LibraryListPane.Visibility = Visibility.Visible;
        PreviewPlayer.IsFullWindow = false;
        if (_page == "library")
        {
            TitleText.Text = UiCopy.T("page.library");
            ApplyWindowSize(760, 720);
        }
    }

    private bool _previewFullScreen;

    private void PreviewFullScreen_Click(object sender, RoutedEventArgs e)
    {
        _previewFullScreen = !_previewFullScreen;
        try
        {
            if (PreviewPlayer.IsFullWindow != _previewFullScreen)
            {
                PreviewPlayer.IsFullWindow = _previewFullScreen;
            }

            AppWindow.SetPresenter(_previewFullScreen
                ? Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen
                : Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
            if (!_previewFullScreen && AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                ApplyWindowSize(960, 720);
            }

            PreviewFullScreenButton.Label = _previewFullScreen ? UiCopy.T("lib.fullscreen.exit") : UiCopy.T("lib.fullscreen");
        }
        catch (Exception ex)
        {
            _previewFullScreen = false;
            ShowBanner("无法切换全屏：" + ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        var box = new TextBox { Text = item.Name };
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.rename"),
            Content = box,
            PrimaryButtonText = UiCopy.T("common.ok"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        ContentDialogResult renameChoice;
        try
        {
            renameChoice = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
            return;
        }

        if (renameChoice != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(box.Text)) return;
        _library.Rename(item.Path, box.Text.Trim());
        RefreshLibrary();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var items = LibraryList.SelectedItems.OfType<LibraryItem>().ToArray();
        if (items.Length == 0) return;
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.delete.title"),
            Content = UiCopy.Tf(items.Length == 1 ? "lib.delete.one" : "lib.delete.many", items.Length == 1 ? items[0].Name : items.Length),
            PrimaryButtonText = UiCopy.T("lib.delete"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        ContentDialogResult deleteChoice;
        try
        {
            deleteChoice = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
            return;
        }

        if (deleteChoice != ContentDialogResult.Primary) return;
        foreach (var item in items) _library.Delete(item.Path);
        RefreshLibrary();
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Settings.SaveFolder);
        if (SelectedItem() is { } item)
        {
            ProcessStart(item.Path);
            return;
        }

        await Launcher.LaunchFolderPathAsync(Settings.SaveFolder);
    }

    private static void ProcessStart(string path)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    private async void RefreshMetadata_Click(object sender, RoutedEventArgs e)
    {
        RefreshLibrary();
        await Task.CompletedTask;
    }

    private async void Repair_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        await RunJob(UiCopy.T("lib.repairing"), () => _editor.RepairAsync(item.Path), item.Path);
    }

    private async void Compress_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.compress"),
            Content = UiCopy.T("lib.compress.body"),
            PrimaryButtonText = UiCopy.T("lib.compress.export"),
            SecondaryButtonText = UiCopy.T("lib.compress.replace"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.None) return;
        await RunJob(UiCopy.T("lib.compressing"), () => _editor.CompressAsync(item.Path, choice == ContentDialogResult.Secondary), item.Path);
    }

    private async void Trim_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        var start = new NumberBox { Header = UiCopy.T("lib.trim.start"), Value = 0, Minimum = 0 };
        var end = new NumberBox { Header = UiCopy.T("lib.trim.end"), Value = 10, Minimum = 0 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(start);
        panel.Children.Add(end);
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.trim"),
            Content = panel,
            PrimaryButtonText = UiCopy.T("common.ok"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            await RunJob(UiCopy.T("lib.trimming"), () => _editor.TrimAsync(item.Path, TimeSpan.FromSeconds(start.Value), TimeSpan.FromSeconds(end.Value)), item.Path);
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        var items = LibraryList.SelectedItems.OfType<LibraryItem>().Select(item => item.Path).ToArray();
        if (items.Length < 2) return;
        if (items.Any(path => _session.Phase is SessionPhase.Recording or SessionPhase.Paused && string.Equals(path, _lastFile, StringComparison.OrdinalIgnoreCase)))
        {
            ShowBanner("正在录制的文件不能做后期。请选择更早的成片，当前录制会继续。", InfoBarSeverity.Warning);
            return;
        }

        await RunJob(UiCopy.T("lib.merging"), () => _editor.MergeAsync(items));
    }

    private async void Subtitle_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        var box = new TextBox { PlaceholderText = UiCopy.T("lib.sub.ph") };
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.sub.title"),
            Content = box,
            PrimaryButtonText = UiCopy.T("lib.sub.text"),
            SecondaryButtonText = UiCopy.T("lib.sub.file"),
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

        if (choice == ContentDialogResult.None)
        {
            return;
        }

        if (choice == ContentDialogResult.Secondary)
        {
            var file = NativeFilePicker.Pick(WindowNative.GetWindowHandle(this), UiCopy.T("lib.sub.file"), UiCopy.T("lib.subtitle"), "*.srt;*.ass");
            if (file is null)
            {
                return;
            }

            await RunJob(UiCopy.T("lib.subbing"), () => _editor.BurnSubtitlesAsync(item.Path, file), item.Path);
            return;
        }

        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowBanner(UiCopy.T("lib.sub.ph"), InfoBarSeverity.Warning);
            return;
        }

        await RunJob(UiCopy.T("lib.subbing"), () => _editor.BurnCaptionTextAsync(item.Path, text), item.Path);
    }

    private async void Music_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem() is not { } item) return;
        var file = NativeFilePicker.Pick(WindowNative.GetWindowHandle(this), UiCopy.T("lib.music"), UiCopy.T("lib.music"), "*.mp3;*.m4a;*.wav");
        if (file is null) return;
        var volume = new Slider { Header = UiCopy.T("lib.music.vol"), Value = 40, Minimum = 1, Maximum = 100 };
        var dialog = new ContentDialog
        {
            Title = UiCopy.T("lib.music"),
            Content = volume,
            PrimaryButtonText = UiCopy.T("lib.export"),
            CloseButtonText = UiCopy.T("common.cancel"),
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await RunJob(UiCopy.T("lib.mixing"), () => _editor.MixMusicAsync(item.Path, file, volume.Value / 100), item.Path);
    }

    private async Task RunJob(string working, Func<Task<string>> work, string? sourcePath = null)
    {
        if (sourcePath is not null
            && _session.Phase is SessionPhase.Recording or SessionPhase.Paused
            && string.Equals(sourcePath, _lastFile, StringComparison.OrdinalIgnoreCase))
        {
            ShowBanner("正在录制的文件不能做后期。请选择更早的成片，当前录制会继续。", InfoBarSeverity.Warning);
            return;
        }

        ShowBanner(working);
        try
        {
            var output = await work();
            RefreshLibrary();
            ShowBanner("已完成 " + Path.GetFileName(output), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowBanner(ex.Message, InfoBarSeverity.Error);
        }
    }
}
