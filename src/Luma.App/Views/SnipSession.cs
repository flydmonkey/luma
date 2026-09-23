using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Luma.App.Services;
using Luma.Core.Capture;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Luma.App.Views;

public enum SnipKind
{
    Canceled,
    Saved,
    Copied,
    SaveFailed,
    CopyFailed
}

public sealed record SnipResult(SnipKind Kind, string? Path, string? Message);

public enum SnipPen
{
    None,
    Pen,
    Rectangle,
    Ellipse,
    Arrow,
    Text
}

public sealed class SnipSession
{
    private readonly TaskCompletionSource<SnipResult> _done = new();
    private readonly List<SnipOverlayWindow> _windows = [];
    private readonly HashSet<nint> _hwnds = [];
    private SnipTool _tool = SnipTool.Region;
    private bool _dragging;
    private bool _busy;
    private int _anchorX;
    private int _anchorY;
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private bool _hasSelection;
    private readonly List<SnipMark> _marks = [];
    private readonly List<int> _stroke = [];
    private SnipMark? _draft;
    private SnipPen _pen;
    private int _color = SnipMark.DefaultColor;
    private bool _inking;
    private bool _recognizing;

    public Task<SnipResult> RunAsync(string saveFolder, nint owner)
    {
        _saveFolder = saveFolder;
        _owner = owner;
        var displays = DisplayCatalog.ListDisplays();
        try
        {
            _frame = StillShot.CaptureDesktop(displays.Select(display => (display.X, display.Y, display.Width, display.Height)).ToArray());
        }
        catch (Exception ex)
        {
            _done.TrySetResult(new SnipResult(SnipKind.SaveFailed, null, string.IsNullOrWhiteSpace(ex.Message) ? UiCopy.T("shot.save.fail") : UiCopy.T("shot.save.fail")));
            return _done.Task;
        }

        foreach (var display in displays)
        {
            var preview = _frame.Slice(display.X, display.Y, display.Width, display.Height);
            var window = new SnipOverlayWindow(display, preview);
            window.ToolPicked += tool => SetTool(tool);
            window.PenPicked += PickPen;
            window.ColorPicked += PickColor;
            window.TextCommitted += RememberText;
            window.PointerAt += OnPointer;
            window.ActionPicked += save => _ = CommitAsync(save);
            window.OcrPicked += () => _ = RecognizeAsync();
            window.CancelRequested += Cancel;
            _windows.Add(window);
            _hwnds.Add(window.Hwnd);
        }

        foreach (var window in _windows)
        {
            window.ShowOverlay();
        }

        return _done.Task;
    }

    private string _saveFolder = "";
    private nint _owner;
    private StillFrame? _frame;

    private void SetTool(SnipTool tool)
    {
        if (_busy)
        {
            return;
        }

        _tool = tool;
        _dragging = false;
        _hasSelection = false;
        ClearMarks();
        foreach (var window in _windows)
        {
            window.MarkTool(tool);
            window.Paint(0, 0, 0, 0, false);
        }
    }

    private void PickPen(SnipPen pen)
    {
        if (_busy || !_hasSelection)
        {
            return;
        }

        _pen = _pen == pen ? SnipPen.None : pen;
        _inking = false;
        _draft = null;
        _stroke.Clear();
        foreach (var window in _windows)
        {
            window.SetPen(_pen);
        }

        ShowInk();
        if (_pen == SnipPen.Text)
        {
            var x = Math.Clamp(_x + 24, _x, Math.Max(_x, _x + _width - 8));
            var y = Math.Clamp(_y + 24, _y, Math.Max(_y, _y + _height - 8));
            WindowAt(x, y)?.BeginText(x, y);
        }
    }

    private void PickColor(int argb)
    {
        _color = argb;
        foreach (var window in _windows)
        {
            window.SetColor(argb);
        }
    }

    private void RememberText(int x, int y, string text, float fontSize, int argb)
    {
        _marks.Add(new SnipMark(SnipMarkKind.Text, x, y, x, y, text, fontSize, argb));
        ShowInk();
    }

    private void OnPointer(int x, int y, SnipPointer phase)
    {
        if (_busy)
        {
            return;
        }

        if (_hasSelection && _pen != SnipPen.None)
        {
            OnInk(x, y, phase);
            return;
        }

        if (_tool == SnipTool.Region)
        {
            OnRegion(x, y, phase);
            return;
        }

        if (phase == SnipPointer.Move && _hasSelection)
        {
            return;
        }

        var rect = _tool == SnipTool.Window ? WindowRect(x, y) : MonitorRect(x, y);
        if (rect is null)
        {
            if (phase != SnipPointer.Up)
            {
                Paint(0, 0, 0, 0, false);
            }

            return;
        }

        var showActions = phase == SnipPointer.Up && StillShot.FormsRegion(rect.Value.Width, rect.Value.Height);
        if (showActions)
        {
            _hasSelection = true;
        }

        Remember(rect.Value);
        Paint(rect.Value.X, rect.Value.Y, rect.Value.Width, rect.Value.Height, showActions);
    }

