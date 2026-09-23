using Luma.App.Services;
using Luma.Core.Session;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using WinRT.Interop;

namespace Luma.App.Views;

public sealed partial class RegionPickerWindow : Window
{
    private readonly TaskCompletionSource<CaptureTarget?> _pick = new();
    private readonly DisplayInfo _display;
    private readonly string? _shot;
    private Windows.Foundation.Point _start;
    private bool _dragging;
    private Windows.Foundation.Rect _selection;

    public RegionPickerWindow(DisplayInfo display, string? screenshotPath)
    {
        InitializeComponent();
        _display = display;
        _shot = screenshotPath;
        Title = UiCopy.T("pick.region");
        HintText.Text = UiCopy.T("pick.region.hint");
        ConfirmButton.Content = UiCopy.T("common.ok");
        CancelButton.Content = UiCopy.T("common.cancel");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        AppWindow.Move(new PointInt32(display.X, display.Y));
        AppWindow.Resize(new SizeInt32(Math.Max(display.Width, 1), Math.Max(display.Height, 1)));
        if (!string.IsNullOrWhiteSpace(_shot) && File.Exists(_shot))
        {
            DesktopImage.Source = new BitmapImage(new Uri(_shot));
        }

        Closed += (_, _) =>
        {
            _pick.TrySetResult(null);
            TryDeleteShot();
        };
        Activated += (_, _) =>
        {
            try
            {
                Root.Focus(FocusState.Programmatic);
            }
            catch
            {
                // The root is not ready on the first activation.
            }
        };
    }

    public Task<CaptureTarget?> PickAsync() => _pick.Task;

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        _start = e.GetCurrentPoint(Root).Position;
        _selection = new Windows.Foundation.Rect(_start.X, _start.Y, 0, 0);
        Rubber.Visibility = Visibility.Visible;
        Root.CapturePointer(e.Pointer);
        UpdateRubber();
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var point = e.GetCurrentPoint(Root).Position;
        var x = Math.Min(_start.X, point.X);
        var y = Math.Min(_start.Y, point.Y);
        _selection = new Windows.Foundation.Rect(x, y, Math.Abs(point.X - _start.X), Math.Abs(point.Y - _start.Y));
        UpdateRubber();
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Root.ReleasePointerCapture(e.Pointer);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Confirm();

    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _pick.TrySetResult(null);
        Close();
    }

    private void Toolbar_PointerPressed(object sender, PointerRoutedEventArgs e) => e.Handled = true;

    private void Confirm()
    {
        if (_selection.Width < 8 || _selection.Height < 8)
        {
            return;
        }

        var scale = Root.XamlRoot?.RasterizationScale ?? 1;
        if (scale <= 0)
        {
            scale = 1;
        }

        var target = new CaptureTarget
        {
            Mode = CaptureMode.Region,
            MonitorIndex = _display.Index,
            CropX = (int)(_selection.X * scale),
            CropY = (int)(_selection.Y * scale),
            CropWidth = (int)(_selection.Width * scale),
            CropHeight = (int)(_selection.Height * scale)
        };
        _pick.TrySetResult(target);
        Close();
    }

    private void UpdateRubber()
    {
        Rubber.Margin = new Thickness(_selection.X, _selection.Y, 0, 0);
        Rubber.Width = Math.Max(0, _selection.Width);
        Rubber.Height = Math.Max(0, _selection.Height);
        RubberSize.Margin = new Thickness(_selection.X, Math.Max(0, _selection.Y - 22), 0, 0);
        RubberSize.Text = $"{(int)_selection.Width}×{(int)_selection.Height}";
        RubberSize.Visibility = _selection.Width > 8 ? Visibility.Visible : Visibility.Collapsed;
        SizeText.Text = RubberSize.Text;
    }

    private void TryDeleteShot()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_shot) && File.Exists(_shot))
            {
                File.Delete(_shot);
            }
        }
        catch
        {
            // temp screenshot can stay
        }
    }
}
