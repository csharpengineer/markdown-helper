using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Matches the app that made a copy against the ignore list. An entry is one of:
    /// a group such as "Terminal", which covers several process names; "Label|process",
    /// written when an app is picked from the list; or a bare process name.
    /// </summary>
    public static class IgnoreList
    {
        public const string VsCode = "VS Code";
        public const string Terminal = "Terminal";

        private const char Separator = '|';

        private static readonly Dictionary<string, string[]> Groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { VsCode, new[] { "code", "code - insiders", "vscodium" } },
            { Terminal, new[] { "windowsterminal", "conhost", "openconsole", "cmd", "powershell", "pwsh", "wezterm-gui", "alacritty" } }
        };

        public static string Entry(string label, string processName) => label + Separator + processName;

        /// <summary>The name to show for an entry.</summary>
        public static string Label(string entry)
        {
            int separator = entry.LastIndexOf(Separator);
            return separator < 0 ? entry : entry.Substring(0, separator);
        }

        private static string ProcessOf(string entry)
        {
            int separator = entry.LastIndexOf(Separator);
            return separator < 0 ? entry : entry.Substring(separator + 1);
        }

        public static bool Matches(IEnumerable<string> entries, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName) || entries == null) return false;
            string name = Clean(processName);

            foreach (var entry in entries)
            {
                if (Groups.TryGetValue(entry, out var names) && names.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
                if (string.Equals(Clean(ProcessOf(entry)), name, StringComparison.OrdinalIgnoreCase)) return true;
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
