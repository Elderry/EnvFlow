using System;
using System.Diagnostics;
using System.Security.Principal;

namespace EnvFlow.Helpers;

public static class AdminHelper
{
    public static bool IsRunningAsAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin()
    {
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "",
            UseShellExecute = true,
            Verb = "runas" // Run as administrator
        };

        Process.Start(processInfo);
        Environment.Exit(0);
    }
}
