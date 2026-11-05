using System.Linq;
using MarkdownHelper.Core;
using Xunit;

namespace MarkdownHelper.Tests
{
    public class DetectorTests
    {
        [Theory]
        [InlineData("# Summary\n\nThis is **important** text.")]
        [InlineData("Some **bold** text and a `code` span here.")]
        [InlineData("Intro paragraph here.\n\n- one\n- two\n\nSee [the docs](https://example.com).")]
        [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 |")]
        [InlineData("Run this:\n\n```\ndir\n```")]
        [InlineData("## Steps\n\n1. First\n2. Second")]
        [InlineData("> A quote worth keeping\n\nAnd **bold** after it.")]
        public void RecognisesMarkdown(string text) => Assert.True(MarkdownDetector.IsMarkdown(text));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("hello")]
        [InlineData("Just an ordinary sentence that someone copied from a page.")]
        [InlineData("One stray * asterisk in a longer sentence of text.")]
        [InlineData("Only **one** sign of markdown in this sentence.")]
        [InlineData("# Just a heading on its own, with nothing else")]
        [InlineData("- a single line that starts with a dash and more")]
        [InlineData("- one\n- two\n- three\n- four, a plain list only")]
        [InlineData("https://example.com/some/long/address?with=query")]
        [InlineData("It costs $5 today and $10 tomorrow, so buy now.")]
        public void IgnoresOrdinaryText(string text) => Assert.False(MarkdownDetector.IsMarkdown(text));

        [Theory]
        [InlineData("# comment\ndef call(*args, **kwargs):\n    return run(**kwargs)\n")]
        [InlineData("# build settings\nname: app\nversion: 1\nitems:\n  - one: 1\n  - two: 2\n")]
        [InlineData("#include <stdio.h>\nint main() {\n    int a = 2 ** 3;\n    return 0;\n}\n")]
        [InlineData("public class Thing\n{\n    // a `comment` here\n    public int Value { get; set; }\n}\n")]
        [InlineData("# install\nnpm install\n# run `tests`\nnpm test;\nexit 0;\n")]
        public void IgnoresSourceCode(string text) => Assert.False(MarkdownDetector.IsMarkdown(text));

        [Fact]
        public void CodeInsideFencesDoesNotCountAgainstTheDocument()
        {
            string text = "Here is the class:\n\n```csharp\npublic class A\n{\n    int x;\n    int y;\n    int z;\n}\n```\n";
            Assert.True(MarkdownDetector.IsMarkdown(text));
        }

        [Fact]
        public void VeryLongTextIsSkipped()
        {
            Assert.False(MarkdownDetector.IsMarkdown("# a\n\n**b**\n" + new string('x', 200001)));
        }
    }

    public class CopyFilterTests
    {
        private const string Markdown = "# Title\n\nSome **bold** words here.";
        private static readonly string[] Ignored = { IgnoreList.VsCode, IgnoreList.Terminal, "notes" };

        private static CopyInfo Copy(string process = "msedge", string text = Markdown)
        {
            return new CopyInfo { ProcessName = process, Text = text };
        }

        [Fact]
        public void OffersMarkdownFromOtherApps() => Assert.True(new CopyFilter().ShouldOffer(Copy(), Ignored));

        [Fact]
        public void SameTextDoesNotTriggerTwice()
        {
            var filter = new CopyFilter();
            Assert.True(filter.ShouldOffer(Copy(), Ignored));
            Assert.False(filter.ShouldOffer(Copy(), Ignored));
        }

        [Fact]
        public void TextCanTriggerAgainAfterSomethingElse()
        {
            var filter = new CopyFilter();
            Assert.True(filter.ShouldOffer(Copy(), Ignored));
            Assert.True(filter.ShouldOffer(Copy(text: "# Other\n\nMore **bold** words."), Ignored));
            Assert.True(filter.ShouldOffer(Copy(), Ignored));
        }

