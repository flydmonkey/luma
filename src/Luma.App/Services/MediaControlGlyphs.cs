using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;

namespace Luma.App.Services;

public static class MediaControlGlyphs
{
    private const string PlayGlyph = "\uE768";
    private const string PauseGlyph = "\uE769";
    private static readonly string[] ButtonNames = ["PlayPauseButton", "PlayPauseButtonOnLeft"];
    private static readonly Lazy<bool> Needed = new(() => !HasFluentIcons());
    private static readonly ConditionalWeakTable<MediaTransportControls, object> Attached = new();

    // The built-in template uses Segoe Fluent Icons-only play/pause glyphs (F5B0/F8AE), which render as boxes on Windows 10.
    public static void Attach(MediaTransportControls controls)
    {
        if (!Needed.Value || Attached.TryGetValue(controls, out _))
        {
            return;
        }

        Attached.Add(controls, new object());
        if (TryPatch(controls))
        {
            return;
        }

        void OnLayoutUpdated(object? sender, object e)
        {
            if (TryPatch(controls))
            {
                controls.LayoutUpdated -= OnLayoutUpdated;
            }
        }

        controls.LayoutUpdated += OnLayoutUpdated;
    }

    private static bool TryPatch(MediaTransportControls controls)
    {
        VisualStateGroup? group = null;
        var buttons = new List<AppBarButton>();
        foreach (var element in Descendants(controls))
        {
            if (element is AppBarButton button && ButtonNames.Contains(button.Name))
            {
                buttons.Add(button);
            }

            group ??= VisualStateManager.GetVisualStateGroups(element).FirstOrDefault(g => g.Name == "PlayPauseStates");
        }

        if (group is null || !buttons.Any(b => b.Name == ButtonNames[0]))
        {
            return false;
        }

        var icons = buttons.Select(button =>
        {
            var icon = new FontIcon { Glyph = PlayGlyph };
            button.Icon = icon;
            return icon;
        }).ToList();

        void Show(VisualState? state)
        {
            var glyph = state?.Name == "PauseState" ? PauseGlyph : PlayGlyph;
            foreach (var icon in icons)
            {
                icon.Glyph = glyph;
            }
        }

        Show(group.CurrentState);
        group.CurrentStateChanged += (_, e) => Show(e.NewState);
        return true;
    }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element)
            {
                yield return element;
            }

            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static bool HasFluentIcons()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
            if (key?.GetValueNames().Any(n => n.StartsWith("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }
        }

        return false;
    }
}
