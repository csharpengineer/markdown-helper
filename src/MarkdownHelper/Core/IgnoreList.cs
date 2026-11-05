using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Matches the app that made a copy against the ignore list. Friendly entries such as
    /// "Terminal" cover several process names; anything else is compared as a process name.
    /// </summary>
    public static class IgnoreList
    {
        public const string VsCode = "VS Code";
        public const string Terminal = "Terminal";

        private static readonly Dictionary<string, string[]> Groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { VsCode, new[] { "code", "code - insiders", "vscodium" } },
            { Terminal, new[] { "windowsterminal", "conhost", "openconsole", "cmd", "powershell", "pwsh", "wezterm-gui", "alacritty" } }
        };

        public static bool Matches(IEnumerable<string> entries, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName) || entries == null) return false;
            string name = Clean(processName);

            foreach (var entry in entries)
            {
                if (Groups.TryGetValue(entry, out var names) && names.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
                if (string.Equals(Clean(entry), name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Clean(string name)
        {
            string trimmed = name.Trim();
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed.Substring(0, trimmed.Length - 4) : trimmed;
        }
    }
}
