using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Writes a document back to normalized markdown. Parsing the result gives the same
    /// document. DiscordRenderer builds on this and changes what Discord cannot show.
    /// </summary>
    public class MarkdownWriter
    {
        private static readonly Regex OrderedStart = new Regex(@"^(\d{1,9})([.)])(\s|$)", RegexOptions.Compiled);
        private static readonly Regex BulletStart = new Regex(@"^[-+*](\s|$)", RegexOptions.Compiled);
        private static readonly Regex RuleOrUnderline = new Regex(@"^(=+|-+|\*+|_+)\s*$", RegexOptions.Compiled);

        public static string Write(MarkdownDocument document) => new MarkdownWriter().Render(document);

        public string Render(MarkdownDocument document)
        {
            return string.Join("\n\n", document.Blocks.Select(WriteBlock));
        }

        protected virtual string WriteBlock(Block block)
        {
            switch (block)
            {
                case Heading heading: return WriteHeading(heading);
                case Paragraph paragraph: return WriteParagraph(paragraph.Inlines);
                case ThematicBreak _: return WriteRule();
                case CodeBlock code: return WriteCode(code);
                case Quote quote: return WriteQuote(quote);
                case ListBlock list: return WriteList(list);
                case Table table: return WriteTable(table);
                default: return "";
            }
        }

        protected virtual string WriteHeading(Heading heading)
        {
            return new string('#', heading.Level) + " " + WriteInlines(heading.Inlines).Replace("\n", " ");
        }

        protected virtual string WriteRule() => "---";

        private string WriteParagraph(List<Inline> inlines)
        {
            string text = WriteInlines(inlines);
            var lines = text.Split('\n').Select(EscapeLineStart);
            return string.Join("\n", lines);
        }

        protected virtual string EscapeLineStart(string line)
        {
            if (line.Length == 0) return line;
            if (line[0] == '#' || line[0] == '>') return "\\" + line;
            if (BulletStart.IsMatch(line) || RuleOrUnderline.IsMatch(line)) return "\\" + line;
            var ordered = OrderedStart.Match(line);
            if (ordered.Success)
            {
                int digits = ordered.Groups[1].Length;
                return line.Substring(0, digits) + "\\" + line.Substring(digits);
            }
            return line;
        }

        private static string WriteCode(CodeBlock code)
        {
            string fence = "```";
            while (code.Code.Contains(fence)) fence += "`";
            return fence + code.Language + "\n" + code.Code + "\n" + fence;
        }

        protected virtual string WriteQuote(Quote quote)
        {
            string body = string.Join("\n\n", quote.Blocks.Select(WriteBlock));
            return string.Join("\n", body.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l));
        }

        private string WriteList(ListBlock list)
        {
            var items = new List<string>();
            for (int i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                string marker = list.Ordered ? (list.Start + i) + ". " : "- ";
                string task = TaskPrefix(item);
                string separator = list.Tight ? "\n" : "\n\n";
                string body = string.Join(separator, item.Blocks.Select(WriteBlock));

                string pad = new string(' ', marker.Length);
                var lines = (task + body).Split('\n');
                var result = new StringBuilder(marker + lines[0]);
                for (int k = 1; k < lines.Length; k++)
                {
                    result.Append('\n');
                    if (lines[k].Length > 0) result.Append(pad).Append(lines[k]);
                }
                items.Add(result.ToString());
            }
            return string.Join(list.Tight ? "\n" : "\n\n", items);
        }

        protected virtual string TaskPrefix(ListItem item)
        {
            if (item.Checked == null) return "";
            return item.Checked.Value ? "[x] " : "[ ] ";
        }

        protected virtual string WriteTable(Table table)
        {
            var lines = new List<string> { TableRow(table.Header) };
            lines.Add("| " + string.Join(" | ", table.Aligns.Select(AlignCell)) + " |");
            lines.AddRange(table.Rows.Select(TableRow));
            return string.Join("\n", lines);
        }

        private string TableRow(List<List<Inline>> cells)
        {
            var text = cells.Select(c => WriteInlines(c).Replace("\n", " ").Replace("|", "\\|"));
            return "| " + string.Join(" | ", text) + " |";
        }

        private static string AlignCell(ColumnAlign align)
        {
            switch (align)
            {
                case ColumnAlign.Left: return ":---";
                case ColumnAlign.Center: return ":---:";
                case ColumnAlign.Right: return "---:";
                default: return "---";
            }
        }

        protected string WriteInlines(List<Inline> inlines)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < inlines.Count; i++)
            {
                char next = i + 1 < inlines.Count && inlines[i + 1] is Text following && following.Value.Length > 0 ? following.Value[0] : '\0';
                char previous = builder.Length > 0 ? builder[builder.Length - 1] : '\0';
                builder.Append(WriteInline(inlines[i], previous, next));
            }
            return builder.ToString();
        }

        private string WriteInline(Inline inline, char previous, char next)
        {
            switch (inline)
            {
                case Text text: return EscapeText(text.Value, previous, next);
                case SoftBreak _: return "\n";
                case HardBreak _: return WriteHardBreak();
                case CodeSpan code: return WriteCodeSpan(code.Code);
                case Strong strong: return Wrap("**", strong.Children);
                case Emphasis emphasis: return Wrap("*", emphasis.Children);
                case Strike strike: return Wrap("~~", strike.Children);
                case Link link: return WriteLink(link);
                case Image image: return WriteImage(image);
                default: return "";
            }
        }

        protected virtual string WriteHardBreak() => "  \n";

        private static string WriteCodeSpan(string code)
        {
            string fence = "`";
            while (code.Contains(fence)) fence += "`";
            bool pad = code.StartsWith("`", StringComparison.Ordinal) || code.EndsWith("`", StringComparison.Ordinal);
            string space = pad ? " " : "";
            return fence + space + code + space + fence;
        }

        private string Wrap(string marker, List<Inline> children)
        {
            string body = WriteInlines(children);
            string trimmed = body.Trim(' ');
            if (trimmed.Length == 0) return body;

            int leading = body.Length - body.TrimStart(' ').Length;
            int trailing = body.Length - body.TrimEnd(' ').Length;
            return new string(' ', leading) + marker + trimmed + marker + new string(' ', trailing);
        }

        private string WriteLink(Link link)
        {
            string text = WriteInlines(link.Children);
            string plain = InlineParser.PlainText(link.Children);
            if (plain == link.Url && link.Title.Length == 0 && IsWebUrl(link.Url)) return link.Url;
            return "[" + text + "](" + WriteDestination(link.Url) + WriteTitle(link.Title) + ")";
        }

        protected virtual string WriteImage(Image image)
        {
            return "![" + EscapeText(image.Alt, '\0', '\0') + "](" + WriteDestination(image.Url) + WriteTitle(image.Title) + ")";
        }

        private static bool IsWebUrl(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string WriteDestination(string url)
        {
            bool unsafeChars = url.Any(c => c == ' ' || c == '(' || c == ')');
            return unsafeChars ? "<" + url + ">" : url;
        }

        private static string WriteTitle(string title)
        {
            return title.Length == 0 ? "" : " \"" + title.Replace("\"", "'") + "\"";
        }

        /// <summary>Escapes only what would otherwise change meaning. Dollar signs stay as they are.</summary>
        protected virtual string EscapeText(string value, char previous, char next)
        {
            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                char before = i > 0 ? value[i - 1] : previous;
                char after = i + 1 < value.Length ? value[i + 1] : next;

                bool escape;
                switch (c)
                {
                    case '\\':
                        escape = after == '\0' || "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".IndexOf(after) >= 0;
                        break;
                    case '*':
                    case '`':
                    case '[':
                    case ']':
                        escape = true;
                        break;
                    case '_':
                        escape = !(char.IsLetterOrDigit(before) && char.IsLetterOrDigit(after));
                        break;
                    case '~':
                        escape = after == '~' || before == '~';
                        break;
                    case '<':
                        escape = char.IsLetter(after) || after == '/' || after == '!';
                        break;
                    case '!':
                        escape = after == '[';
                        break;
                    default:
                        escape = false;
                        break;
                }

                if (escape) builder.Append('\\');
                builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
