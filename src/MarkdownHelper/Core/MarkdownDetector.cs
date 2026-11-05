using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Decides whether copied text is worth offering to format. It is picky on purpose:
    /// a false alarm interrupts someone, while a miss is covered by the shortcut.
    /// </summary>
    public static class MarkdownDetector
    {
        private const int MinimumLength = 15;
        private const int MaximumLength = 200000;
        private const int SignsNeeded = 2;
        private const double CodeLineLimit = 0.3;
        private static readonly TimeSpan MatchLimit = TimeSpan.FromMilliseconds(250);

        private static readonly Regex Fence = new Regex(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex HeadingLine = new Regex(@"^ {0,3}#{1,6} +\S", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex Bold = new Regex(@"(?<![\w*])\*\*(?=\S)[^\n]*?(?<=\S)\*\*|(?<!\w)__(?=\S)[^\n]*?(?<=\S)__(?!\w)", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex Bullet = new Regex(@"^ *[-*+] +\S", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex Numbered = new Regex(@"^ *\d{1,3}[.)] +\S", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex QuoteLine = new Regex(@"^ {0,3}> *\S", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex LinkInline = new Regex(@"\[[^\]\n]+\]\((?:https?://|mailto:|/|\.|#)[^)\s]*\)", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex InlineCode = new Regex(@"(?<!`)`[^`\n]+`(?!`)", RegexOptions.Compiled, MatchLimit);
        private static readonly Regex TableDelimiter = new Regex(@"^ *\|? *:?-+:? *(\| *:?-+:? *)+\|? *$|^ *\| *:?-+:? *\| *$", RegexOptions.Compiled, MatchLimit);

        private static readonly Regex CodeLine = new Regex(
            @"[;{}]\s*$|^\s*(def|class|import|from|function|func|public|private|protected|internal|static|var|let|const|return|using|namespace|if|else|elif|for|foreach|while|try|catch)\b.*[:({=]|^\s*</?\w[^>]*>\s*$|^\s*[\w.-]+:(\s+\S+)?\s*$|^\s*-\s+[\w.-]+:(\s+\S+)?\s*$",
            RegexOptions.Compiled, MatchLimit);

        public static bool IsMarkdown(string text)
        {
            try
            {
                return Inspect(text);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private static bool Inspect(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < MinimumLength || text.Length > MaximumLength) return false;

            var lines = MarkdownParser.SplitLines(text);
            var prose = new System.Collections.Generic.List<string>();
            bool inFence = false, sawFence = false;
            foreach (var line in lines)
            {
                if (Fence.IsMatch(line))
                {
                    inFence = !inFence;
                    sawFence = true;
                    continue;
                }
                if (!inFence) prose.Add(line);
            }

            if (LooksLikeSourceCode(prose)) return false;

            int signs = sawFence ? SignsNeeded : 0;
            if (HasTable(prose)) signs += SignsNeeded;
            if (prose.Any(l => HeadingLine.IsMatch(l))) signs++;
            if (prose.Any(l => Bold.IsMatch(l))) signs++;
            if (HasRun(prose, Bullet)) signs++;
            if (HasRun(prose, Numbered)) signs++;
            if (prose.Any(l => QuoteLine.IsMatch(l))) signs++;
            if (prose.Any(l => LinkInline.IsMatch(l))) signs++;
            if (prose.Any(l => InlineCode.IsMatch(l))) signs++;

            return signs >= SignsNeeded;
        }

        private static bool HasTable(System.Collections.Generic.List<string> lines)
        {
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i - 1].Contains("|") && TableDelimiter.IsMatch(lines[i])) return true;
            }
            return false;
        }

        /// <summary>A list needs two items in a row, so one line starting with a dash does not count.</summary>
        private static bool HasRun(System.Collections.Generic.List<string> lines, Regex pattern)
        {
            for (int i = 1; i < lines.Count; i++)
            {
                if (pattern.IsMatch(lines[i - 1]) && pattern.IsMatch(lines[i])) return true;
            }
            return false;
        }

        private static bool LooksLikeSourceCode(System.Collections.Generic.List<string> lines)
        {
            var content = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (content.Count == 0) return false;
            int code = content.Count(l => CodeLine.IsMatch(l));
            return (double)code / content.Count > CodeLineLimit;
        }
    }
}