    private void OnRegion(int x, int y, SnipPointer phase)
    {
        if (phase == SnipPointer.Down)
        {
            _dragging = true;
            _hasSelection = false;
            ClearMarks();
            _anchorX = x;
            _anchorY = y;
            Paint(0, 0, 0, 0, false);
            return;
        }

        if (!_dragging && phase == SnipPointer.Move)
        {
            return;
        }

        var left = Math.Min(_anchorX, x);
        var top = Math.Min(_anchorY, y);
        var width = Math.Abs(x - _anchorX);
        var height = Math.Abs(y - _anchorY);
        if (phase == SnipPointer.Up)
        {
            _dragging = false;
            if (!StillShot.FormsRegion(width, height))
            {
                Cancel();
                return;
            }

            _hasSelection = true;
            Remember(new ScreenRect(left, top, width, height));
            Paint(left, top, width, height, true);
            return;
        }

        Paint(left, top, width, height, false);
    }

    private void Remember(ScreenRect rect)
    {
        _x = rect.X;
        _y = rect.Y;
        _width = rect.Width;
        _height = rect.Height;
    }

    private void OnInk(int x, int y, SnipPointer phase)
    {
        if (_pen == SnipPen.Text)
        {
            if (phase == SnipPointer.Up && Inside(x, y))
            {
                WindowAt(x, y)?.BeginText(x, y);
            }

            return;
        }

        if (_pen == SnipPen.Pen)
        {
            OnStroke(x, y, phase);
            return;
        }

        if (phase == SnipPointer.Down)
        {
            if (!Inside(x, y))
            {
                return;
            }

            _inking = true;
            _anchorX = x;
            _anchorY = y;
            _draft = Shape(_anchorX, _anchorY, x, y);
            ShowInk();
            return;
        }

        if (!_inking)
        {
            return;
        }

        if (phase == SnipPointer.Move)
        {
            _draft = Shape(_anchorX, _anchorY, x, y);
            ShowInk();
            return;
        }

        _inking = false;
        var mark = Shape(_anchorX, _anchorY, x, y);
        _draft = null;
        if (Math.Abs(mark.X2 - mark.X1) >= 4 || Math.Abs(mark.Y2 - mark.Y1) >= 4)
        {
            _marks.Add(mark);
        }

        ShowInk();
    }

    private SnipMark Shape(int x1, int y1, int x2, int y2)
    {
        var kind = _pen switch
        {
            SnipPen.Ellipse => SnipMarkKind.Ellipse,
            SnipPen.Arrow => SnipMarkKind.Arrow,
            _ => SnipMarkKind.Rectangle
        };
        return new SnipMark(kind, x1, y1, x2, y2, null, 0, _color);
    }

    private void OnStroke(int x, int y, SnipPointer phase)
    {
        if (phase == SnipPointer.Down)
        {
            if (!Inside(x, y))
            {
                return;
            }

            _inking = true;
            _stroke.Clear();
            _stroke.Add(x);
            _stroke.Add(y);
            _draft = StrokeMark();
            ShowInk();
            return;
        }

        if (!_inking)
        {
            return;
        }

        if (phase == SnipPointer.Move)
        {
            var dx = x - _stroke[^2];
            var dy = y - _stroke[^1];
            if (dx * dx + dy * dy < 4)
            {
                return;
            }

            _stroke.Add(x);
            _stroke.Add(y);
            _draft = StrokeMark();
            ShowInk();
            return;
        }

        _inking = false;
        if (_stroke.Count >= 2)
        {
            _marks.Add(StrokeMark());
        }

        _stroke.Clear();
        _draft = null;
        ShowInk();
    }

    private SnipMark StrokeMark() =>
        new(SnipMarkKind.Pen, _stroke[0], _stroke[1], _stroke[^2], _stroke[^1], null, 0, _color, _stroke.ToArray());

    private bool Inside(int x, int y) => x >= _x && y >= _y && x < _x + _width && y < _y + _height;

    private SnipOverlayWindow? WindowAt(int x, int y)
    {
        foreach (var window in _windows)
        {
            if (window.ContainsScreen(x, y))
            {
                return window;
            }
        }

        return _windows.FirstOrDefault();
    }

    private void ClearMarks()
    {
        _marks.Clear();
        _stroke.Clear();
        _draft = null;
        _inking = false;
        _pen = SnipPen.None;
        foreach (var window in _windows)
        {
            window.SetPen(SnipPen.None);
            window.RenderMarks(_marks, null);
        }
    }

    private void ShowInk()
    {
        foreach (var window in _windows)
        {
            window.RenderMarks(_marks, _draft);
        }
    }

    private void Paint(int x, int y, int width, int height, bool actions)
    {
        foreach (var window in _windows)
        {
            window.Paint(x, y, width, height, actions);
        }
    }

