using System.Diagnostics;

namespace Luma.App.Services;

public static class AutomationTaskService
{
    private const string TaskName = "Luma.StartAtLogon";

    public static void Apply(bool startAtLogon, string executablePath)
    {
        try
        {
            if (startAtLogon && File.Exists(executablePath))
            {
                Run("schtasks", $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /RL LIMITED /TR \"\\\"{executablePath}\\\" --logon\"");
            }
            else
            {
                Run("schtasks", $"/Delete /F /TN \"{TaskName}\"");
            }
        }
        catch
        {
            // scheduled-task registration is best effort
        }
    }

    private static void Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });
        process?.WaitForExit(5000);
    }
}
