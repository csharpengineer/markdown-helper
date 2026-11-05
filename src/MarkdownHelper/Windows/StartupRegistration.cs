using System;
using System.Reflection;
using Microsoft.Win32;

namespace MarkdownHelper.Windows
{
    /// <summary>The per-user "start with Windows" entry. Needs no administrator rights.</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MarkdownHelper";
        public const string BackgroundArgument = "--background";

        public static void Apply(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        // Written every launch, so moving the exe does not leave a dead entry.
                        key.SetValue(ValueName, "\"" + Assembly.GetEntryAssembly().Location + "\" " + BackgroundArgument);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is System.Security.SecurityException)
            {
                // Locked-down machines can refuse this. The tool still works when started by hand.
            }
        }
    }
}
