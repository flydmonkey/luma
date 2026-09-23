using System.Drawing;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Luma.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly DispatcherQueue _dispatcher;
    private readonly MenuFlyoutItem _openItem;
    private readonly MenuFlyoutItem _libraryItem;
    private readonly MenuFlyoutItem _startItem;
    private readonly MenuFlyoutItem _pauseItem;
    private readonly MenuFlyoutItem _stopItem;
    private readonly MenuFlyoutItem _exitItem;
    private readonly Relay _startCommand;
    private readonly Relay _pauseCommand;
    private readonly Relay _stopCommand;
    private bool _recording;
    private bool _paused;
    private Icon? _trayIcon;

    public TrayService(DispatcherQueue dispatcher, Action showMain, Action showLibrary, Action exit, Action start, Action pause, Action stop)
    {
        _dispatcher = dispatcher;
        _startCommand = new Relay(OnUi(start));
        _pauseCommand = new Relay(OnUi(pause));
        _stopCommand = new Relay(OnUi(stop));
        _openItem = Item(UiCopy.T("tray.open"), new Relay(OnUi(showMain)));
        _libraryItem = Item(UiCopy.T("tray.library"), new Relay(OnUi(showLibrary)));
        _startItem = Item(UiCopy.T("tray.start"), _startCommand);
        _pauseItem = Item(UiCopy.T("tray.pause"), _pauseCommand);
        _stopItem = Item(UiCopy.T("tray.stop"), _stopCommand);
        _exitItem = Item(UiCopy.T("tray.exit"), new Relay(OnUi(exit)));
        _icon = new TaskbarIcon
        {
            ToolTipText = "Luma",
            ContextMenuMode = ContextMenuMode.PopupMenu,
            NoLeftClickDelay = true,
            LeftClickCommand = new Relay(OnUi(showMain)),
            ContextFlyout = new MenuFlyout
            {
                Items = { _openItem, _libraryItem, _startItem, _pauseItem, _stopItem, _exitItem }
            }
        };
        ApplySession(false, false);
        try { _icon.ForceCreate(false); } catch { }
    }

    public void Attach(XamlRoot? root)
    {
        if (root is not null && _icon.ContextFlyout is { } flyout)
        {
            flyout.XamlRoot = root;
        }
    }

    public void SetSession(bool recording, bool paused)
    {
        if (_dispatcher.HasThreadAccess)
        {
            ApplySession(recording, paused);
            return;
        }

        _dispatcher.TryEnqueue(() => ApplySession(recording, paused));
    }

    public void ApplyLanguage()
    {
        void Go()
        {
            _openItem.Text = UiCopy.T("tray.open");
            _libraryItem.Text = UiCopy.T("tray.library");
            _startItem.Text = UiCopy.T("tray.start");
            _stopItem.Text = UiCopy.T("tray.stop");
            _exitItem.Text = UiCopy.T("tray.exit");
            ApplySession(_recording, _paused);
        }

        if (_dispatcher.HasThreadAccess) Go();
        else _dispatcher.TryEnqueue(Go);
    }

    public void ApplyVisibility(bool hideIcon)
    {
        _icon.Visibility = hideIcon ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Dispose()
    {
        try { _icon.Dispose(); } catch { }
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void ApplySession(bool recording, bool paused)
    {
        _recording = recording;
        _paused = paused;
        _startCommand.SetCanExecute(!recording);
        _pauseCommand.SetCanExecute(recording);
        _stopCommand.SetCanExecute(recording);
        _startItem.IsEnabled = !recording;
        _pauseItem.IsEnabled = recording;
        _stopItem.IsEnabled = recording;
        _pauseItem.Text = paused ? UiCopy.T("tray.resume") : UiCopy.T("tray.pause");
        _icon.ToolTipText = recording
            ? (paused ? UiCopy.T("tray.resume") : UiCopy.T("tray.recording"))
            : "Luma";
        TrySetIcon(recording, paused);
    }

    private Action OnUi(Action action) => () =>
    {
        if (_dispatcher.HasThreadAccess) action();
        else _dispatcher.TryEnqueue(() => action());
    };

    private static MenuFlyoutItem Item(string text, ICommand command) => new()
    {
        Text = text,
        Command = command
    };

    private void TrySetIcon(bool recording, bool paused)
    {
        try
        {
            var back = paused
                ? Color.FromArgb(180, 120, 0)
                : recording
                    ? Color.FromArgb(196, 43, 28)
                    : Color.FromArgb(28, 28, 28);
            var dot = recording || paused ? Color.White : Color.Gainsboro;
            using var bitmap = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(back);
            using var brush = new SolidBrush(dot);
            g.FillEllipse(brush, 4, 4, 8, 8);
            var handle = bitmap.GetHicon();
            var next = Icon.FromHandle(handle);
            _icon.UpdateIcon(next);
            var previous = _trayIcon;
            _trayIcon = next;
            previous?.Dispose();
        }
        catch
        {
            _icon.ToolTipText = "Luma";
        }
    }

    private sealed class Relay : ICommand
    {
        private readonly Action _action;
        private bool _canExecute = true;

        public Relay(Action action) => _action = action;

        public bool CanExecute(object? parameter) => _canExecute;

        public void SetCanExecute(bool value)
        {
            if (_canExecute == value)
            {
                return;
            }

            _canExecute = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Execute(object? parameter)
        {
            if (_canExecute)
            {
                _action();
            }
        }

        public event EventHandler? CanExecuteChanged;
    }
}
