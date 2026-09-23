using System.Threading;
using Luma.Core.Localization;
using Luma.Core.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Globalization;

namespace Luma.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            ApplyStartupLanguage();
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    private static void ApplyStartupLanguage()
    {
        try
        {
            var settings = new SettingsStore().Load();
            var resolved = UiLanguages.ResolveEffective(settings.UiLanguage);
            ApplicationLanguages.PrimaryLanguageOverride = UiLanguages.FrameworkTag(resolved);
        }
        catch
        {
            // Control names still come from the in-app language tables.
        }
    }
}
