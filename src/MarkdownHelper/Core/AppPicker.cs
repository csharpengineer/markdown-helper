using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>An app with a window open.</summary>
    public sealed class RunningApp
    {
        public RunningApp(string processName, string description, string path)
        {
            ProcessName = processName;
            Label = string.IsNullOrWhiteSpace(description) ? processName : description.Trim();
            Path = path;
        }

        public string ProcessName { get; }
        public string Label { get; }

        /// <summary>The app's file, used for its icon. Null when Windows will not reveal it.</summary>
        public string Path { get; }
    }

    /// <summary>Decides which running apps to offer when adding to the ignore list.</summary>
    public static class AppPicker
    {
        // Windows helpers that own windows but are not apps anyone would recognise.
        private static readonly string[] Hidden = { "ApplicationFrameHost", "TextInputHost", "SystemSettings", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost" };

        public static List<RunningApp> Choices(IEnumerable<RunningApp> running, IEnumerable<IgnoredApp> ignored, string ownProcessName)
        {
            var entries = ignored.ToList();
            return running
                .Where(a => !a.ProcessName.Equals(ownProcessName, StringComparison.OrdinalIgnoreCase))
                .Where(a => !Hidden.Contains(a.ProcessName, StringComparer.OrdinalIgnoreCase))
                .Where(a => !IgnoreList.Matches(entries, a.ProcessName))
                .GroupBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