    private ScreenRect? WindowRect(int x, int y)
    {
        var hwnd = TopWindowAt(x, y, _hwnds);
        if (hwnd == 0 || !GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (!StillShot.FormsRegion(width, height))
        {
            return null;
        }

        return new ScreenRect(rect.Left, rect.Top, width, height);
    }

    private static ScreenRect? MonitorRect(int x, int y)
    {
        foreach (var display in DisplayCatalog.ListDisplays())
        {
            if (x >= display.X && y >= display.Y && x < display.X + display.Width && y < display.Y + display.Height)
            {
                return new ScreenRect(display.X, display.Y, display.Width, display.Height);
            }
        }

        return null;
    }

    private async Task CommitAsync(bool save)
    {
        if (_busy || !_hasSelection || !StillShot.FormsRegion(_width, _height))
        {
            return;
        }

        _busy = true;
        var x = _x;
        var y = _y;
        var width = _width;
        var height = _height;
        foreach (var window in _windows)
        {
            window.FlushText();
        }

        CloseWindows();
        SnipResult result;
        try
        {
            var png = _frame?.CropPng(x, y, width, height, _marks) ?? throw new InvalidOperationException("没有可裁切的画面。");
            if (save)
            {
                var path = NativeFilePicker.Save(
                    SaveOwner(),
                    UiCopy.T("shot.save.title"),
                    UiCopy.T("shot.save.filter"),
                    "*.png",
                    StillShot.FileName(DateTime.Now),
                    _saveFolder);
                if (path is null)
                {
                    result = new SnipResult(SnipKind.Canceled, null, null);
                }
                else
                {
                    StillShot.WritePng(path, png);
                    result = new SnipResult(SnipKind.Saved, path, UiCopy.T("shot.saved"));
                }
            }
            else
            {
                await StillShotClipboard.CopyPngAsync(png);
                result = new SnipResult(SnipKind.Copied, null, UiCopy.T("shot.copied"));
            }
        }
        catch (Exception ex)
        {
            var message = save ? UiCopy.T("shot.save.fail") : UiCopy.T("shot.copy.fail");
            result = new SnipResult(save ? SnipKind.SaveFailed : SnipKind.CopyFailed, null, string.IsNullOrWhiteSpace(ex.Message) ? message : message);
        }

        _frame?.Dispose();
        _frame = null;
        _done.TrySetResult(result);
    }

    private async Task RecognizeAsync()
    {
        if (_recognizing || _busy || !_hasSelection || _frame is null || !StillShot.FormsRegion(_width, _height))
        {
            return;
        }

        _recognizing = true;
        foreach (var window in _windows)
        {
            window.FlushText();
            window.SetOcrBusy(true);
        }

        string? text = null;
        string message;
        try
        {
            var pixels = _frame.Slice(_x, _y, _width, _height);
            text = await StillShotOcr.RecognizeAsync(pixels);
            if (_busy || _windows.Count == 0)
            {
                return;
            }

            if (text is null)
            {
                message = UiCopy.T("shot.ocr.unavailable");
                text = null;
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                message = UiCopy.T("shot.ocr.empty");
                text = null;
            }
            else
            {
                StillShotClipboard.CopyText(text);
                message = UiCopy.T("shot.ocr.copied");
            }
        }
        catch
        {
            if (_busy || _windows.Count == 0)
            {
                return;
            }

            message = UiCopy.T("shot.ocr.fail");
            text = null;
        }
        finally
        {
            _recognizing = false;
            foreach (var window in _windows)
            {
                window.SetOcrBusy(false);
            }
        }

        foreach (var window in _windows)
        {
            window.ShowOcr(text, message);
        }
    }

    private nint SaveOwner()
    {
        if (_owner == 0 || !GetWindowRect(_owner, out var rect) || rect.Left <= -16000)
        {
            return 0;
        }

        return _owner;
    }

    private void Cancel()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        CloseWindows();
        _frame?.Dispose();
        _frame = null;
        _done.TrySetResult(new SnipResult(SnipKind.Canceled, null, null));
    }

    private void CloseWindows()
    {
        foreach (var window in _windows)
        {
            window.CloseOverlay();
        }

        _windows.Clear();
    }

