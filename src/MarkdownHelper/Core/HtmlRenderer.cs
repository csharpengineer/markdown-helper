using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace MarkdownHelper.Core
{
    /// <summary>Renders semantic HTML for the clipboard. Raw HTML in the source stays literal text.</summary>
    public static class HtmlRenderer
    {
        private const string TableStyle = "border-collapse:collapse";
        private const string CellStyle = "border:1px solid #bbbbbb;padding:4px 8px";

        public static string Render(MarkdownDocument document)
        {
            var builder = new StringBuilder();
            foreach (var block in document.Blocks) WriteBlock(builder, block, tight: false);
            return builder.ToString();
        }

        private static void WriteBlock(StringBuilder html, Block block, bool tight)
        {
            switch (block)
            {
                case Heading heading:
                    html.Append("<h").Append(heading.Level).Append('>');
                    WriteInlines(html, heading.Inlines);
                    html.Append("</h").Append(heading.Level).Append(">\n");
                    break;

                case Paragraph paragraph:
                    if (!tight) html.Append("<p>");
                    WriteInlines(html, paragraph.Inlines);
                    html.Append(tight ? "\n" : "</p>\n");
                    break;

                case ThematicBreak _:
                    html.Append("<hr>\n");
                    break;

                case CodeBlock code:
                    html.Append("<pre><code>").Append(Escape(code.Code)).Append("</code></pre>\n");
                    break;

                case Quote quote:
                    html.Append("<blockquote>\n");
                    foreach (var inner in quote.Blocks) WriteBlock(html, inner, tight: false);
                    html.Append("</blockquote>\n");
                    break;

                case ListBlock list:
                    WriteList(html, list);
                    break;

                case Table table:
                    WriteTable(html, table);
                    break;
            }
        }

        private static void WriteList(StringBuilder html, ListBlock list)
        {
            string tag = list.Ordered ? "ol" : "ul";
            html.Append('<').Append(tag);
            if (list.Ordered && list.Start != 1) html.Append(" start=\"").Append(list.Start).Append('"');
            html.Append(">\n");

            foreach (var item in list.Items)
            {
                html.Append("<li>");
                if (item.Checked != null) html.Append(item.Checked.Value ? "☑ " : "☐ ");
                foreach (var block in item.Blocks) WriteBlock(html, block, list.Tight);
                html.Append("</li>\n");
            }

            html.Append("</").Append(tag).Append(">\n");
        }

        private static void WriteTable(StringBuilder html, Table table)
        {
            html.Append("<table style=\"").Append(TableStyle).Append("\">\n<thead>\n<tr>");
            for (int c = 0; c < table.Header.Count; c++) WriteCell(html, "th", table.Header[c], table.Aligns[c]);
            html.Append("</tr>\n</thead>\n<tbody>\n");

            foreach (var row in table.Rows)
            {
                html.Append("<tr>");
                for (int c = 0; c < row.Count; c++) WriteCell(html, "td", row[c], table.Aligns[c]);
                html.Append("</tr>\n");
            }

            html.Append("</tbody>\n</table>\n");
        }

        private static void WriteCell(StringBuilder html, string tag, List<Inline> inlines, ColumnAlign align)
        {
            string style = CellStyle;
            if (align != ColumnAlign.None) style += ";text-align:" + align.ToString().ToLowerInvariant();
            html.Append('<').Append(tag).Append(" style=\"").Append(style).Append("\">");
            WriteInlines(html, inlines);
            html.Append("</").Append(tag).Append('>');
        }

        private static void WriteInlines(StringBuilder html, IEnumerable<Inline> inlines)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Text text: html.Append(Escape(text.Value)); break;
                    case SoftBreak _: html.Append('\n'); break;
                    case HardBreak _: html.Append("<br>\n"); break;
                    case CodeSpan code: html.Append("<code>").Append(Escape(code.Code)).Append("</code>"); break;
                    case Strong strong: Wrap(html, "strong", strong.Children); break;
                    case Emphasis emphasis: Wrap(html, "em", emphasis.Children); break;
                    case Strike strike: Wrap(html, "del", strike.Children); break;
                    case Link link: WriteLink(html, link.Url, link.Children); break;
                    case Image image:
                        string label = image.Alt.Length > 0 ? image.Alt : image.Url;
                        WriteLink(html, image.Url, new List<Inline> { new Text(label) });
                        break;
                }
            }
        }

        private static void Wrap(StringBuilder html, string tag, IEnumerable<Inline> children)
        {
            html.Append('<').Append(tag).Append('>');
            WriteInlines(html, children);
            html.Append("</").Append(tag).Append('>');
        }

        private static void WriteLink(StringBuilder html, string url, IEnumerable<Inline> children)
        {
            if (!IsSafeUrl(url))
            {
                WriteInlines(html, children);
                return;
            }
            html.Append("<a href=\"").Append(Escape(url)).Append("\">");
            WriteInlines(html, children);
            html.Append("</a>");
        }

        /// <summary>Only web and mail links are kept, so a javascript: link cannot reach the paste target.</summary>
        public static bool IsSafeUrl(string url)
        {
            string[] allowed = { "http://", "https://", "mailto:" };
            return allowed.Any(p => url.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private static string Escape(string text) => WebUtility.HtmlEncode(text).Replace("&#39;", "'").Replace("&#163;", "£");
    }
}
