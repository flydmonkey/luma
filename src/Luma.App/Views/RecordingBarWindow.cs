using System.Runtime.InteropServices;
using Luma.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;

namespace Luma.App.Views;

public sealed class RecordingBarWindow : Window
{
    private readonly Func<Task> _pause;
    private readonly Func<Task> _stop;
    private readonly Action<bool> _mute;
    private readonly DispatcherTimer _timer;
    private readonly Func<TimeSpan> _elapsed;
    private readonly Func<bool> _isPaused;
    private readonly TextBlock _elapsedText;
    private readonly Button _pauseButton;
    private readonly Button _stopButton;
    private readonly ToggleButton _micButton;
    private readonly Ellipse _liveDot;
    private readonly Func<(double System, double Mic)> _levels;
    private readonly ProgressBar _systemMeter;
    private readonly ProgressBar _micMeter;
    private readonly bool _showMeters;
    private readonly ScreenInkOverlay? _ink;
    private readonly ToggleButton? _penButton;
    private bool? _shownPaused;
    private string? _shownMicGlyph;

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    public RecordingBarWindow(
        Func<TimeSpan> elapsed,
        Func<bool> isPaused,
        Func<Task> pause,
        Func<Task> stop,
        Action<bool> mute,
        bool micOn,
        Func<(double System, double Mic)> levels,
        bool systemOn,
        ScreenInkOverlay? ink)
    {
        _levels = levels;
        _ink = ink;
        _showMeters = micOn || systemOn;
        Title = "Luma";
        AppIcon.Apply(this);
        AppWindow.IsShownInSwitchers = false;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        _elapsed = elapsed;
        _isPaused = isPaused;
        _pause = pause;
        _stop = stop;
        _mute = mute;
        _elapsedText = new TextBlock
        {
            Text = "00:00:00",
            MinWidth = 78,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _pauseButton = IconButton(Symbol("\uE769"), UiCopy.T("bar.pause"));
        _pauseButton.Click += async (_, _) => await _pause();
        _stopButton = IconButton(Symbol("\uE71A"), UiCopy.T("bar.stop"));
        _stopButton.Click += async (_, _) => await _stop();
        _micButton = new ToggleButton
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsChecked = micOn
        };
        ApplyMicGlyph(micOn);
        _micButton.Click += (_, _) =>
        {
            var on = _micButton.IsChecked == true;
            ApplyMicGlyph(on);
            _mute(!on);
        };

        _liveDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_liveDot);
        row.Children.Add(_elapsedText);
        row.Children.Add(_pauseButton);
        row.Children.Add(_stopButton);
        row.Children.Add(_micButton);
        if (_ink is not null)
        {
            var penTip = UiCopy.T("bar.ink");
            _penButton = new ToggleButton
            {
                Width = 36,
                Height = 36,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = Symbol("\uE70F")
            };
            ToolTipService.SetToolTip(_penButton, penTip);
            AutomationProperties.SetName(_penButton, penTip);
            _penButton.Click += (_, _) => _ink.SetDrawing(_penButton.IsChecked == true);
            var clearButton = IconButton(Symbol("\uE75C"), UiCopy.T("bar.ink.clear"));
            AutomationProperties.SetName(clearButton, UiCopy.T("bar.ink.clear"));
            clearButton.Click += (_, _) => _ink.Clear();
            row.Children.Add(_penButton);
            row.Children.Add(clearButton);
        }

        _systemMeter = Meter(systemOn);
        _micMeter = Meter(micOn);
        if (_showMeters)
        {
            var meters = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            meters.Children.Add(_systemMeter);
            meters.Children.Add(_micMeter);
            var tip = UiCopy.T("bar.levels");
            ToolTipService.SetToolTip(meters, tip);
            AutomationProperties.SetName(meters, tip);
            row.Children.Add(meters);
        }

        Content = new Grid { Padding = new Thickness(14, 8, 14, 8), Children = { row } };
        AppTheme.ApplyToWindow(this, App.Settings.Theme);
        ExtendsContentIntoTitleBar = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
        }

        try { SetWindowDisplayAffinity(WindowNative.GetWindowHandle(this), 0x11); } catch { }
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) =>
        {
            _elapsedText.Text = _elapsed().ToString(@"hh\:mm\:ss");
            ApplyTransport(_isPaused());
            if (_penButton is not null && _penButton.IsChecked != _ink!.IsDrawing)
            {
                _penButton.IsChecked = _ink.IsDrawing;
            }

            if (_showMeters)
            {
                var (system, mic) = _levels();
                _systemMeter.Value = system;
                _micMeter.Value = _micButton.IsChecked == true ? mic : 0;
            }
        };
        _timer.Start();
        ApplyTransport(_isPaused());
        Closed += (_, _) => _timer.Stop();
        Reveal();
    }

    public void Reveal()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        try { ShowWindow(hwnd, 5); } catch { }
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        Place();
    }

    public void Place()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1;
        var width = (int)(((_showMeters ? 340 : 280) + (_ink is not null ? 92 : 0)) * scale);
        AppWindow.Resize(new SizeInt32(width, (int)(48 * scale)));
        var work = DisplayArea.Primary.WorkArea;
        AppWindow.Move(new PointInt32(work.X + (work.Width - width) / 2, work.Y + 8));
        Activate();
    }

    private void ApplyTransport(bool paused)
    {
        if (_shownPaused == paused)
        {
            return;
        }

        _shownPaused = paused;
        _liveDot.Fill = new SolidColorBrush(paused ? Microsoft.UI.Colors.Goldenrod : Microsoft.UI.Colors.IndianRed);
        var glyph = paused ? "\uE768" : "\uE769";
        _pauseButton.Content = Symbol(glyph);
        var tip = paused ? UiCopy.T("bar.resume") : UiCopy.T("bar.pause");
        ToolTipService.SetToolTip(_pauseButton, tip);
        AutomationProperties.SetName(_pauseButton, tip);
    }

    private static ProgressBar Meter(bool enabled) => new()
    {
        Width = 44,
        Minimum = 0,
        Maximum = 1,
        Opacity = enabled ? 1 : 0.35
    };

    private static FontIcon Symbol(string glyph) => new()
    {
        Glyph = glyph,
        FontSize = 14,
        FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"]
    };

    private static Button IconButton(FontIcon icon, string tip)
    {
        var button = new Button
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = icon
        };
        ToolTipService.SetToolTip(button, tip);
        return button;
    }

    public void ApplyTheme() => AppTheme.ApplyToWindow(this, App.Settings.Theme);

    private void ApplyMicGlyph(bool on)
    {
        var glyph = on ? "\uE1D6" : "\uF781";
        if (_shownMicGlyph != glyph)
        {
            _shownMicGlyph = glyph;
            _micButton.Content = Symbol(glyph);
        }

        var tip = on ? UiCopy.T("sum.mic.on") : UiCopy.T("sum.mic.off");
        ToolTipService.SetToolTip(_micButton, tip);
        AutomationProperties.SetName(_micButton, tip);
    }
}
