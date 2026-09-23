using System.Text.Json;
using Luma.Core.Lan;
using Luma.Core.Settings;

namespace Luma.Core.Tests;

public sealed class LanSettingsPatchTests
{
    [Fact]
    public void Segment_and_tray_apply_while_logon_and_schedules_stay_off()
    {
        var source = new AppSettings
        {
            SaveFolder = Path.GetTempPath(),
            LaunchToTray = false,
            Automation = new AutomationSettings
            {
                StartAtLogon = true,
                SegmentEnabled = false,
                Schedules = [new ScheduleRule { Enabled = true, Start = new TimeOnly(9, 0) }]
            }
        };
        using var patch = JsonDocument.Parse("""
            {
              "launchToTray": true,
              "closeToTray": true,
              "automation": {
                "startAtLogon": true,
                "segmentEnabled": true,
                "segmentMinutes": 15,
                "schedules": [{ "enabled": true, "start": "08:00:00" }]
              },
              "hotkeys": {
                "enabled": true,
                "start": "Ctrl+Alt+R",
                "pause": "Ctrl+Alt+Shift+P",
                "stop": "Ctrl+Alt+S",
                "screenshot": "Ctrl+Shift+X"
              }
            }
            """);

        var updated = LanControlApi.ApplySettings(source, patch.RootElement);

        Assert.True(updated.LaunchToTray);
        Assert.True(updated.CloseToTray);
        Assert.True(updated.Automation.SegmentEnabled);
        Assert.Equal(15, updated.Automation.SegmentMinutes);
        Assert.False(updated.Automation.StartAtLogon);
        Assert.Empty(updated.Automation.Schedules);
        Assert.Equal("Ctrl+Shift+X", updated.Hotkeys.Screenshot);
        Assert.Equal(source.Lan.Port, updated.Lan.Port);
    }

    [Fact]
    public void Lan_port_and_unknown_fields_are_rejected()
    {
        var source = new AppSettings { SaveFolder = Path.GetTempPath() };
        using var unknown = JsonDocument.Parse("""{"nope":1}""");
        using var port = JsonDocument.Parse("""{"lanPort":9}""");

        Assert.Throws<InvalidOperationException>(() => LanControlApi.ApplySettings(source, unknown.RootElement));
        Assert.Throws<InvalidOperationException>(() => LanControlApi.ApplySettings(source, port.RootElement));
    }
}
