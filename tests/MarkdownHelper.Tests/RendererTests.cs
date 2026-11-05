using System;
using System.Linq;
using System.Text;
using MarkdownHelper.Core;
using Xunit;

namespace MarkdownHelper.Tests
{
    public class RendererTests
    {
        private const string Everything =
            "# Title\n\n## Sub\n\nThe fee fell from **$5 to $0**, *really* ~~maybe~~ `now`.\n\n" +
            "Line one  \nline two\n\n---\n\n- a\n- b\n  - nested\n\n1. one\n2. two\n\n- [ ] open\n- [x] done\n\n" +
            "- loose\n\n- items\n\n> quote\n>\n> > inner\n\n```csharp\nvar x = 1;\n```\n\n" +
            "| L | C | R |\n| :--- | :---: | ---: |\n| **1** | `2` | $3 |\n\n" +
            "[link](https://example.com \"title\") and ![pic](a.png) and https://example.org/x\n\n" +
            "Literal \\*stars\\* and \\# hash and 1\\. number";

        private static string Html(string markdown) => HtmlRenderer.Render(MarkdownParser.Parse(markdown));
        private static string Discord(string markdown) => DiscordRenderer.Format(MarkdownParser.Parse(markdown));

        [Fact]
        public void HtmlKeepsBoldAndDollars()
        {
            Assert.Equal("<p>The fee fell from <strong>$5 to $0</strong>.</p>\n", Html("The fee fell from **$5 to $0**."));
        }

        [Theory]
        [InlineData("# H", "<h1>H</h1>\n")]
        [InlineData("*i*", "<p><em>i</em></p>\n")]
        [InlineData("~~s~~", "<p><del>s</del></p>\n")]
        [InlineData("`c`", "<p><code>c</code></p>\n")]
        [InlineData("---", "<hr>\n")]
        [InlineData("a  \nb", "<p>a<br>\nb</p>\n")]
        [InlineData("```\n<x>\n```", "<pre><code>&lt;x&gt;</code></pre>\n")]
        [InlineData("> q", "<blockquote>\n<p>q</p>\n</blockquote>\n")]
        [InlineData("- a\n- b", "<ul>\n<li>a\n</li>\n<li>b\n</li>\n</ul>\n")]
        [InlineData("3. a", "<ol start=\"3\">\n<li>a\n</li>\n</ol>\n")]
        [InlineData("- [x] a", "<ul>\n<li>\u2611 a\n</li>\n</ul>\n")]
        public void HtmlConstructs(string markdown, string html) => Assert.Equal(html, Html(markdown));

        [Fact]
        public void HtmlTableKeepsAlignment()
        {
            string html = Html("| A | B |\n| :-: | --: |\n| 1 | 2 |");
            Assert.Contains("<table", html);
            Assert.Contains("text-align:center\">A</th>", html);
            Assert.Contains("text-align:right\">2</td>", html);
        }

        [Fact]
        public void RawHtmlIsEscaped()
        {
            string html = Html("<script>alert('x')</script> & more");
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&amp; more", html);
        }

        [Theory]
        [InlineData("[x](javascript:alert(1))")]
        [InlineData("[x](data:text/html,hi)")]
        [InlineData("![x](file:///c:/secret.txt)")]
        public void UnsafeLinksLoseTheirTarget(string markdown)
        {
            string html = Html(markdown);
            Assert.DoesNotContain("href", html);
            Assert.Contains("x", html);
        }

        [Fact]
        public void ImagesBecomeLinks()
        {
            Assert.Equal("<p><a href=\"https://example.com/a.png\">chart</a></p>\n", Html("![chart](https://example.com/a.png)"));
        }

        [Fact]
        public void NonAsciiTextStaysReadableInHtml()
        {
            string html = Html("§1.1 × 30% — café 🎉");
            string decoded = System.Net.WebUtility.HtmlDecode(html);
            Assert.Contains("§1.1 × 30% — café 🎉", decoded);
        }

