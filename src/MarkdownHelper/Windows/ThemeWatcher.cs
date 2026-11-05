using System;
using System.Windows;
using MarkdownHelper.UI;
using Microsoft.Win32;

namespace MarkdownHelper.Windows
{
    /// <summary>Reads the Windows theme and reports when it changes.</summary>
    internal sealed class ThemeWatcher : IDisposable
    {
        /// <summary>Forces a theme, for checking one that Windows is not currently using.</summary>
        private const string ForceThemeVariable = "MDH_THEME";

        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public ThemeWatcher()
        {
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        }

        public event EventHandler Changed;

        public static ThemePalette Current()
        {
            string forced = Environment.GetEnvironmentVariable(ForceThemeVariable);
            if (forced == "light") return ThemePalette.Light;
            if (forced == "dark") return ThemePalette.Dark;
            if (SystemParameters.HighContrast) return ThemePalette.HighContrast();
            return AppsUseLightTheme() ? ThemePalette.Light : ThemePalette.Dark;
        }

        private static bool AppsUseLightTheme()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
            {
                // Windows versions without the setting are light.
                return !(key?.GetValue("AppsUseLightTheme") is int value) || value != 0;
            }
        }

        private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.Accessibility)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        }
    }
}
