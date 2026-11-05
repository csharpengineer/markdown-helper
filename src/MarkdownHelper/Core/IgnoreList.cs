using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MarkdownHelper.Core
{
    /// <summary>An app picked for the ignore list.</summary>
    [DataContract]
    public sealed class IgnoredApp
    {
        public IgnoredApp(string label, string processName, string iconPng = null)
        {
            Label = label;
            ProcessName = processName;
            IconPng = iconPng;
        }

        [DataMember] public string Label { get; set; }
        [DataMember] public string ProcessName { get; set; }

        /// <summary>
        /// The app's icon as base64 PNG. The image itself is kept rather than a path, because
        /// many apps move folders when they update, and it should still show when they're closed.
        /// </summary>
        [DataMember] public string IconPng { get; set; }
    }

    /// <summary>Matches the app that made a copy against the ignore list.</summary>
    public static class IgnoreList
    {
        public static bool Matches(IEnumerable<IgnoredApp> apps, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName) || apps == null) return false;
            string name = Clean(processName);
            return apps.Any(a => string.Equals(Clean(a.ProcessName), name, StringComparison.OrdinalIgnoreCase));
        }

        private static string Clean(string name)
        {
            string trimmed = (name ?? "").Trim();
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed.Substring(0, trimmed.Length - 4) : trimmed;
        }
    }
}
