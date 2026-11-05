using System.Linq;
using MarkdownHelper.Core;
using Xunit;

namespace MarkdownHelper.Tests
{
    public class ParserTests
    {
        private static void Expect(string markdown, string dump) => Assert.Equal(dump, NodeDump.Dump(markdown));

        [Fact]
        public void EmptyInputGivesNoBlocks()
        {
            Assert.Empty(MarkdownParser.Parse("").Blocks);
            Assert.Empty(MarkdownParser.Parse(null).Blocks);
            Assert.Empty(MarkdownParser.Parse("   \n\n  ").Blocks);
        }

        [Theory]
        [InlineData("# One", "h1(One)")]
        [InlineData("### Three ###", "h3(Three)")]
        [InlineData("###### Six", "h6(Six)")]
        [InlineData("####### Seven", "p(####### Seven)")]
        [InlineData("#NoSpace", "p(#NoSpace)")]
        [InlineData("Title\n=====", "h1(Title)")]
        [InlineData("Title\n---", "h2(Title)")]
        public void Headings(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("The fee fell from **$5 to $0**, as planned.", "p(The fee fell from {b:$5 to $0}, as planned.)")]
        [InlineData("It costs $5 and then $10 later.", "p(It costs $5 and then $10 later.)")]
        [InlineData("**$35 × 30% = $10.50**, and **$33 × 30% = $9.90**.", "p({b:$35 × 30% = $10.50}, and {b:$33 × 30% = $9.90}.)")]
        [InlineData("Math like $x^2$ stays literal.", "p(Math like $x^2$ stays literal.)")]
        [InlineData("$$\nx = 1\n$$", "p($${soft}x = 1{soft}$$)")]
        public void DollarSignsAreNeverMath(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("*a* and _b_", "p({i:a} and {i:b})")]
        [InlineData("**a** and __b__", "p({b:a} and {b:b})")]
        [InlineData("***both***", "p({i:{b:both}})")]
        [InlineData("**bold *inner* bold**", "p({b:bold {i:inner} bold})")]
        [InlineData("~~gone~~", "p({s:gone})")]
        [InlineData("a ~ b ~ c", "p(a ~ b ~ c)")]
        [InlineData("snake_case_name stays", "p(snake_case_name stays)")]
        [InlineData("5 * 3 * 2", "p(5 * 3 * 2)")]
        [InlineData("a * b", "p(a * b)")]
        [InlineData("**never closed", "p(**never closed)")]
        [InlineData("**Note:**text after", "p({b:Note:}text after)")]
        [InlineData("**§1.1** at the start", "p({b:§1.1} at the start)")]
        public void Emphasis(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("`code` here", "p({code:code} here)")]
        [InlineData("`` a`b ``", "p({code:a`b})")]
        [InlineData("`*not bold*`", "p({code:*not bold*})")]
        [InlineData("`unclosed", "p(`unclosed)")]
        public void CodeSpans(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData(@"\*literal\*", "p(*literal*)")]
        [InlineData(@"\$5", "p($5)")]
        [InlineData(@"back\slash", @"p(back\slash)")]
        public void Escapes(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("[text](https://example.com)", "p({a:https://example.com|text})")]
        [InlineData("[text](https://example.com \"A title\")", "p({a:https://example.com|text})")]
        [InlineData("[**bold**](https://example.com/a_(b))", "p({a:https://example.com/a_(b)|{b:bold}})")]
        [InlineData("<https://example.com>", "p({a:https://example.com|https://example.com})")]
        [InlineData("<me@example.com>", "p({a:mailto:me@example.com|me@example.com})")]
        [InlineData("See https://example.com/x.", "p(See {a:https://example.com/x|https://example.com/x}.)")]
        [InlineData("(see https://example.com/x)", "p((see {a:https://example.com/x|https://example.com/x}))")]
        [InlineData("![alt text](pic.png)", "p({img:pic.png|alt text})")]
        [InlineData("citation [1] stays", "p(citation [1] stays)")]
        [InlineData("[not a link]", "p([not a link])")]
        public void Links(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("one\ntwo", "p(one{soft}two)")]
        [InlineData("one  \ntwo", "p(one{hard}two)")]
        [InlineData("one\\\ntwo", "p(one{hard}two)")]
        public void LineBreaks(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("---", "hr")]
        [InlineData("***", "hr")]
        [InlineData("_ _ _", "hr")]
        public void ThematicBreaks(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("```\nx = 1\n```", "code[](x = 1)")]
        [InlineData("```csharp title\nvar a;\n```", "code[csharp](var a;)")]
        [InlineData("~~~\ncode\n~~~", "code[](code)")]
        [InlineData("```\nnever closed", "code[](never closed)")]
        [InlineData("```\n**not bold**\n\n# not heading\n```", "code[](**not bold**\n\n# not heading)")]
        [InlineData("    indented\n\n    more", "code[](indented\n\nmore)")]
        public void CodeBlocks(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("> quoted", "quote(p(quoted))")]
        [InlineData("> one\ncontinues", "quote(p(one{soft}continues))")]
        [InlineData("> outer\n>> inner", "quote(p(outer),quote(p(inner)))")]
        [InlineData("> # Head\n> body", "quote(h1(Head),p(body))")]
        public void Quotes(string markdown, string dump) => Expect(markdown, dump);

