using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace AuTaskBar.Services
{
    public class StartupService : IStartupService
    {
        private const string StartupRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupAppName = "AuTaskBarFocusBar";

        public void SetRunAtStartup(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRunKey, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(StartupRunKey);
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        key.SetValue(StartupAppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(StartupAppName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // Keep app non-blocking if registry access fails.
            }
        }
    }
}
