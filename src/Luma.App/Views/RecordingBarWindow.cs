using System.Runtime.InteropServices;
using Luma.App.Services;
using Luma.Core.Settings;
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
    private readonly ToggleButton? _styleButton;
    private readonly Ellipse? _styleDot;
    private readonly Button? _undoButton;
    private readonly StackPanel? _stylePanel;
    private readonly StackPanel? _inkTools;
    private readonly List<(string Color, Button Button)> _colorButtons = [];
    private readonly List<(int Width, Button Button, Ellipse Dot)> _widthButtons = [];
    private readonly Action<string, int>? _inkStyleChanged;
    private string _inkColor;
    private int _inkWidth;
    private bool? _shownPaused;
    private const int StylePanelHeight = 40;
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
        ScreenInkOverlay? ink,
        string inkColor,
        int inkWidth,
        Action<string, int>? inkStyleChanged)
    {
        _levels = levels;
        _ink = ink;
        _inkColor = inkColor;
        _inkWidth = inkWidth;
        _inkStyleChanged = inkStyleChanged;
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
            TextLineBounds = TextLineBounds.Tight,
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
            _penButton.Click += (_, _) =>
            {
                _ink.SetDrawing(_penButton.IsChecked == true);
                ShowInkTools(_ink.IsDrawing);
            };
            var styleTip = UiCopy.T("bar.ink.style");
            _styleDot = new Ellipse { Width = 14, Height = 14, StrokeThickness = 1, Stroke = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
            _styleButton = new ToggleButton
            {
                Width = 36,
                Height = 36,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = _styleDot
            };
            ToolTipService.SetToolTip(_styleButton, styleTip);
            AutomationProperties.SetName(_styleButton, styleTip);
            _styleButton.Click += (_, _) => ShowStylePanel(_styleButton.IsChecked == true);
            _undoButton = IconButton(Symbol("\uE7A7"), UiCopy.T("bar.ink.undo"));
            AutomationProperties.SetName(_undoButton, UiCopy.T("bar.ink.undo"));
            _undoButton.Click += (_, _) => _ink.Undo();
            var clearButton = IconButton(Symbol("\uE75C"), UiCopy.T("bar.ink.clear"));
            AutomationProperties.SetName(clearButton, UiCopy.T("bar.ink.clear"));
            clearButton.Click += (_, _) => _ink.Clear();
            _inkTools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Visibility = Visibility.Collapsed };
            _inkTools.Children.Add(_styleButton);
            _inkTools.Children.Add(_undoButton);
            _inkTools.Children.Add(clearButton);
            row.Children.Add(_penButton);
            row.Children.Add(_inkTools);
            _stylePanel = BuildStylePanel();
            ApplyInkStyle();
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

        var body = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(new Grid { Height = 36, Children = { row } });
        if (_stylePanel is not null)
        {
            body.Children.Add(_stylePanel);
        }

        Content = new Grid { Padding = new Thickness(14, 0, 14, 0), Children = { body } };
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
                ShowInkTools(_ink.IsDrawing);
            }

            if (_undoButton is not null && _undoButton.IsEnabled != _ink!.CanUndo)
            {
                _undoButton.IsEnabled = _ink.CanUndo;
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

    public void Place() => Place(activate: true);

    private void Place(bool activate)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        if (scale <= 0) scale = 1;
        var inkWidth = _ink is null ? 0 : 46 + (_inkTools?.Visibility == Visibility.Visible ? 138 : 0);
        var width = (int)(((_showMeters ? 340 : 280) + inkWidth) * scale);
        var height = 48 + (_stylePanel?.Visibility == Visibility.Visible ? StylePanelHeight : 0);
        AppWindow.Resize(new SizeInt32(width, (int)(height * scale)));
        var work = DisplayArea.Primary.WorkArea;
        AppWindow.Move(new PointInt32(work.X + (work.Width - width) / 2, work.Y + 8));
        if (activate)
        {
            Activate();
        }
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

    private StackPanel BuildStylePanel()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Height = StylePanelHeight - 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        foreach (var color in ScreenAidSettings.InkColors)
        {
            var button = SwatchButton(new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(ParseColor(color)),
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                StrokeThickness = 1
            });
            button.Click += (_, _) => ChooseInkStyle(color, _inkWidth);
            _colorButtons.Add((color, button));
            panel.Children.Add(button);
        }

        panel.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(4, 0, 4, 0), Background = new SolidColorBrush(Microsoft.UI.Colors.Gray) });
        foreach (var width in ScreenAidSettings.InkWidths)
        {
            var size = 4 + width * 1.5;
            var dot = new Ellipse { Width = size, Height = size };
            var button = SwatchButton(dot);
            button.Click += (_, _) => ChooseInkStyle(_inkColor, width);
            _widthButtons.Add((width, button, dot));
            panel.Children.Add(button);
        }

        return panel;
    }

    private static Button SwatchButton(UIElement content) => new()
    {
        Width = 32,
        Height = 32,
        Padding = new Thickness(0),
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderThickness = new Thickness(2),
        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        Content = content
    };

    private void ShowInkTools(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_inkTools is null || _inkTools.Visibility == visibility)
        {
            return;
        }

        _inkTools.Visibility = visibility;
        if (!show)
        {
            _stylePanel!.Visibility = Visibility.Collapsed;
            _styleButton!.IsChecked = false;
        }

        Place(activate: false);
    }

    private void ShowStylePanel(bool show)
    {
        if (_stylePanel is null)
        {
            return;
        }

        _stylePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _styleButton!.IsChecked = show;
        Place(activate: false);
    }

    private void ChooseInkStyle(string color, int width)
    {
        _inkColor = color;
        _inkWidth = width;
        ApplyInkStyle();
        _ink!.SetDrawing(true);
        _inkStyleChanged?.Invoke(color, width);
    }

    private void ApplyInkStyle()
    {
        _ink!.SetPen(_inkColor, _inkWidth);
        var fill = new SolidColorBrush(ParseColor(_inkColor));
        _styleDot!.Fill = fill;
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var (color, button) in _colorButtons)
        {
            button.BorderBrush = color == _inkColor ? accent : none;
        }

        foreach (var (width, button, dot) in _widthButtons)
        {
            dot.Fill = fill;
            button.BorderBrush = width == _inkWidth ? accent : none;
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return Windows.UI.Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
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