        [Fact]
        public void DiscordTableBecomesAlignedCodeBlock()
        {
            string text = Discord("| Item | Cost |\n| --- | ---: |\n| **Fee** | $14 |\n| Long item name | $8.40 |");
            Assert.Equal(
                "```\n" +
                "Item             Cost\n" +
                "--------------  -----\n" +
                "Fee               $14\n" +
                "Long item name  $8.40\n" +
                "```", text);
        }

        [Theory]
        [InlineData("# One", "# One")]
        [InlineData("### Three", "### Three")]
        [InlineData("#### Four", "**Four**")]
        [InlineData("###### Six", "**Six**")]
        [InlineData("---", "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500")]
        [InlineData("- [ ] a\n- [x] b", "- \u2610 a\n- \u2611 b")]
        [InlineData("a  \nb", "a\nb")]
        [InlineData("> outer\n>\n> > inner", "> outer\n>\n> inner")]
        [InlineData("![pic](https://example.com/a.png)", "[pic](https://example.com/a.png)")]
        [InlineData("**$5 to $0**", "**$5 to $0**")]
        [InlineData("- a\n  - b", "- a\n  - b")]
        public void DiscordConversions(string markdown, string text) => Assert.Equal(text, Discord(markdown));

        [Fact]
        public void WriterRoundTripKeepsStructure()
        {
            var first = MarkdownParser.Parse(Everything);
            string written = MarkdownWriter.Write(first);
            Assert.Equal(NodeDump.Dump(first), NodeDump.Dump(written));
        }

        [Fact]
        public void WriterIsStableAfterOnePass()
        {
            string once = MarkdownWriter.Write(MarkdownParser.Parse(Everything));
            string twice = MarkdownWriter.Write(MarkdownParser.Parse(once));
            Assert.Equal(once, twice);
        }

        [Theory]
        [InlineData("2 \\* 3 \\* 4")]
        [InlineData("\\# not a heading")]
        [InlineData("\\- not a list")]
        [InlineData("1\\. not a list")]
        [InlineData("\\> not a quote")]
        [InlineData("\\`not code\\`")]
        [InlineData("\\[not\\](a-link)")]
        [InlineData("a\\_b\\_ c")]
        [InlineData("back\\\\slash")]
        [InlineData("\\<b>not html")]
        [InlineData("~\\~not strike\\~~")]
        public void WriterEscapesWhatWouldChangeMeaning(string markdown)
        {
            var first = MarkdownParser.Parse(markdown);
            Assert.Equal(NodeDump.Dump(first), NodeDump.Dump(MarkdownWriter.Write(first)));
        }

        [Fact]
        public void WriterLeavesDollarsAlone()
        {
            Assert.Equal("Costs $5 or $10.", MarkdownWriter.Write(MarkdownParser.Parse("Costs $5 or $10.")));
        }

        [Fact]
        public void WriterHandlesCodeContainingFences()
        {
            var doc = new MarkdownDocument();
            doc.Blocks.Add(new CodeBlock { Code = "```\ninner\n```" });
            Assert.Equal(NodeDump.Dump(doc), NodeDump.Dump(MarkdownWriter.Write(doc)));
        }

        [Fact]
        public void ClipboardHtmlOffsetsPointAtTheFragment()
        {
            const string fragment = "<p>§1.1 × 30% — café 🎉</p>";
            byte[] data = ClipboardHtml.Encode(fragment);
            string text = Encoding.UTF8.GetString(data);

            int Offset(string name)
            {
                int start = text.IndexOf(name + ":", StringComparison.Ordinal) + name.Length + 1;
                return int.Parse(text.Substring(start, 10));
            }

            int from = Offset("StartFragment"), to = Offset("EndFragment");
            Assert.Equal(fragment, Encoding.UTF8.GetString(data, from, to - from));
            Assert.Equal(data.Length, Offset("EndHTML"));

            string html = Encoding.UTF8.GetString(data, Offset("StartHTML"), data.Length - Offset("StartHTML"));
            Assert.StartsWith("<html>", html);
            Assert.EndsWith("</html>", html);
        }
    }
}