        [Theory]
        [InlineData("- a\n- b", "ul(p(a);p(b))")]
        [InlineData("* a\n* b", "ul(p(a);p(b))")]
        [InlineData("1. a\n2. b", "ol1(p(a);p(b))")]
        [InlineData("3) a\n4) b", "ol3(p(a);p(b))")]
        [InlineData("- a\n\n- b", "ul-loose(p(a);p(b))")]
        [InlineData("- a\n  - b\n    - c", "ul(p(a),ul(p(b),ul(p(c))))")]
        [InlineData("- a\n    - four space nesting", "ul(p(a),ul(p(four space nesting)))")]
        [InlineData("1. a\n  - two space under number\n2. b", "ol1(p(a),ul(p(two space under number));p(b))")]
        [InlineData("- a\n  continues", "ul(p(a{soft}continues))")]
        [InlineData("- a\nlazy", "ul(p(a{soft}lazy))")]
        [InlineData("- [ ] open\n- [x] done", "ul([ ]p(open);[x]p(done))")]
        [InlineData("- a\n\n  second paragraph", "ul-loose(p(a),p(second paragraph))")]
        [InlineData("- a\n\ntext after", "ul(p(a))\np(text after)")]
        [InlineData("- a\n* b", "ul(p(a))\nul(p(b))")]
        [InlineData("- item\n  ```\n  code\n  ```", "ul(p(item),code[](code))")]
        public void Lists(string markdown, string dump) => Expect(markdown, dump);

        [Fact]
        public void ListInterruptsParagraphOnlyWhenItStartsAtOne()
        {
            Expect("text\n- item", "p(text)\nul(p(item))");
            Expect("text\n1. item", "p(text)\nol1(p(item))");
            Expect("In 2024\n2. not a list", "p(In 2024{soft}2. not a list)");
        }

        [Theory]
        [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 |", "table[None,None](A|B // 1|2)")]
        [InlineData("A | B\n--- | ---\n1 | 2", "table[None,None](A|B // 1|2)")]
        [InlineData("| L | C | R |\n| :-- | :-: | --: |", "table[Left,Center,Right](L|C|R // )")]
        [InlineData("| A | B |\n| --- | --- |\n| only one |", "table[None,None](A|B // only one|)")]
        [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 | 3 |", "table[None,None](A|B // 1|2)")]
        [InlineData(@"| A |" + "\n| --- |\n" + @"| x \| y |", "table[None](A // x | y)")]
        [InlineData("| **A** |\n| --- |\n| `c` |", "table[None]({b:A} // {code:c})")]
        [InlineData("Intro:\n| A |\n| --- |\n| 1 |", "p(Intro:)\ntable[None](A // 1)")]
        [InlineData("| A | B |\n| --- |\n", "p(| A | B |{soft}| --- |)")]
        public void Tables(string markdown, string dump) => Expect(markdown, dump);

        [Fact]
        public void RawHtmlIsPlainText()
        {
            Expect("<b>hi</b>", "p(<b>hi</b>)");
            Expect("<script>alert(1)</script>", "p(<script>alert(1)</script>)");
        }

        [Fact]
        public void WindowsLineEndingsWork()
        {
            Expect("# Head\r\n\r\ntext\r\n", "h1(Head)\np(text)");
        }

        [Fact]
        public void BlocksCarrySourceLines()
        {
            var blocks = MarkdownParser.Parse("# Head\n\ntext\nmore\n\n- a\n- b\n\n```\nx\n```").Blocks;
            Assert.Equal(new[] { (0, 0), (2, 3), (5, 6), (8, 10) }, blocks.Select(b => (b.StartLine, b.EndLine)));

            var list = (ListBlock)blocks[2];
            Assert.Equal(new[] { (5, 5), (6, 6) }, list.Items.Select(b => (b.StartLine, b.EndLine)));
        }

        [Fact]
        public void NestedLinesKeepTheirSourceNumbers()
        {
            var quote = (Quote)MarkdownParser.Parse("text\n\n> one\n>\n> two").Blocks[1];
            Assert.Equal(new[] { 2, 4 }, quote.Blocks.Select(b => b.StartLine));
        }

        [Fact]
        public void DeeplyNestedInputDoesNotBlowUp()
        {
            string deep = string.Concat(Enumerable.Repeat("> ", 200)) + "text";
            Assert.NotEmpty(MarkdownParser.Parse(deep).Blocks);
            string stars = string.Concat(Enumerable.Repeat("*a ", 5000));
            Assert.NotEmpty(MarkdownParser.Parse(stars).Blocks);
        }
    }
}
