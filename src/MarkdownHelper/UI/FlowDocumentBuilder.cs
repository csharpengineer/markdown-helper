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
    /// Turns parsed markdown into a WPF document. FlowMarkdownReader recovers the structure
    /// from visible properties (font size, font family) because the rich text editor keeps
    /// those through edits, while it drops things like Tag when a paragraph is split.
    /// </summary>
    public static class FlowDocumentBuilder
    {
        public const double BodySize = 14;
        public static readonly double[] HeadingSizes = { 26, 22, 18, 16, 15, 14.5 };
        public static readonly FontFamily BodyFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily CodeFont = new FontFamily("Cascadia Mono, Cascadia Code, Consolas");
        private static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");
        public const string ImageTag = "image";
        public const string RuleTag = "rule";

        public const string UncheckedBox = "☐ ";
        public const string CheckedBox = "☑ ";

        public static FlowDocument Build(Core.MarkdownDocument document, ThemePalette palette)
        {
            var flow = NewDocument(palette);
            foreach (var block in document.Blocks) flow.Blocks.Add(ToBlock(block, palette, tight: false));
            return flow;
        }

        /// <summary>Builds the small document shown for one piece in Remove pieces mode.</summary>
        public static FlowDocument BuildPiece(Core.Piece piece, ThemePalette palette)
        {
            var flow = NewDocument(palette);
            if (piece.OwnerList != null)
            {
                var single = new Core.ListBlock
                {
                    Ordered = piece.OwnerList.Ordered,
                    Start = piece.ItemNumber,
                    Tight = true,
                    Items = new List<Core.ListItem> { (Core.ListItem)piece.Block }
                };
                flow.Blocks.Add(ToBlock(single, palette, tight: true));
            }
            else
            {
                flow.Blocks.Add(ToBlock(piece.Block, palette, tight: false));
            }

            foreach (var block in flow.Blocks) block.Margin = new Thickness(0);
            return flow;
        }

        private static FlowDocument NewDocument(ThemePalette palette)
        {
            return new FlowDocument
            {
                FontFamily = BodyFont,
                FontSize = BodySize,
                Foreground = new SolidColorBrush(palette.Text),
                Background = Brushes.Transparent,
                PagePadding = new Thickness(0),
                LineHeight = double.NaN,
                TextAlignment = TextAlignment.Left
            };
        }

        private static System.Windows.Documents.Block ToBlock(Core.Block block, ThemePalette palette, bool tight)
        {
            switch (block)
            {
                case Core.Heading heading: return ToHeading(heading, palette);
                case Core.Paragraph paragraph: return ToParagraph(paragraph, palette, tight);
                case Core.ThematicBreak _: return ToRule(palette);
                case Core.CodeBlock code: return ToCode(code, palette);
                case Core.Quote quote: return ToQuote(quote, palette);
                case Core.ListBlock list: return ToList(list, palette);
                case Core.Table table: return ToTable(table, palette);
                default: throw new ArgumentException("Unknown block " + block.GetType().Name);
            }
        }

        private static System.Windows.Documents.Paragraph ToHeading(Core.Heading heading, ThemePalette palette)
        {
            int level = Math.Min(Math.Max(heading.Level, 1), HeadingSizes.Length);
            var paragraph = new System.Windows.Documents.Paragraph
            {
                FontSize = HeadingSizes[level - 1],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, level <= 2 ? 14 : 12, 0, 8)
            };
            AddInlines(paragraph.Inlines, heading.Inlines, palette);
            return paragraph;
        }

        private static System.Windows.Documents.Paragraph ToParagraph(Core.Paragraph source, ThemePalette palette, bool tight)
        {
            var paragraph = new System.Windows.Documents.Paragraph
            {
                Margin = new Thickness(0, 0, 0, tight ? 2 : 11),
                LineHeight = 22
            };
            AddInlines(paragraph.Inlines, source.Inlines, palette);
            return paragraph;
        }

        private static System.Windows.Documents.Paragraph ToRule(ThemePalette palette)
        {
            return new System.Windows.Documents.Paragraph
            {
                Tag = RuleTag,
                FontSize = 2,
                Margin = new Thickness(0, 8, 0, 16),
                BorderBrush = new SolidColorBrush(palette.StrokeStrong),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private static System.Windows.Documents.Paragraph ToCode(Core.CodeBlock code, ThemePalette palette)
        {
            var paragraph = new System.Windows.Documents.Paragraph
            {
                Tag = code.Language,
                FontFamily = CodeFont,
                FontSize = 12.5,
                Background = new SolidColorBrush(palette.CodeBackground),
                BorderBrush = new SolidColorBrush(palette.Stroke),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13, 10, 13, 10),
                Margin = new Thickness(0, 2, 0, 12),
                LineHeight = 20
            };

            var lines = code.Code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) paragraph.Inlines.Add(new LineBreak());
                paragraph.Inlines.Add(new Run(lines[i]));
            }
            return paragraph;
        }

        private static Section ToQuote(Core.Quote quote, ThemePalette palette)
        {
            var section = new Section
            {
                BorderBrush = new SolidColorBrush(palette.Accent),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(14, 2, 0, 2),
                Margin = new Thickness(0, 2, 0, 12),
                Foreground = new SolidColorBrush(palette.TextSecondary)
            };

            foreach (var block in quote.Blocks) section.Blocks.Add(ToBlock(block, palette, tight: false));
            var last = section.Blocks.LastBlock;
            if (last != null) last.Margin = new Thickness(last.Margin.Left, last.Margin.Top, last.Margin.Right, 0);
            return section;
        }

        private static List ToList(Core.ListBlock source, ThemePalette palette)
        {
            bool allTasks = source.Items.Count > 0 && source.Items.All(i => i.Checked != null);
            var list = new List
            {
                MarkerStyle = allTasks ? TextMarkerStyle.None : source.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                StartIndex = Math.Max(1, source.Start),
                Margin = new Thickness(0, 0, 0, 11),
                Padding = new Thickness(allTasks ? 0 : 22, 0, 0, 0)
            };

            foreach (var item in source.Items)
            {
                var listItem = new System.Windows.Documents.ListItem();
                foreach (var block in item.Blocks) listItem.Blocks.Add(ToBlock(block, palette, source.Tight));
                if (listItem.Blocks.Count == 0) listItem.Blocks.Add(new System.Windows.Documents.Paragraph());

                if (item.Checked != null && listItem.Blocks.FirstBlock is System.Windows.Documents.Paragraph first)
                {
                    var box = new Run(item.Checked.Value ? CheckedBox : UncheckedBox) { FontFamily = SymbolFont };
                    if (first.Inlines.FirstInline != null) first.Inlines.InsertBefore(first.Inlines.FirstInline, box);
                    else first.Inlines.Add(box);
                }
                foreach (var nested in listItem.Blocks.OfType<List>()) nested.Margin = new Thickness(0, 2, 0, 2);
                list.ListItems.Add(listItem);
            }
            return list;
        }

        private static System.Windows.Documents.Table ToTable(Core.Table source, ThemePalette palette)
        {
            var table = new System.Windows.Documents.Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 2, 0, 12),
                BorderBrush = new SolidColorBrush(palette.Stroke),
                BorderThickness = new Thickness(1, 1, 0, 0),
                FontSize = 13
            };
            foreach (var _ in source.Aligns) table.Columns.Add(new TableColumn());

            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            var header = new TableRow { FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(palette.CodeBackground) };
            for (int c = 0; c < source.Header.Count; c++) header.Cells.Add(ToCell(source.Header[c], source.Aligns[c], palette));
            group.Rows.Add(header);

            foreach (var row in source.Rows)
            {
                var tableRow = new TableRow();
                for (int c = 0; c < row.Count; c++) tableRow.Cells.Add(ToCell(row[c], source.Aligns[c], palette));
                group.Rows.Add(tableRow);
            }
            return table;
        }

        private static TableCell ToCell(List<Core.Inline> inlines, Core.ColumnAlign align, ThemePalette palette)
        {
            var paragraph = new System.Windows.Documents.Paragraph { Margin = new Thickness(0), TextAlignment = ToAlignment(align) };
            AddInlines(paragraph.Inlines, inlines, palette);
            return new TableCell(paragraph)
            {
                Padding = new Thickness(11, 7, 11, 7),
                BorderBrush = new SolidColorBrush(palette.Stroke),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
        }

        private static TextAlignment ToAlignment(Core.ColumnAlign align)
        {
            switch (align)
            {
                case Core.ColumnAlign.Center: return TextAlignment.Center;
                case Core.ColumnAlign.Right: return TextAlignment.Right;
                default: return TextAlignment.Left;
            }
        }

        private static void AddInlines(InlineCollection target, IEnumerable<Core.Inline> inlines, ThemePalette palette)
        {
            foreach (var inline in inlines) target.Add(ToInline(inline, palette));
        }

        private static System.Windows.Documents.Inline ToInline(Core.Inline inline, ThemePalette palette)
        {
            switch (inline)
            {
                case Core.Text text: return new Run(text.Value);
                case Core.SoftBreak _: return new Run(" ");
                case Core.HardBreak _: return new LineBreak();
                case Core.CodeSpan code:
                    return new Run(code.Code)
                    {
                        FontFamily = CodeFont,
                        FontSize = BodySize * 0.9,
                        Background = new SolidColorBrush(palette.CodeBackground)
                    };
                case Core.Strong strong:
                    return Wrap(new Span { FontWeight = FontWeights.SemiBold }, strong.Children, palette);
                case Core.Emphasis emphasis:
                    return Wrap(new Span { FontStyle = FontStyles.Italic }, emphasis.Children, palette);
                case Core.Strike strike:
                    return Wrap(new Span { TextDecorations = TextDecorations.Strikethrough }, strike.Children, palette);
                case Core.Link link:
                    return Wrap(MakeLink(link.Url, link.Title, palette), link.Children, palette);
                case Core.Image image:
                    var hyperlink = MakeLink(image.Url, image.Title, palette);
                    hyperlink.Tag = ImageTag;
                    hyperlink.Inlines.Add(new Run(image.Alt.Length > 0 ? image.Alt : image.Url));
                    return hyperlink;
                default:
                    throw new ArgumentException("Unknown inline " + inline.GetType().Name);
            }
        }

        private static Hyperlink MakeLink(string url, string title, ThemePalette palette)
        {
            var hyperlink = new Hyperlink
            {
                Foreground = new SolidColorBrush(palette.Accent),
                ToolTip = string.IsNullOrEmpty(title) ? url : title
            };
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri)) hyperlink.NavigateUri = uri;
            return hyperlink;
        }

        private static Span Wrap(Span span, IEnumerable<Core.Inline> children, ThemePalette palette)
        {
            AddInlines(span.Inlines, children, palette);
            return span;
        }
    }
}
