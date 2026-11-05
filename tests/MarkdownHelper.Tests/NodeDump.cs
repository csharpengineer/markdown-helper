using System.Collections.Generic;
using System.Linq;
using System.Text;
using MarkdownHelper.Core;

namespace MarkdownHelper.Tests
{
    /// <summary>Compact text form of a document, for comparing structure in tests.</summary>
    internal static class NodeDump
    {
        public static string Dump(string markdown) => Dump(MarkdownParser.Parse(markdown));

        public static string Dump(MarkdownDocument document) => string.Join("\n", document.Blocks.Select(Block));

        public static string Inlines(IEnumerable<Inline> inlines)
        {
            var text = new StringBuilder();
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Text t: text.Append(t.Value); break;
                    case SoftBreak _: text.Append("{soft}"); break;
                    case HardBreak _: text.Append("{hard}"); break;
                    case CodeSpan c: text.Append("{code:").Append(c.Code).Append('}'); break;
                    case Strong s: text.Append("{b:").Append(Inlines(s.Children)).Append('}'); break;
                    case Emphasis e: text.Append("{i:").Append(Inlines(e.Children)).Append('}'); break;
                    case Strike s: text.Append("{s:").Append(Inlines(s.Children)).Append('}'); break;
                    case Link l: text.Append("{a:").Append(l.Url).Append('|').Append(Inlines(l.Children)).Append('}'); break;
                    case Image i: text.Append("{img:").Append(i.Url).Append('|').Append(i.Alt).Append('}'); break;
                }
            }
            return text.ToString();
        }

        private static string Block(Block block)
        {
            switch (block)
            {
                case Heading h: return "h" + h.Level + "(" + Inlines(h.Inlines) + ")";
                case Paragraph p: return "p(" + Inlines(p.Inlines) + ")";
                case ThematicBreak _: return "hr";
                case CodeBlock c: return "code[" + c.Language + "](" + c.Code + ")";
                case Quote q: return "quote(" + string.Join(",", q.Blocks.Select(Block)) + ")";
                case ListBlock l:
                    string kind = (l.Ordered ? "ol" + l.Start : "ul") + (l.Tight ? "" : "-loose");
                    return kind + "(" + string.Join(";", l.Items.Select(Item)) + ")";
                case Table t:
                    string aligns = string.Join(",", t.Aligns);
                    string head = string.Join("|", t.Header.Select(Inlines));
                    string rows = string.Join("/", t.Rows.Select(r => string.Join("|", r.Select(Inlines))));
                    return "table[" + aligns + "](" + head + " // " + rows + ")";
                default: return "?";
            }
        }

        private static string Item(ListItem item)
        {
            string check = item.Checked == null ? "" : item.Checked.Value ? "[x]" : "[ ]";
            return check + string.Join(",", item.Blocks.Select(Block));
        }
    }
}
