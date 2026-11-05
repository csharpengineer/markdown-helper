using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Plain text for Discord, which renders most markdown itself. Only what it cannot
    /// show is changed: tables, deep headings, rules, task lists, images and nested quotes.
    /// </summary>
    public sealed class DiscordRenderer : MarkdownWriter
    {
        public const int MessageLimit = 2000;

        private const string Rule = "────────────";

        public static string Format(MarkdownDocument document)
        {
            return new DiscordRenderer().Render(document);
        }

        protected override string WriteHeading(Heading heading)
        {
            if (heading.Level <= 3) return base.WriteHeading(heading);
            return "**" + WriteInlines(heading.Inlines).Replace("\n", " ") + "**";
        }

        protected override string WriteRule() => Rule;

        protected override string WriteHardBreak() => "\n";

        protected override string TaskPrefix(ListItem item)
        {
            if (item.Checked == null) return "";
            return item.Checked.Value ? "☑ " : "☐ ";
        }

        protected override string WriteImage(Image image)
        {
            string label = image.Alt.Length > 0 ? image.Alt : image.Url;
            return "[" + EscapeText(label, '\0', '\0') + "](" + image.Url + ")";
        }

        protected override string WriteQuote(Quote quote)
        {
            // Discord has no nested quotes, so inner quote markers would show as literal text.
            var flat = Flatten(quote.Blocks);
            string body = string.Join("\n\n", flat.Select(WriteBlock));
            return string.Join("\n", body.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l));
        }

        private static IEnumerable<Block> Flatten(IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (block is Quote inner)
                {
                    foreach (var child in Flatten(inner.Blocks)) yield return child;
                }
                else
                {
                    yield return block;
                }
            }
        }

        protected override string WriteTable(Table table)
        {
            var rows = new List<List<string>> { table.Header.Select(InlineParser.PlainText).ToList() };
            rows.AddRange(table.Rows.Select(r => r.Select(InlineParser.PlainText).ToList()));

            int columns = table.Aligns.Count;
            var widths = new int[columns];
            foreach (var row in rows)
            {
                for (int c = 0; c < columns; c++) widths[c] = Math.Max(widths[c], Math.Max(row[c].Length, 3));
            }

            var lines = new List<string>();
            for (int r = 0; r < rows.Count; r++)
            {
                lines.Add(string.Join("  ", rows[r].Select((cell, c) => Pad(cell, widths[c], table.Aligns[c]))).TrimEnd());
                if (r == 0) lines.Add(string.Join("  ", widths.Select(w => new string('-', w))));
            }
            return "```\n" + string.Join("\n", lines) + "\n```";
        }

        private static string Pad(string text, int width, ColumnAlign align)
        {
            int space = width - text.Length;
            switch (align)
            {
                case ColumnAlign.Right: return new string(' ', space) + text;
                case ColumnAlign.Center: return new string(' ', space / 2) + text + new string(' ', space - space / 2);
                default: return text + new string(' ', space);
            }
        }
    }
}
