using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Core = MarkdownHelper.Core;

namespace MarkdownHelper.UI
{
    /// <summary>
    /// Turns an edited WPF document back into markdown. The rich text editor rearranges
    /// inline elements freely, so formatting is read from each run's resolved properties
    /// rather than from the element tree that FlowDocumentBuilder produced.
    /// </summary>
    public static class FlowMarkdownReader
    {

        private sealed class Segment
        {
            public string Text = "";
            public bool IsBreak;
            public bool Bold;
            public bool Italic;
            public bool Strike;
            public bool Code;
            public string Url;
            public bool IsImage;
            public string Title = "";
        }

        public static string Read(FlowDocument document)
        {
            var result = new Core.MarkdownDocument();
            result.Blocks.AddRange(ReadBlocks(document.Blocks));
            return Core.MarkdownWriter.Write(result);
        }

        private static List<Core.Block> ReadBlocks(IEnumerable<Block> blocks)
        {
            var result = new List<Core.Block>();
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case Paragraph paragraph:
                        var read = ReadParagraph(paragraph);
                        if (read == null) break;

                        // Pressing Enter inside a code block splits it in two; rejoin the halves.
                        if (read is Core.CodeBlock code && result.LastOrDefault() is Core.CodeBlock previous)
                        {
                            previous.Code += "\n" + code.Code;
                            break;
                        }
                        result.Add(read);
                        break;

                    case Section section:
                        if (IsQuote(section))
                        {
                            var inner = ReadBlocks(section.Blocks);
                            if (inner.Count > 0) result.Add(new Core.Quote { Blocks = inner });
                        }
                        else
                        {
                            result.AddRange(ReadBlocks(section.Blocks));
                        }
                        break;

                    case List list:
                        var readList = ReadList(list);
                        if (readList.Items.Count > 0) result.Add(readList);
                        break;

                    case Table table:
                        var readTable = ReadTable(table);
                        if (readTable != null) result.Add(readTable);
                        break;
                }
            }
            return result;
        }

        private static bool IsQuote(Section section) => section.BorderThickness.Left > 0;

        private static Core.Block ReadParagraph(Paragraph paragraph)
        {
            if (Equals(paragraph.Tag, FlowDocumentBuilder.RuleTag)) return new Core.ThematicBreak();

            if (IsCodeFont(paragraph.FontFamily))
            {
                return new Core.CodeBlock
                {
                    Language = paragraph.Tag as string ?? "",
                    Code = CodeText(paragraph)
                };
            }

            int level = HeadingLevel(paragraph.FontSize);
            var segments = ReadSegments(paragraph.Inlines, level > 0);
            var inlines = ToInlines(segments);
            if (Core.InlineParser.PlainText(inlines).Trim().Length == 0 && !inlines.Any(i => i is Core.Image)) return null;

            if (level > 0) return new Core.Heading { Level = level, Inlines = inlines };
            return new Core.Paragraph { Inlines = inlines };
        }

        private static string CodeText(Paragraph paragraph)
        {
            var lines = new List<string>();
            string current = "";
            foreach (var segment in ReadSegments(paragraph.Inlines, baseBold: false))
            {
                if (segment.IsBreak) { lines.Add(current); current = ""; }
                else current += segment.Text;
            }
            lines.Add(current);
            return string.Join("\n", lines);
        }

        private static int HeadingLevel(double fontSize)
        {
            var sizes = FlowDocumentBuilder.HeadingSizes;
            for (int i = 0; i < sizes.Length; i++)
            {
                if (Math.Abs(sizes[i] - fontSize) < 0.01) return i + 1;
            }
            return 0;
        }

        private static bool IsCodeFont(FontFamily family)
        {
            return family != null && family.Source == FlowDocumentBuilder.CodeFont.Source;
        }

        private static Core.ListBlock ReadList(List list)
        {
            bool ordered = list.MarkerStyle == TextMarkerStyle.Decimal;
            var result = new Core.ListBlock { Ordered = ordered, Start = ordered ? list.StartIndex : 1, Tight = true };

            foreach (var item in list.ListItems)
            {
                var blocks = ReadBlocks(item.Blocks);
                if (blocks.Count == 0) continue;

                var read = new Core.ListItem { Blocks = blocks };
                if (blocks[0] is Core.Paragraph first) read.Checked = TakeCheckbox(first);
                result.Items.Add(read);
            }
            return result;
        }

        private static bool? TakeCheckbox(Core.Paragraph paragraph)
        {
            if (!(paragraph.Inlines.FirstOrDefault() is Core.Text text)) return null;

            bool? state = null;
            string prefix = null;
            if (text.Value.StartsWith(FlowDocumentBuilder.CheckedBox.TrimEnd(), StringComparison.Ordinal))
            {
                state = true;
                prefix = FlowDocumentBuilder.CheckedBox.TrimEnd();
            }
            else if (text.Value.StartsWith(FlowDocumentBuilder.UncheckedBox.TrimEnd(), StringComparison.Ordinal))
            {
                state = false;
                prefix = FlowDocumentBuilder.UncheckedBox.TrimEnd();
            }
            if (state == null) return null;

            text.Value = text.Value.Substring(prefix.Length).TrimStart(' ');
            if (text.Value.Length == 0) paragraph.Inlines.RemoveAt(0);
            return state;
        }

        private static Core.Table ReadTable(Table table)
        {
            var rows = table.RowGroups.SelectMany(g => g.Rows).ToList();
            if (rows.Count == 0 || rows[0].Cells.Count == 0) return null;

            int columns = rows[0].Cells.Count;
            var result = new Core.Table();
            for (int c = 0; c < columns; c++)
            {
                result.Header.Add(ReadCell(rows[0].Cells[c], baseBold: true));
                result.Aligns.Add(ReadAlign(rows[0].Cells[c]));
            }

            foreach (var row in rows.Skip(1))
            {
                var cells = new List<List<Core.Inline>>();
                for (int c = 0; c < columns; c++)
                {
                    cells.Add(c < row.Cells.Count ? ReadCell(row.Cells[c], baseBold: false) : new List<Core.Inline>());
                }
                result.Rows.Add(cells);
            }
            return result;
        }

        private static Core.ColumnAlign ReadAlign(TableCell cell)
        {
            if (!(cell.Blocks.FirstBlock is Paragraph paragraph)) return Core.ColumnAlign.None;
            switch (paragraph.TextAlignment)
            {
                case TextAlignment.Center: return Core.ColumnAlign.Center;
                case TextAlignment.Right: return Core.ColumnAlign.Right;
                default: return Core.ColumnAlign.None;
            }
        }

        private static List<Core.Inline> ReadCell(TableCell cell, bool baseBold)
        {
            var segments = new List<Segment>();
            foreach (var paragraph in cell.Blocks.OfType<Paragraph>())
            {
                if (segments.Count > 0) segments.Add(new Segment { Text = " " });
                segments.AddRange(ReadSegments(paragraph.Inlines, baseBold));
            }
            segments.RemoveAll(s => s.IsBreak);
            return ToInlines(segments);
        }

        private static List<Segment> ReadSegments(InlineCollection inlines, bool baseBold)
        {
            var segments = new List<Segment>();
            foreach (var inline in inlines) Collect(inline, null, false, "", baseBold, segments);
            return segments;
        }

        private static void Collect(Inline inline, string url, bool isImage, string title, bool baseBold, List<Segment> segments)
        {
            switch (inline)
            {
                case Run run:
                    if (run.Text.Length == 0) return;
                    bool code = IsCodeFont(run.FontFamily);
                    segments.Add(new Segment
                    {
                        Text = run.Text,
                        Bold = !baseBold && !code && run.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight(),
                        Italic = !code && run.FontStyle == FontStyles.Italic,
                        Strike = !code && HasStrike(run),
                        Code = code,
                        Url = url,
                        IsImage = isImage,
                        Title = title
                    });
                    break;

                case LineBreak _:
                    segments.Add(new Segment { IsBreak = true, Url = url });
                    break;

                case Hyperlink hyperlink:
                    string target = hyperlink.NavigateUri?.OriginalString;
                    bool image = Equals(hyperlink.Tag, FlowDocumentBuilder.ImageTag);
                    string tip = hyperlink.ToolTip as string ?? "";
                    string linkTitle = tip == target ? "" : tip;
                    foreach (var child in hyperlink.Inlines) Collect(child, target, image, linkTitle, baseBold, segments);
                    break;

                case Span span:
                    foreach (var child in span.Inlines) Collect(child, url, isImage, title, baseBold, segments);
                    break;
            }
        }

        private static bool HasStrike(Run run)
        {
            for (DependencyObject current = run; current is TextElement element; current = element.Parent)
            {
                if (element is Inline inline && inline.TextDecorations != null
                    && inline.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Rebuilds nesting from flat styled runs, outermost style first, so that a bold
        /// phrase with an italic word in it stays one bold span.
        /// </summary>
        private static List<Core.Inline> ToInlines(List<Segment> segments)
        {

            return GroupByLink(segments);
        }

        private static List<Core.Inline> GroupByLink(List<Segment> segments)
        {
            var result = new List<Core.Inline>();
            foreach (var group in Runs(segments, s => (s.Url ?? "") + "\0" + s.IsImage))
            {
                var first = group[0];
                if (first.Url == null)
                {
                    result.AddRange(GroupByBold(group));
                }
                else if (first.IsImage)
                {
                    result.Add(new Core.Image { Url = first.Url, Alt = string.Concat(group.Select(s => s.Text)), Title = first.Title });
                }
                else
                {
                    result.Add(new Core.Link { Url = first.Url, Title = first.Title, Children = GroupByBold(group) });
                }
            }
            return MergeText(result);
        }

        private static List<Core.Inline> GroupByBold(List<Segment> segments)
        {
            var result = new List<Core.Inline>();
            foreach (var group in Runs(segments, s => s.Bold))
            {
                if (group[0].Bold) result.Add(new Core.Strong { Children = GroupByItalic(group) });
                else result.AddRange(GroupByItalic(group));
            }
            return result;
        }

        private static List<Core.Inline> GroupByItalic(List<Segment> segments)
        {
            var result = new List<Core.Inline>();
            foreach (var group in Runs(segments, s => s.Italic))
            {
                if (group[0].Italic) result.Add(new Core.Emphasis { Children = GroupByStrike(group) });
                else result.AddRange(GroupByStrike(group));
            }
            return result;
        }

        private static List<Core.Inline> GroupByStrike(List<Segment> segments)
        {
            var result = new List<Core.Inline>();
            foreach (var group in Runs(segments, s => s.Strike))
            {
                if (group[0].Strike) result.Add(new Core.Strike { Children = Leaves(group) });
                else result.AddRange(Leaves(group));
            }
            return result;
        }

        private static List<Core.Inline> Leaves(List<Segment> segments)
        {
            var result = new List<Core.Inline>();
            foreach (var segment in segments)
            {
                if (segment.IsBreak) result.Add(new Core.HardBreak());
                else if (segment.Code) result.Add(new Core.CodeSpan(segment.Text));
                else result.Add(new Core.Text(segment.Text));
            }
            return MergeText(result);
        }

        private static List<Core.Inline> MergeText(List<Core.Inline> inlines)
        {
            var result = new List<Core.Inline>();
            foreach (var inline in inlines)
            {
                if (inline is Core.Text text && result.LastOrDefault() is Core.Text previous)
                {
                    previous.Value += text.Value;
                    continue;
                }
                result.Add(inline);
            }
            return result;
        }

        private static IEnumerable<List<Segment>> Runs<T>(List<Segment> segments, Func<Segment, T> key)
        {
            var current = new List<Segment>();
            foreach (var segment in segments)
            {
                // A line break carries no style of its own, so it never splits a styled span.
                if (segment.IsBreak && current.Count > 0) { current.Add(segment); continue; }
                if (current.Count > 0 && !Equals(key(current[0]), key(segment)))
                {
                    yield return current;
                    current = new List<Segment>();
                }
                current.Add(segment);
            }
            if (current.Count > 0) yield return current;
        }
    }
}
