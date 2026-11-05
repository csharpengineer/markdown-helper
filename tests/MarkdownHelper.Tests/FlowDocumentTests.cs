using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MarkdownHelper.Core;
using MarkdownHelper.UI;
using Xunit;
using WpfParagraph = System.Windows.Documents.Paragraph;

namespace MarkdownHelper.Tests
{
    public class FlowDocumentTests
    {
        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception e) { failure = e; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        private static string RoundTrip(string markdown)
        {
            var flow = FlowDocumentBuilder.Build(MarkdownParser.Parse(markdown), ThemePalette.Light);
            return FlowMarkdownReader.Read(flow);
        }

        [Theory]
        [InlineData("# One")]
        [InlineData("###### Six")]
        [InlineData("Plain text.")]
        [InlineData("The fee fell from **$5 to $0**, as planned.")]
        [InlineData("*italic* and ~~gone~~ and `code`")]
        [InlineData("**bold *inner* bold**")]
        [InlineData("[text](https://example.com)")]
        [InlineData("[**bold** link](https://example.com \"Title\")")]
        [InlineData("![alt text](https://example.com/a.png)")]
        [InlineData("line one  \nline two")]
        [InlineData("---")]
        [InlineData("```csharp\nvar x = 1;\n\nvar y = 2;\n```")]
        [InlineData("> quoted\n>\n> > inner")]
        [InlineData("- a\n- b\n  - nested")]
        [InlineData("3. three\n4. four")]
        [InlineData("- [ ] open\n- [x] done")]
        [InlineData("| L | C | R |\n| --- | :---: | ---: |\n| **1** | `2` | $3 |")]
        public void BuildThenReadKeepsMeaning(string markdown)
        {
            OnSta(() => Assert.Equal(NodeDump.Dump(markdown), NodeDump.Dump(RoundTrip(markdown))));
        }

        [Fact]
        public void ReadingAfterTheEditorHasTouchedTheDocumentKeepsEverything()
        {
            const string markdown =
                "# Title\n\nSome **bold** and [a link](https://example.com).\n\n- one\n- two\n\n" +
                "| A | B |\n| --- | --- |\n| 1 | 2 |\n\n```\ncode\n```\n\n> quote";

            OnSta(() =>
            {
                var flow = FlowDocumentBuilder.Build(MarkdownParser.Parse(markdown), ThemePalette.Light);
                var box = new RichTextBox { Document = flow };
                var paragraph = (WpfParagraph)flow.Blocks.ElementAt(1);
                paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).InsertTextInRun("Edited. ");

                string read = FlowMarkdownReader.Read(box.Document);
                string expected = markdown.Replace("Some **bold**", "Edited. Some **bold**");
                Assert.Equal(NodeDump.Dump(expected), NodeDump.Dump(read));
            });
        }

        [Fact]
        public void BoldAppliedByTheEditorIsRead()
        {
            OnSta(() =>
            {
                var flow = FlowDocumentBuilder.Build(MarkdownParser.Parse("plain words here"), ThemePalette.Light);
                var box = new RichTextBox { Document = flow };
                var start = flow.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                var range = new TextRange(start.GetPositionAtOffset(6), start.GetPositionAtOffset(11));
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                Assert.Equal("plain **words** here", FlowMarkdownReader.Read(box.Document));
            });
        }

        [Fact]
        public void SplittingAHeadingKeepsBothHalvesAsHeadings()
        {
            OnSta(() =>
            {
                var flow = FlowDocumentBuilder.Build(MarkdownParser.Parse("## Two words"), ThemePalette.Light);
                var box = new RichTextBox { Document = flow };
                var start = flow.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                start.GetPositionAtOffset(4).InsertParagraphBreak();
                Assert.Equal("## Two\n\n## words", FlowMarkdownReader.Read(box.Document).Replace(" \n", "\n"));
            });
        }

        [Theory]
        [InlineData("* star at start")]
        [InlineData("# not a heading")]
        [InlineData("2 * 3 * 4 and a_b_c and `tick`")]
        [InlineData("1. not a list")]
        [InlineData("Cost is $5 or $10")]
        public void TypedSymbolsStayLiteral(string typed)
        {
            OnSta(() =>
            {
                var flow = FlowDocumentBuilder.Build(new MarkdownDocument(), ThemePalette.Light);
                flow.Blocks.Add(new WpfParagraph(new Run(typed)));
                string markdown = FlowMarkdownReader.Read(flow);
                var parsed = MarkdownParser.Parse(markdown);
                var paragraph = Assert.IsType<MarkdownHelper.Core.Paragraph>(parsed.Blocks.Single());
                Assert.Equal(typed, InlineParser.PlainText(paragraph.Inlines));
            });
        }

        [Fact]
        public void EmptyDocumentReadsAsEmpty()
        {
            OnSta(() => Assert.Equal("", FlowMarkdownReader.Read(new FlowDocument(new WpfParagraph()))));
        }

        [Fact]
        public void PiecesBuildForEveryKindOfBlock()
        {
            OnSta(() =>
            {
                var session = new EditSession();
                session.Commit("# H\n\ntext\n\n- a\n- b\n\n3. c\n\n> q\n\n```\nx\n```\n\n| A |\n| --- |\n| 1 |\n\n---");
                foreach (var piece in session.Pieces())
                {
                    var flow = FlowDocumentBuilder.BuildPiece(piece, ThemePalette.Dark);
                    Assert.Single(flow.Blocks);
                }
                var ordered = session.Pieces().First(p => p.OwnerList != null && p.OwnerList.Ordered);
                var list = (System.Windows.Documents.List)FlowDocumentBuilder.BuildPiece(ordered, ThemePalette.Light).Blocks.FirstBlock;
                Assert.Equal(3, list.StartIndex);
            });
        }
    }
}
