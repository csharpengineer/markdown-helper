using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using MarkdownHelper.Core;
using MarkdownHelper.UI;
using Xunit;

namespace MarkdownHelper.Tests
{
    public class CopyFilterTests
    {
        private const string Markdown = "# Title\n\nSome **bold** words here.";
        private static readonly IgnoredApp[] Ignored = { new IgnoredApp("Notes App", "notes"), new IgnoredApp("Visual Studio Code", "Code") };
        private static readonly IgnoredApp[] Nothing = new IgnoredApp[0];

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
        [InlineData("notes")]
        [InlineData("NOTES.EXE")]
        public void IgnoredAppsAreSkipped(string process)
        {
            Assert.False(new CopyFilter().ShouldOffer(Copy(process), Ignored));
        }

        [Fact]
        public void TheFriendlyNameIsNotWhatMatches()
        {
            Assert.True(new CopyFilter().ShouldOffer(Copy("Notes App"), Ignored));
        }

        [Fact]
        public void UnknownOwnerIsNotIgnored() => Assert.True(new CopyFilter().ShouldOffer(Copy(process: null), Ignored));

        [Theory]
        [InlineData("Code")]
        [InlineData("WindowsTerminal")]
        [InlineData("pwsh")]
        public void NothingIsIgnoredWhenTheListIsEmpty(string process)
        {
            Assert.True(new CopyFilter().ShouldOffer(Copy(process), Nothing));
        }
    }

    public class AppPickerTests
    {
        private const string Self = "MarkdownHelper";
        private static readonly IgnoredApp[] Nothing = new IgnoredApp[0];

        private static RunningApp App(string process, string description = "") => new RunningApp(process, description, null);

        [Fact]
        public void ChoicesUseFriendlyNamesSortedAlphabetically()
        {
            var choices = AppPicker.Choices(new[] { App("winword", "Microsoft Word"), App("chrome", "Google Chrome") }, Nothing, Self);
            Assert.Equal(new[] { "Google Chrome", "Microsoft Word" }, choices.Select(c => c.Label));
        }

        [Fact]
        public void AppsWithNoDescriptionShowTheirProcessName()
        {
            Assert.Equal("mytool", AppPicker.Choices(new[] { App("mytool", "  ") }, Nothing, Self).Single().Label);
        }

        [Fact]
        public void AnAppWithManyWindowsAppearsOnce()
        {
            Assert.Single(AppPicker.Choices(new[] { App("chrome", "Chrome"), App("CHROME", "Chrome") }, Nothing, Self));
        }

        [Fact]
        public void AlreadyIgnoredAppsAreLeftOut()
        {
            var ignored = new[] { new IgnoredApp("Google Chrome", "chrome") };
            var choices = AppPicker.Choices(new[] { App("chrome"), App("notepad") }, ignored, Self);
            Assert.Equal("notepad", choices.Single().ProcessName);
        }

        [Fact]
        public void ThisToolAndWindowsPlumbingAreLeftOut()
        {
            var running = new[] { App(Self), App("ApplicationFrameHost"), App("TextInputHost"), App("notepad") };
            Assert.Equal("notepad", AppPicker.Choices(running, Nothing, Self).Single().ProcessName);
        }

        [Fact]
        public void NothingRunningGivesAnEmptyList()
        {
            Assert.Empty(AppPicker.Choices(new RunningApp[0], Nothing, Self));
        }
    }

    public class AppIconTests
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

        [Fact]
        public void AnIconSurvivesBeingSavedAndLoaded()
        {
            OnSta(() =>
            {
                var icon = AppIcon.FromFile(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
                Assert.NotNull(icon);

                var loaded = AppIcon.FromBase64(AppIcon.ToBase64(icon));
                Assert.NotNull(loaded);
                Assert.Equal(icon.PixelWidth, ((System.Windows.Media.Imaging.BitmapSource)loaded).PixelWidth);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not base64 at all!")]
        [InlineData("aGVsbG8=")]
        public void DamagedIconsShowAsNoIcon(string saved)
        {
            OnSta(() => Assert.Null(AppIcon.FromBase64(saved)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"C:\does\not\exist.exe")]
        public void MissingFilesGiveNoIcon(string path)
        {
            OnSta(() =>
            {
                Assert.Null(AppIcon.FromFile(path));
                Assert.Null(AppIcon.ToBase64(null));
            });
        }
    }
}
