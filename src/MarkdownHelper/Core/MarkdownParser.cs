using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Block parser for the CommonMark and GFM features this tool supports.
    /// There is deliberately no math support: a dollar sign is always plain text.
    /// </summary>
    public static class MarkdownParser
    {
        private static readonly Regex AtxHeading = new Regex(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))??(?:[ \t]+#+)?[ \t]*$", RegexOptions.Compiled);
        private static readonly Regex ThematicBreakLine = new Regex(@"^ {0,3}(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$", RegexOptions.Compiled);
        private static readonly Regex FenceOpen = new Regex(@"^( {0,3})(`{3,}|~{3,})[ \t]*([^`]*?)[ \t]*$", RegexOptions.Compiled);
        private static readonly Regex QuoteLine = new Regex(@"^ {0,3}> ?", RegexOptions.Compiled);
        private static readonly Regex SetextUnderline = new Regex(@"^ {0,3}(=+|-+)[ \t]*$", RegexOptions.Compiled);
        private static readonly Regex ListMarker = new Regex(@"^( *)([-*+]|\d{1,9}[.)])( +|\t|$)(.*)$", RegexOptions.Compiled);
        private static readonly Regex TaskMarker = new Regex(@"^\[( |x|X)\](?:[ \t]+|$)", RegexOptions.Compiled);
        private static readonly Regex DelimiterCell = new Regex(@"^:?-+:?$", RegexOptions.Compiled);

        private struct Line
        {
            public Line(string text, int number) { Text = text; Number = number; }
            public string Text;
            public int Number;
            public bool IsBlank => string.IsNullOrWhiteSpace(Text);
        }

        public static MarkdownDocument Parse(string markdown)
        {
            var document = new MarkdownDocument();
            if (string.IsNullOrEmpty(markdown)) return document;

            var lines = SplitLines(markdown).Select((text, i) => new Line(ExpandLeadingTabs(text), i)).ToList();
            document.Blocks.AddRange(ParseBlocks(lines));
            return document;
        }

        /// <summary>Splits text into lines the same way the parser numbers them.</summary>
        public static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static string ExpandLeadingTabs(string line)
        {
            int i = 0, column = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                column += line[i] == '\t' ? 4 - column % 4 : 1;
                i++;
            }
            return i == 0 ? line : new string(' ', column) + line.Substring(i);
        }

        private static int Indent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        private static List<Block> ParseBlocks(List<Line> lines)
        {
            var blocks = new List<Block>();
            int i = 0;
            while (i < lines.Count)
            {
                var line = lines[i];
                if (line.IsBlank) { i++; continue; }

                if (Indent(line.Text) >= 4) { blocks.Add(ParseIndentedCode(lines, ref i)); continue; }

                var fence = FenceOpen.Match(line.Text);
                if (fence.Success) { blocks.Add(ParseFence(lines, ref i, fence)); continue; }

                if (TryParseAtxHeading(line, out var heading)) { blocks.Add(heading); i++; continue; }

                if (ThematicBreakLine.IsMatch(line.Text))
                {
                    blocks.Add(new ThematicBreak { StartLine = line.Number, EndLine = line.Number });
                    i++;
                    continue;
                }

                if (QuoteLine.IsMatch(line.Text)) { blocks.Add(ParseQuote(lines, ref i)); continue; }

                if (ListMarker.IsMatch(line.Text) && Indent(line.Text) <= 3) { blocks.Add(ParseList(lines, ref i)); continue; }

                if (IsTableStart(lines, i)) { blocks.Add(ParseTable(lines, ref i)); continue; }

                blocks.Add(ParseParagraph(lines, ref i));
            }
            return blocks;
        }

        private static bool TryParseAtxHeading(Line line, out Heading heading)
        {
            heading = null;
            var match = AtxHeading.Match(line.Text);
            if (!match.Success) return false;
            heading = new Heading
            {
                Level = match.Groups[1].Length,
                Inlines = InlineParser.Parse(match.Groups[2].Value.Trim()),
                StartLine = line.Number,
                EndLine = line.Number
            };
            return true;
        }

        private static CodeBlock ParseIndentedCode(List<Line> lines, ref int i)
        {
            int start = i, last = i;
            var code = new List<string>();
            while (i < lines.Count && (lines[i].IsBlank || Indent(lines[i].Text) >= 4))
            {
                if (!lines[i].IsBlank) last = i;
                i++;
            }
            for (int k = start; k <= last; k++)
            {
                code.Add(lines[k].Text.Length >= 4 ? lines[k].Text.Substring(4) : "");
            }
            i = last + 1;
            return new CodeBlock
            {
                Code = string.Join("\n", code),
                StartLine = lines[start].Number,
                EndLine = lines[last].Number
            };
        }

        private static CodeBlock ParseFence(List<Line> lines, ref int i, Match open)
        {
            int indent = open.Groups[1].Length;
            string marker = open.Groups[2].Value;
            string info = open.Groups[3].Value.Trim();
            var closing = new Regex(@"^ {0,3}" + Regex.Escape(marker[0].ToString()) + "{" + marker.Length + @",}[ \t]*$");

            int start = i;
            i++;
            var code = new List<string>();
            int end = lines[start].Number;
            while (i < lines.Count)
            {
                end = lines[i].Number;
                if (closing.IsMatch(lines[i].Text)) { i++; break; }
                string text = lines[i].Text;
                int strip = Math.Min(indent, Indent(text));
                code.Add(text.Substring(strip));
                i++;
            }
            return new CodeBlock
            {
                Language = info.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
                Code = string.Join("\n", code),
                StartLine = lines[start].Number,
                EndLine = end
            };
        }

        private static Quote ParseQuote(List<Line> lines, ref int i)
        {
            int start = i;
            var inner = new List<Line>();
            bool paragraphOpen = false;
            while (i < lines.Count)
            {
                var line = lines[i];
                var marker = QuoteLine.Match(line.Text);
                if (marker.Success)
                {
                    string rest = line.Text.Substring(marker.Length);
                    inner.Add(new Line(rest, line.Number));
                    paragraphOpen = !string.IsNullOrWhiteSpace(rest) && !StartsNewBlock(lines, i, rest);
                    i++;
                    continue;
                }
                if (paragraphOpen && !line.IsBlank && !StartsNewBlock(lines, i, line.Text))
                {
                    inner.Add(line);
                    i++;
                    continue;
                }
                break;
            }
            return new Quote
            {
                Blocks = ParseBlocks(inner),
                StartLine = lines[start].Number,
                EndLine = lines[i - 1].Number
            };
        }

        private static bool StartsNewBlock(List<Line> lines, int index, string text)
        {
            if (AtxHeading.IsMatch(text) || FenceOpen.IsMatch(text) || ThematicBreakLine.IsMatch(text) || QuoteLine.IsMatch(text)) return true;
            if (Indent(text) <= 3 && ListCanInterruptParagraph(text)) return true;
            return IsTableStart(lines, index);
        }

        private static bool ListCanInterruptParagraph(string text)
        {
            var match = ListMarker.Match(text);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[4].Value)) return false;
            string marker = match.Groups[2].Value;
            return marker.Length == 1 || marker.TrimEnd('.', ')') == "1";
        }

        private static Block ParseParagraph(List<Line> lines, ref int i)
        {
            int start = i;
            var text = new List<string>();
            while (i < lines.Count && !lines[i].IsBlank)
            {
                if (i > start)
                {
                    var underline = SetextUnderline.Match(lines[i].Text);
                    if (underline.Success)
                    {
                        var heading = new Heading
                        {
                            Level = underline.Groups[1].Value[0] == '=' ? 1 : 2,
                            Inlines = InlineParser.Parse(JoinParagraph(text)),
                            StartLine = lines[start].Number,
                            EndLine = lines[i].Number
                        };
                        i++;
                        return heading;
                    }
                    if (StartsNewBlock(lines, i, lines[i].Text)) break;
                }
                text.Add(lines[i].Text);
                i++;
            }
            return new Paragraph
            {
                Inlines = InlineParser.Parse(JoinParagraph(text)),
                StartLine = lines[start].Number,
                EndLine = lines[i - 1].Number
            };
        }

        private static string JoinParagraph(List<string> text)
        {
            var builder = new StringBuilder();
            for (int k = 0; k < text.Count; k++)
            {
                string line = text[k].TrimStart(' ');
                if (k == text.Count - 1) line = line.TrimEnd(' ', '\t');
                if (k > 0) builder.Append('\n');
                builder.Append(line);
            }
            return builder.ToString();
        }

        private struct Marker
        {
            public int Indent;
            public int ContentOffset;
            public bool Ordered;
            public char Kind;
            public int Number;
            public string Rest;
        }

        private static bool TryMarker(string text, out Marker marker)
        {
            marker = default(Marker);
            if (ThematicBreakLine.IsMatch(text)) return false;
            var match = ListMarker.Match(text);
            if (!match.Success) return false;

            string symbol = match.Groups[2].Value;
            string spacing = match.Groups[3].Value;
            string rest = match.Groups[4].Value;
            int spaces = spacing == "\t" ? 1 : spacing.Length;
            bool blankStart = string.IsNullOrWhiteSpace(rest);

            marker.Indent = match.Groups[1].Length;
            marker.Ordered = symbol.Length > 1;
            marker.Kind = symbol[symbol.Length - 1];
            marker.Number = marker.Ordered ? int.Parse(symbol.Substring(0, symbol.Length - 1)) : 1;

            // Five or more spaces after the marker means the content is indented code.
            int gap = blankStart || spaces >= 5 ? 1 : spaces;
            marker.ContentOffset = marker.Indent + symbol.Length + gap;
            marker.Rest = blankStart ? "" : (spaces >= 5 ? new string(' ', spaces - 1) : "") + rest;
            return true;
        }

        private static ListBlock ParseList(List<Line> lines, ref int i)
        {
            TryMarker(lines[i].Text, out var first);
            var list = new ListBlock { Ordered = first.Ordered, Start = first.Number, StartLine = lines[i].Number };

            while (i < lines.Count)
            {
                if (!TryMarker(lines[i].Text, out var marker) || marker.Ordered != first.Ordered || marker.Kind != first.Kind || marker.Indent > 3)
                {
                    break;
                }

                var item = ParseListItem(lines, ref i, marker);
                list.Items.Add(item);

                int next = i;
                while (next < lines.Count && lines[next].IsBlank) next++;
                if (next >= lines.Count) { i = next; break; }

                if (next > i && TryMarker(lines[next].Text, out var following)
                    && following.Ordered == first.Ordered && following.Kind == first.Kind && following.Indent <= 3)
                {
                    i = next;
                    continue;
                }
                if (next > i) { i = next; break; }
            }

            list.EndLine = list.Items[list.Items.Count - 1].EndLine;
            list.Tight = IsTight(list);
            return list;
        }

        private static ListItem ParseListItem(List<Line> lines, ref int i, Marker marker)
        {
            var item = new ListItem { StartLine = lines[i].Number };
            string first = marker.Rest;

            var task = TaskMarker.Match(first);
            if (task.Success)
            {
                item.Checked = task.Groups[1].Value != " ";
                first = first.Substring(task.Length);
            }

            var inner = new List<Line> { new Line(first, lines[i].Number) };
            int lastContent = i;
            bool paragraphOpen = !string.IsNullOrWhiteSpace(first);
            i++;

            while (i < lines.Count)
            {
                var line = lines[i];
                if (line.IsBlank)
                {
                    inner.Add(new Line("", line.Number));
                    paragraphOpen = false;
                    i++;
                    continue;
                }

                int indent = Indent(line.Text);
                if (indent >= marker.ContentOffset)
                {
                    string stripped = line.Text.Substring(marker.ContentOffset);
                    inner.Add(new Line(stripped, line.Number));
                    paragraphOpen = !StartsNewBlock(lines, i, stripped);
                    lastContent = i;
                    i++;
                    continue;
                }

                // AI output often nests with two spaces under "1." which strict CommonMark
                // would treat as ending the list and restarting the numbering.
                if (indent >= marker.Indent + 2 && TryMarker(line.Text, out _))
                {
                    inner.Add(new Line(line.Text.Substring(indent), line.Number));
                    paragraphOpen = false;
                    lastContent = i;
                    i++;
                    continue;
                }

                if (paragraphOpen && inner[inner.Count - 1].Text.Length > 0 && !TryMarker(line.Text, out _) && !StartsNewBlock(lines, i, line.Text))
                {
                    inner.Add(new Line(line.Text.TrimStart(' '), line.Number));
                    lastContent = i;
                    i++;
                    continue;
                }
                break;
            }

            // Trailing blank lines belong to whatever comes next, not to this item.
            i = lastContent + 1;
            int keep = inner.FindLastIndex(l => !l.IsBlank);
            inner = keep < 0 ? new List<Line>() : inner.Take(keep + 1).ToList();

            item.Blocks = ParseBlocks(inner);
            item.EndLine = lines[lastContent].Number;
            return item;
        }

        private static bool IsTight(ListBlock list)
        {
            for (int k = 0; k < list.Items.Count; k++)
            {
                var item = list.Items[k];
                if (k > 0 && item.StartLine - list.Items[k - 1].EndLine > 1) return false;
                for (int b = 1; b < item.Blocks.Count; b++)
                {
                    if (item.Blocks[b].StartLine - item.Blocks[b - 1].EndLine > 1) return false;
                }
            }
            return true;
        }

        private static bool IsTableStart(List<Line> lines, int index)
        {
            if (index + 1 >= lines.Count) return false;
            string header = lines[index].Text;
            string delimiter = lines[index + 1].Text;
            if (!header.Contains("|") || !delimiter.Contains("-")) return false;
            if (Indent(header) > 3) return false;

            var cells = SplitRow(delimiter);
            return cells.Count > 0
                && cells.All(c => DelimiterCell.IsMatch(c))
                && cells.Count == SplitRow(header).Count;
        }

        private static Table ParseTable(List<Line> lines, ref int i)
        {
            var header = SplitRow(lines[i].Text);
            var aligns = SplitRow(lines[i + 1].Text).Select(ToAlign).ToList();
            var table = new Table
            {
                Aligns = aligns,
                Header = header.Select(InlineParser.Parse).ToList(),
                StartLine = lines[i].Number
            };

            i += 2;
            while (i < lines.Count && !lines[i].IsBlank && !StartsNewBlock(lines, i, lines[i].Text))
            {
                var cells = SplitRow(lines[i].Text);
                var row = new List<List<Inline>>();
                for (int c = 0; c < aligns.Count; c++)
                {
                    row.Add(InlineParser.Parse(c < cells.Count ? cells[c] : ""));
                }
                table.Rows.Add(row);
                i++;
            }

            table.EndLine = lines[i - 1].Number;
            return table;
        }

        private static ColumnAlign ToAlign(string cell)
        {
            bool left = cell.StartsWith(":", StringComparison.Ordinal);
            bool right = cell.EndsWith(":", StringComparison.Ordinal);
            if (left && right) return ColumnAlign.Center;
            if (right) return ColumnAlign.Right;
            return left ? ColumnAlign.Left : ColumnAlign.None;
        }

        /// <summary>Splits a table row on unescaped pipes, dropping the optional outer pipes.</summary>
        private static List<string> SplitRow(string row)
        {
            string text = row.Trim();
            if (text.StartsWith("|", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.EndsWith("|", StringComparison.Ordinal) && !text.EndsWith(@"\|", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1);
            }

            var cells = new List<string>();
            var cell = new StringBuilder();
            for (int k = 0; k < text.Length; k++)
            {
                if (text[k] == '\\' && k + 1 < text.Length) { cell.Append(text[k]).Append(text[k + 1]); k++; continue; }
                if (text[k] == '|') { cells.Add(cell.ToString().Trim()); cell.Clear(); continue; }
                cell.Append(text[k]);
            }
            cells.Add(cell.ToString().Trim());
            return cells;
        }
    }
}