    private static nint TopWindowAt(int x, int y, HashSet<nint> ignore)
    {
        nint found = 0;
        EnumWindows((hwnd, _) =>
        {
            if (ignore.Contains(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindow(hwnd, 4) != 0)
            {
                return true;
            }

            if (DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return true;
            }

            var style = GetWindowLong(hwnd, -20);
            if ((style & 0x80) != 0)
            {
                return true;
            }

            if (!GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            if (x < rect.Left || y < rect.Top || x >= rect.Right || y >= rect.Bottom)
            {
                return true;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (!StillShot.FormsRegion(width, height))
            {
                return true;
            }

            var length = GetWindowTextLength(hwnd);
            if (length <= 0)
            {
                return true;
            }

            found = hwnd;
            return false;
        }, 0);
        return found;
    }

    private readonly record struct ScreenRect(int X, int Y, int Width, int Height);

    private delegate bool EnumProc(nint hwnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, nint lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(nint hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);
}

public enum SnipTool
{
    Region,
    Window,
    Display
}

public enum SnipPointer
{
    Down,
    Move,
    Up
}

public sealed class SnipOverlayWindow : Window
{
    private readonly DisplayInfo _display;
    private readonly Grid _root;
    private readonly Canvas _canvas;
    private readonly Border _dimTop;
    private readonly Border _dimLeft;
    private readonly Border _dimRight;
    private readonly Border _dimBottom;
    private readonly Border _frame;
    private readonly StackPanel _tools;
    private readonly Border _actions;
    private readonly Canvas _ink;
    private readonly TextBox _editor;
    private readonly Button _regionButton;
    private readonly Button _windowButton;
    private readonly Button _displayButton;
    private readonly Button _brushPen;
    private readonly Button _rectPen;
    private readonly Button _circlePen;
    private readonly Button _arrowPen;
    private readonly Button _textPen;
    private readonly Button _ocrButton;
    private Flyout? _ocrFlyout;
    private readonly Dictionary<Button, Border> _plates = [];
    private readonly Border _colorSwatch;
    private readonly Dictionary<int, Border> _menuSwatches = [];
    private int _color = SnipMark.DefaultColor;
    private readonly SolidColorBrush _inkBrush = new(Color.FromArgb(255, 232, 17, 35));
    private readonly SolidColorBrush _penOn = new(Color.FromArgb(255, 68, 68, 68));
    private readonly SolidColorBrush _penOff = new(Color.FromArgb(0, 0, 0, 0));
    private bool _allowClose;
    private bool _flushing;
    private uint _dpi = 96;
    private int _textX;
    private int _textY;
    private SnipPen _pen;

    public nint Hwnd { get; }
    public uint DisplayAffinity { get; private set; }

    public event Action<SnipTool>? ToolPicked;
    public event Action<SnipPen>? PenPicked;
    public event Action<int>? ColorPicked;
    public event Action<int, int, string, float, int>? TextCommitted;
    public event Action<int, int, SnipPointer>? PointerAt;
    public event Action<bool>? ActionPicked;
    public event Action? OcrPicked;
    public event Action? CancelRequested;

    public SnipOverlayWindow(DisplayInfo display, StillPixels preview)
    {
        _display = display;
        Title = "Luma.Snip";
        AppIcon.Apply(this);
        AppWindow.IsShownInSwitchers = false;
        SystemBackdrop = null;
        Hwnd = WindowNative.GetWindowHandle(this);
        var dim = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0));
        var clear = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        _dimTop = Block(dim);
        _dimLeft = Block(dim);
        _dimRight = Block(dim);
        _dimBottom = Block(dim);
        _frame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            Visibility = Visibility.Collapsed
        };
        _canvas = new Canvas();
        _canvas.Children.Add(new Border { Background = clear });
        _canvas.Children.Add(_dimTop);
        _canvas.Children.Add(_dimLeft);
        _canvas.Children.Add(_dimRight);
        _canvas.Children.Add(_dimBottom);
        _canvas.Children.Add(_frame);
        _ink = new Canvas { IsHitTestVisible = false };
        _editor = new TextBox
        {
            Visibility = Visibility.Collapsed,
            MinWidth = 96,
            FontSize = 18,
            Foreground = _inkBrush,
            Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
            BorderBrush = _inkBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2, 6, 2),
            PlaceholderText = UiCopy.T("shot.text.hint")
        };
        AutomationProperties.SetName(_editor, UiCopy.T("shot.text.hint"));
        Canvas.SetZIndex(_editor, 20);
        _editor.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                FlushText();
            }
        };
        _canvas.Children.Add(_ink);
        _canvas.Children.Add(_editor);
        _regionButton = ToolButton(UiCopy.T("shot.region"), SnipTool.Region);
        _windowButton = ToolButton(UiCopy.T("shot.window"), SnipTool.Window);
        _displayButton = ToolButton(UiCopy.T("shot.display"), SnipTool.Display);
        _tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 0, 0)
        };
        _tools.Children.Add(_regionButton);
        _tools.Children.Add(_windowButton);
        _tools.Children.Add(_displayButton);
        _brushPen = PenButton(PenIcon(), UiCopy.T("shot.pen"), SnipPen.Pen);
        _rectPen = PenButton(BoxIcon(), UiCopy.T("shot.box"), SnipPen.Rectangle);
        _circlePen = PenButton(CircleIcon(), UiCopy.T("shot.circle"), SnipPen.Ellipse);
        _arrowPen = PenButton(ArrowIcon(), UiCopy.T("shot.arrow"), SnipPen.Arrow);
        _textPen = PenButton(TextIcon(), UiCopy.T("shot.text"), SnipPen.Text);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(_brushPen);
        row.Children.Add(_rectPen);
        row.Children.Add(_circlePen);
        row.Children.Add(_arrowPen);
        row.Children.Add(_textPen);
        row.Children.Add(new Border
        {
            Width = 1,
            Height = 18,
            Margin = new Thickness(6, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
            VerticalAlignment = VerticalAlignment.Center
        });
        _colorSwatch = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = BrushFor(_color),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(ColorMenu());
        row.Children.Add(new Border
        {
            Width = 1,
            Height = 18,
            Margin = new Thickness(6, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
            VerticalAlignment = VerticalAlignment.Center
        });
        _ocrButton = IconButton(OcrIcon(), UiCopy.T("shot.ocr"));
        _ocrButton.Click += (_, _) => OcrPicked?.Invoke();
        row.Children.Add(_ocrButton);
        row.Children.Add(ActionButton(Icon("\uE74E"), UiCopy.T("shot.save"), true));
        row.Children.Add(ActionButton(Icon("\uE8C8"), UiCopy.T("shot.copy"), false));
        _actions = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(242, 32, 32, 32)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 2, 4, 2),
            Visibility = Visibility.Collapsed,
            Child = row
        };
        _root = new Grid
        {
            Background = new ImageBrush { ImageSource = LoadPixels(preview), Stretch = Stretch.Fill },
            IsTabStop = true,
            Children = { _canvas, _tools, _actions }
        };
        _root.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                if (_editor.Visibility == Visibility.Visible)
                {
                    DismissEditor();
                    return;
                }

                if (_ocrFlyout?.IsOpen == true)
                {
                    _ocrFlyout.Hide();
                    return;
                }

                CancelRequested?.Invoke();
            }
        };
        _root.PointerPressed += (_, e) => RaisePointer(e, SnipPointer.Down);
        _root.PointerMoved += (_, e) => RaisePointer(e, SnipPointer.Move);
        _root.PointerReleased += (_, e) => RaisePointer(e, SnipPointer.Up);
        Content = _root;
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

        try
        {
            SetWindowDisplayAffinity(Hwnd, 0x11);
            if (GetWindowDisplayAffinity(Hwnd, out var affinity))
            {
                DisplayAffinity = affinity;
            }
        }
        catch
        {
            DisplayAffinity = 0;
        }

        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose)
            {
                return;
            }

            args.Cancel = true;
            CancelRequested?.Invoke();
        };
        MarkTool(SnipTool.Region);
        Paint(0, 0, 0, 0, false);
    }

    public void ShowOverlay()
    {
        try
        {
            AppWindow.Move(new PointInt32(_display.X, _display.Y));
            AppWindow.Resize(new SizeInt32(Math.Max(1, _display.Width), Math.Max(1, _display.Height)));
        }
        catch
        {
            // A monitor at a negative origin can reject the move; the window still covers what it can.
        }

        var dpi = GetDpiForWindow(Hwnd);
        if (dpi > 0)
        {
            _dpi = dpi;
        }

        try
        {
            SetWindowDisplayAffinity(Hwnd, 0x11);
            if (GetWindowDisplayAffinity(Hwnd, out var affinity))
            {
                DisplayAffinity = affinity;
            }
        }
        catch
        {
            DisplayAffinity = 0;
        }

        Activate();
        _root.Focus(FocusState.Programmatic);
    }

    public void MarkTool(SnipTool tool)
    {
        _regionButton.Opacity = tool == SnipTool.Region ? 1 : 0.65;
        _windowButton.Opacity = tool == SnipTool.Window ? 1 : 0.65;
        _displayButton.Opacity = tool == SnipTool.Display ? 1 : 0.65;
    }

    public void Paint(int x, int y, int width, int height, bool actions)
    {
        var viewWidth = Math.Max(_root.ActualWidth, Dip(_display.Width));
        var viewHeight = Math.Max(_root.ActualHeight, Dip(_display.Height));
        if (_canvas.Children[0] is FrameworkElement backdrop)
        {
            backdrop.Width = viewWidth;
            backdrop.Height = viewHeight;
        }

        var intersects = width > 0 && height > 0
            && x < _display.X + _display.Width && x + width > _display.X
            && y < _display.Y + _display.Height && y + height > _display.Y;
        if (!intersects)
        {
            Fill(viewWidth, viewHeight);
            _frame.Visibility = Visibility.Collapsed;
            _actions.Visibility = Visibility.Collapsed;
            return;
        }

        var left = Dip(Math.Max(x, _display.X) - _display.X);
        var top = Dip(Math.Max(y, _display.Y) - _display.Y);
        var right = Dip(Math.Min(x + width, _display.X + _display.Width) - _display.X);
        var bottom = Dip(Math.Min(y + height, _display.Y + _display.Height) - _display.Y);
        Place(_dimTop, 0, 0, viewWidth, top);
        Place(_dimLeft, 0, top, left, Math.Max(0, bottom - top));
        Place(_dimRight, right, top, Math.Max(0, viewWidth - right), Math.Max(0, bottom - top));
        Place(_dimBottom, 0, bottom, viewWidth, Math.Max(0, viewHeight - bottom));
        Canvas.SetLeft(_frame, left);
        Canvas.SetTop(_frame, top);
        _frame.Width = Math.Max(0, right - left);
        _frame.Height = Math.Max(0, bottom - top);
        _frame.Visibility = Visibility.Visible;
        var showHere = actions && x + width - 1 >= _display.X && x + width - 1 < _display.X + _display.Width
            && y + height - 1 >= _display.Y && y + height - 1 < _display.Y + _display.Height;
        _actions.Visibility = showHere ? Visibility.Visible : Visibility.Collapsed;
        if (!showHere)
        {
            return;
        }

        _actions.Measure(new Size(viewWidth, viewHeight));
        var ax = Math.Min(right + 8, Math.Max(8, viewWidth - _actions.DesiredSize.Width - 8));
        var ay = bottom + 8;
        if (ay + _actions.DesiredSize.Height > viewHeight - 8)
        {
            ay = Math.Max(8, top - _actions.DesiredSize.Height - 8);
        }

        _actions.HorizontalAlignment = HorizontalAlignment.Left;
        _actions.VerticalAlignment = VerticalAlignment.Top;
        _actions.Margin = new Thickness(ax, ay, 0, 0);
    }

    public void CloseOverlay()
    {
        _allowClose = true;
        try
        {
            Close();
        }
        catch
        {
            // already closed
        }
    }

    private void Fill(double width, double height)
    {
        Place(_dimTop, 0, 0, width, height);
        Place(_dimLeft, 0, 0, 0, 0);
        Place(_dimRight, 0, 0, 0, 0);
        Place(_dimBottom, 0, 0, 0, 0);
    }

    private Button ToolButton(string label, SnipTool tool)
    {
        var button = new Button { Content = label, MinWidth = 72 };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => ToolPicked?.Invoke(tool);
        return button;
    }

    public bool ContainsScreen(int x, int y) =>
        x >= _display.X && y >= _display.Y && x < _display.X + _display.Width && y < _display.Y + _display.Height;

    public void SetColor(int argb)
    {
        _color = argb;
        _editor.Foreground = BrushFor(argb);
        _colorSwatch.Background = BrushFor(argb);
        foreach (var pair in _menuSwatches)
        {
            var selected = pair.Key == argb;
            pair.Value.BorderThickness = new Thickness(selected ? 2 : 1);
            pair.Value.BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(70, 255, 255, 255));
        }
        if (_editor.Visibility == Visibility.Visible)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (_editor.Visibility == Visibility.Visible)
                {
                    _editor.Focus(FocusState.Keyboard);
                }
            });
        }
    }

    public void SetPen(SnipPen pen)
    {
        if (pen != SnipPen.Text)
        {
            FlushText();
        }

        _pen = pen;
        Highlight(_brushPen, pen == SnipPen.Pen);
        Highlight(_rectPen, pen == SnipPen.Rectangle);
        Highlight(_circlePen, pen == SnipPen.Ellipse);
        Highlight(_arrowPen, pen == SnipPen.Arrow);
        Highlight(_textPen, pen == SnipPen.Text);
    }

    public void RenderMarks(IReadOnlyList<SnipMark> marks, SnipMark? draft)
    {
        _ink.Children.Clear();
        foreach (var mark in marks)
        {
            AddMark(mark);
        }

        if (draft is not null)
        {
            AddMark(draft);
        }
    }

    public void BeginText(int x, int y)
    {
        FlushText();
        _textX = x;
        _textY = y;
        _editor.Text = "";
        Canvas.SetLeft(_editor, Dip(x - _display.X));
        Canvas.SetTop(_editor, Dip(y - _display.Y));
        _editor.Visibility = Visibility.Visible;
        _editor.UpdateLayout();
        Activate();
        _editor.Focus(FocusState.Programmatic);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (_editor.Visibility != Visibility.Visible)
            {
                return;
            }

            Activate();
            _editor.Focus(FocusState.Keyboard);
        });
    }

    public void FlushText()
    {
        if (_flushing || _editor.Visibility != Visibility.Visible)
        {
            return;
        }

        _flushing = true;
        var text = _editor.Text;
        _editor.Visibility = Visibility.Collapsed;
        _flushing = false;
        if (!string.IsNullOrWhiteSpace(text))
        {
            TextCommitted?.Invoke(_textX, _textY, text, 18f * _dpi / 96f, _color);
        }
    }

    private void DismissEditor()
    {
        _flushing = true;
        _editor.Text = "";
        _editor.Visibility = Visibility.Collapsed;
        _flushing = false;
    }

    private void AddMark(SnipMark mark)
    {
        var x1 = Dip(mark.X1 - _display.X);
        var y1 = Dip(mark.Y1 - _display.Y);
        var x2 = Dip(mark.X2 - _display.X);
        var y2 = Dip(mark.Y2 - _display.Y);
        var brush = BrushFor(mark.Argb);
        UIElement? shape = mark.Kind switch
        {
            SnipMarkKind.Rectangle => Box(x1, y1, x2, y2, ellipse: false, brush),
            SnipMarkKind.Ellipse => Box(x1, y1, x2, y2, ellipse: true, brush),
            SnipMarkKind.Arrow => Arrow(x1, y1, x2, y2, brush),
            SnipMarkKind.Pen => Stroke(mark, brush),
            SnipMarkKind.Text when !string.IsNullOrWhiteSpace(mark.Text) => new TextBlock
            {
                Text = mark.Text,
                Foreground = brush,
                FontSize = 18,
                IsHitTestVisible = false
            },
            _ => null
        };
        if (shape is null)
        {
            return;
        }

        if (mark.Kind == SnipMarkKind.Text)
        {
            Canvas.SetLeft(shape, x1);
            Canvas.SetTop(shape, y1);
        }

        _ink.Children.Add(shape);
    }

    private UIElement Box(double x1, double y1, double x2, double y2, bool ellipse, Brush stroke)
    {
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        Shape shape = ellipse
            ? new Ellipse { Stroke = stroke, StrokeThickness = 3 }
            : new Microsoft.UI.Xaml.Shapes.Rectangle { Stroke = stroke, StrokeThickness = 3 };
        shape.Width = Math.Max(1, Math.Abs(x2 - x1));
        shape.Height = Math.Max(1, Math.Abs(y2 - y1));
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        return shape;
    }

    private UIElement Stroke(SnipMark mark, Brush stroke)
    {
        var poly = new Polyline
        {
            Stroke = stroke,
            StrokeThickness = SnipInk.PenWidth,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        if (mark.Points is { Length: >= 2 })
        {
            var points = new PointCollection();
            for (var i = 0; i + 1 < mark.Points.Length; i += 2)
            {
                points.Add(new Windows.Foundation.Point(Dip(mark.Points[i] - _display.X), Dip(mark.Points[i + 1] - _display.Y)));
            }

            poly.Points = points;
        }

        return poly;
    }

    private UIElement Arrow(double x1, double y1, double x2, double y2, Brush stroke)
    {
        var canvas = new Canvas { IsHitTestVisible = false };
        canvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        var head = 14.0;
        var spread = Math.PI / 7;
        var left = new Windows.Foundation.Point(x2 - head * Math.Cos(angle - spread), y2 - head * Math.Sin(angle - spread));
        var right = new Windows.Foundation.Point(x2 - head * Math.Cos(angle + spread), y2 - head * Math.Sin(angle + spread));
        canvas.Children.Add(new Polygon
        {
            Fill = stroke,
            Points = new PointCollection { new(x2, y2), left, right }
        });
        return canvas;
    }

    private Button ColorMenu()
    {
        var face = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        face.Children.Add(_colorSwatch);
        face.Children.Add(new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
        });
        var button = IconButton(face, UiCopy.T("shot.color"));
        button.Width = 48;
        var grid = new StackPanel { Spacing = 6 };
        StackPanel? line = null;
        var index = 0;
        foreach (var argb in SnipInk.Palette)
        {
            if (index % 4 == 0)
            {
                line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                grid.Children.Add(line);
            }

            line!.Children.Add(ColorDot(argb, button));
            index++;
        }

        button.Flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.Bottom,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Child = grid
            }
        };
        return button;
    }

    private Button ColorDot(int argb, Button owner)
    {
        var selected = argb == _color;
        var swatch = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = BrushFor(argb),
            BorderThickness = new Thickness(selected ? 2 : 1),
            BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(70, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var item = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = _penOff,
            BorderThickness = new Thickness(0),
            Content = swatch
        };
        QuietButton(item);
        item.Click += (_, _) =>
        {
            ColorPicked?.Invoke(argb);
            owner.Flyout?.Hide();
        };
        _menuSwatches[argb] = swatch;
        return item;
    }

    private static SolidColorBrush BrushFor(int argb) => new(Color.FromArgb(
        (byte)((argb >> 24) & 255),
        (byte)((argb >> 16) & 255),
        (byte)((argb >> 8) & 255),
        (byte)(argb & 255)));

    private static UIElement PenIcon()
    {
        var ink = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        return new Polyline
        {
            Width = 16,
            Height = 16,
            Stroke = ink,
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Points = new PointCollection { new(2, 13), new(6, 8), new(9, 11), new(14, 3) }
        };
    }

    private Button PenButton(UIElement icon, string label, SnipPen pen)
    {
        var button = IconButton(icon, label);
        button.Click += (_, _) => PenPicked?.Invoke(pen);
        return button;
    }

    public void SetOcrBusy(bool busy)
    {
        _ocrButton.IsEnabled = !busy;
    }

    public void ShowOcr(string? text, string message)
    {
        if (_actions.Visibility != Visibility.Visible)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 8, MaxWidth = 360 };
        stack.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
        });
        if (!string.IsNullOrEmpty(text))
        {
            stack.Children.Add(new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 200,
                MinWidth = 220,
                Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            });
        }

        _ocrFlyout ??= new Flyout
        {
            Placement = FlyoutPlacementMode.Top,
            FlyoutPresenterStyle = DarkFlyout()
        };
        _ocrFlyout.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32)),
            Padding = new Thickness(12),
            Child = stack
        };
        _ocrFlyout.ShowAt(_ocrButton);
    }

    private static Style DarkFlyout()
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(FlyoutPresenter.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 32, 32, 32))));
        style.Setters.Add(new Setter(FlyoutPresenter.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FlyoutPresenter.BorderThicknessProperty, new Thickness(0)));
        return style;
    }

    private static UIElement OcrIcon()
    {
        var ink = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        var canvas = new Canvas { Width = 16, Height = 16 };
        foreach (var y in new[] { 4.0, 8.0, 12.0 })
        {
            canvas.Children.Add(new Line
            {
                X1 = 2,
                Y1 = y,
                X2 = 14,
                Y2 = y,
                Stroke = ink,
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        return canvas;
    }

    private Button ActionButton(UIElement icon, string label, bool save)
    {
        var button = IconButton(icon, label);
        button.Click += (_, _) => ActionPicked?.Invoke(save);
        return button;
    }

    private Button IconButton(UIElement icon, string label)
    {
        var plate = new Border
        {
            Background = _penOff,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = icon
        };
        var button = new Button
        {
            Content = plate,
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = _penOff,
            BorderThickness = new Thickness(0)
        };
        var hover = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        var pressed = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        button.Resources["ButtonBackground"] = _penOff;
        button.Resources["ButtonBackgroundPointerOver"] = hover;
        button.Resources["ButtonBackgroundPressed"] = pressed;
        button.Resources["ButtonBorderBrush"] = _penOff;
        button.Resources["ButtonBorderBrushPointerOver"] = _penOff;
        button.Resources["ButtonBorderBrushPressed"] = _penOff;
        button.Resources["ButtonForeground"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        AutomationProperties.SetName(button, label);
        _plates[button] = plate;
        return button;
    }

    private void QuietButton(Button button)
    {
        var hover = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        var pressed = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        button.Resources["ButtonBackground"] = _penOff;
        button.Resources["ButtonBackgroundPointerOver"] = hover;
        button.Resources["ButtonBackgroundPressed"] = pressed;
        button.Resources["ButtonBorderBrush"] = _penOff;
        button.Resources["ButtonBorderBrushPointerOver"] = _penOff;
        button.Resources["ButtonBorderBrushPressed"] = _penOff;
        button.Resources["ButtonForeground"] = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
    }

    private void Highlight(Button button, bool on)
    {
        if (_plates.TryGetValue(button, out var plate))
        {
            plate.Background = on ? _penOn : _penOff;
        }
    }

    private static UIElement BoxIcon() => new Border
    {
        Width = 14,
        Height = 10,
        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
        BorderThickness = new Thickness(1.6)
    };

    private static UIElement CircleIcon() => new Ellipse
    {
        Width = 14,
        Height = 14,
        Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
        StrokeThickness = 1.6
    };

    private static UIElement ArrowIcon()
    {
        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(new Line
        {
            X1 = 2,
            Y1 = 13,
            X2 = 13,
            Y2 = 3,
            Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            StrokeThickness = 1.6
        });
        canvas.Children.Add(new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            Points = new PointCollection { new(13, 3), new(7, 4), new(12, 9) }
        });
        return canvas;
    }

    private static UIElement TextIcon() => new TextBlock
    {
        Text = "T",
        FontSize = 16,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static UIElement Icon(string glyph) => new FontIcon
    {
        Glyph = glyph,
        FontSize = 15,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
    };

    private void RaisePointer(PointerRoutedEventArgs e, SnipPointer phase)
    {
        if (InChrome(e.OriginalSource))
        {
            return;
        }

        if (!GetCursorPos(out var point))
        {
            return;
        }

        if (phase == SnipPointer.Down)
        {
            _root.CapturePointer(e.Pointer);
        }

        PointerAt?.Invoke(point.X, point.Y, phase);
    }

    private bool InChrome(object source)
    {
        var node = source as DependencyObject;
        while (node is not null)
        {
            if (ReferenceEquals(node, _tools) || ReferenceEquals(node, _actions) || ReferenceEquals(node, _editor))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private double Dip(int pixels) => pixels * 96.0 / _dpi;

    private static WriteableBitmap LoadPixels(StillPixels pixels)
    {
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        var length = (int)bitmap.PixelBuffer.Length;
        var rowBytes = pixels.Width * 4;
        var stride = pixels.Height == 0 ? rowBytes : length / pixels.Height;
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            if (stride == rowBytes)
            {
                stream.Write(pixels.Bgra, 0, pixels.Bgra.Length);
            }
            else
            {
                for (var row = 0; row < pixels.Height; row++)
                {
                    stream.Position = row * stride;
                    stream.Write(pixels.Bgra, row * rowBytes, rowBytes);
                }
            }
        }

        bitmap.Invalidate();
        return bitmap;
    }

    private static Border Block(Brush fill) => new() { Background = fill };

    private static void Place(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
}
