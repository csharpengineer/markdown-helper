using System.Collections.Generic;

namespace MarkdownHelper.Core
{
    /// <summary>What the clipboard watcher learned about a single copy.</summary>
    public sealed class CopyInfo
    {
        public bool IsOwnWrite { get; set; }
        public bool IsPrivate { get; set; }
        public string ProcessName { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// Decides whether a copy should reach the person. Only a hash of the last text is
    /// kept, so clipboard content stays out of memory once it has been judged.
    /// </summary>
    public sealed class CopyFilter
    {
        private int? lastHash;

        public bool ShouldOffer(CopyInfo copy, IEnumerable<IgnoredApp> ignoredApps)
        {
            if (copy.IsOwnWrite || copy.IsPrivate) return false;
            if (string.IsNullOrEmpty(copy.Text)) return false;
            if (IgnoreList.Matches(ignoredApps, copy.ProcessName)) return false;
            if (!MarkdownDetector.IsMarkdown(copy.Text)) return false;

            int hash = StableHash(copy.Text);
            if (hash == lastHash) return false;
            lastHash = hash;
            return true;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return hash;
            }
        }
    }
}