        [Fact]
        public void PrivateAndOwnCopiesAreSkipped()
        {
            var filter = new CopyFilter();
            Assert.False(filter.ShouldOffer(new CopyInfo { Text = Markdown, IsPrivate = true }, Ignored));
            Assert.False(filter.ShouldOffer(new CopyInfo { Text = Markdown, IsOwnWrite = true }, Ignored));
        }

        [Theory]
        [InlineData("Code")]
        [InlineData("code.exe")]
        [InlineData("Code - Insiders")]
        [InlineData("WindowsTerminal")]
        [InlineData("pwsh")]
        [InlineData("NOTES.EXE")]
        public void IgnoredAppsAreSkipped(string process)
        {
            Assert.False(new CopyFilter().ShouldOffer(Copy(process), Ignored));
        }

        [Fact]
        public void UnknownOwnerIsNotIgnored() => Assert.True(new CopyFilter().ShouldOffer(Copy(process: null), Ignored));

        [Fact]
        public void EmptyIgnoreListIgnoresNothing() => Assert.True(new CopyFilter().ShouldOffer(Copy("Code"), new string[0]));
    }
}

namespace MarkdownHelper.Tests
{
    public class AppPickerTests
    {
        private static RunningApp App(string process, string description = "") => new RunningApp(process, description, null);

        [Fact]
        public void EntriesKeepAFriendlyNameAndMatchByProcess()
        {
            string entry = IgnoreList.Entry("Google Chrome", "chrome");
            Assert.Equal("Google Chrome", IgnoreList.Label(entry));
            Assert.True(IgnoreList.Matches(new[] { entry }, "chrome.exe"));
            Assert.False(IgnoreList.Matches(new[] { entry }, "Google Chrome"));
        }

        [Fact]
        public void PlainEntriesStillWork()
        {
            Assert.Equal("notepad", IgnoreList.Label("notepad"));
            Assert.Equal(IgnoreList.VsCode, IgnoreList.Label(IgnoreList.VsCode));
            Assert.True(IgnoreList.Matches(new[] { "notepad" }, "Notepad"));
        }

        [Fact]
        public void ChoicesUseFriendlyNamesSortedAlphabetically()
        {
            var choices = AppPicker.Choices(new[] { App("winword", "Microsoft Word"), App("chrome", "Google Chrome") }, new string[0], "MarkdownHelper");
            Assert.Equal(new[] { "Google Chrome", "Microsoft Word" }, choices.Select(c => c.Label));
        }

        [Fact]
        public void AppsWithNoDescriptionShowTheirProcessName()
        {
            var choices = AppPicker.Choices(new[] { App("mytool", "  ") }, new string[0], "MarkdownHelper");
            Assert.Equal("mytool", choices.Single().Label);
        }

        [Fact]
        public void AnAppWithManyWindowsAppearsOnce()
        {
            var choices = AppPicker.Choices(new[] { App("chrome", "Chrome"), App("CHROME", "Chrome") }, new string[0], "MarkdownHelper");
            Assert.Single(choices);
        }

        [Fact]
        public void AlreadyIgnoredAppsAreLeftOut()
        {
            string[] ignored = { IgnoreList.VsCode, IgnoreList.Entry("Google Chrome", "chrome") };
            var choices = AppPicker.Choices(new[] { App("Code", "Visual Studio Code"), App("chrome"), App("notepad") }, ignored, "MarkdownHelper");
            Assert.Equal("notepad", choices.Single().ProcessName);
        }

        [Fact]
        public void ThisToolAndWindowsPlumbingAreLeftOut()
        {
            var running = new[] { App("MarkdownHelper"), App("ApplicationFrameHost"), App("TextInputHost"), App("notepad") };
            Assert.Equal("notepad", AppPicker.Choices(running, new string[0], "MarkdownHelper").Single().ProcessName);
        }

        [Fact]
        public void NothingRunningGivesAnEmptyList()
        {
            Assert.Empty(AppPicker.Choices(new RunningApp[0], new string[0], "MarkdownHelper"));
        }
    }
}
