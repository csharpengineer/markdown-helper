using System;
using System.IO;
using System.Linq;
using MarkdownHelper.Core;
using Xunit;

namespace MarkdownHelper.Tests
{
    public class EditSessionTests
    {
        private static EditSession Session(string text)
        {
            var session = new EditSession();
            session.Commit(text);
            return session;
        }

        [Fact]
        public void UndoRestoresEarlierText()
        {
            var session = Session("one");
            session.Commit("two");
            session.Undo();
            Assert.Equal("one", session.Markdown);
            session.Undo();
            Assert.Equal("", session.Markdown);
            Assert.False(session.CanUndo);
        }

        [Fact]
        public void LoadingIntoAnEmptyWindowHasNothingToUndo()
        {
            var session = new EditSession();
            session.Load("first");
            Assert.Equal("first", session.Markdown);
            Assert.False(session.CanUndo);
        }

        [Fact]
        public void LoadingOverExistingTextCanBeUndone()
        {
            var session = new EditSession();
            session.Load("first");
            session.Load("second");
            session.Undo();
            Assert.Equal("first", session.Markdown);
        }

        [Fact]
        public void UnchangedTextAddsNoHistory()
        {
            var session = Session("one");
            session.Commit("one");
            session.Undo();
            Assert.Equal("", session.Markdown);
        }

        [Fact]
        public void HistoryIsCapped()
        {
            var session = new EditSession();
            for (int i = 0; i < 1000; i++) session.Commit(i.ToString());
            int undos = 0;
            while (session.CanUndo) { session.Undo(); undos++; }
            Assert.Equal(200, undos);
        }

        [Fact]
        public void PiecesSplitListsIntoItems()
        {
            var pieces = Session("# Head\n\ntext\n\n- a\n- b\n\n| A |\n| --- |\n| 1 |").Pieces();
            Assert.Equal(5, pieces.Count);
            Assert.IsType<ListItem>(pieces[2].Block);
            Assert.NotNull(pieces[3].OwnerList);
            Assert.IsType<Table>(pieces[4].Block);
        }

        [Fact]
        public void RemovingAParagraphLeavesTheRestExactly()
        {
            var session = Session("# Head\n\nfirst\n\n*odd*   spacing_kept\n\nlast");
            session.Remove(session.Pieces()[1]);
            Assert.Equal("# Head\n\n*odd*   spacing_kept\n\nlast", session.Markdown);
        }

        [Fact]
        public void RemovingTheFirstAndLastPieceTrimsBlankLines()
        {
            var session = Session("first\n\nmiddle\n\nlast");
            session.Remove(session.Pieces()[0]);
            Assert.Equal("middle\n\nlast", session.Markdown);
            session.Remove(session.Pieces()[1]);
            Assert.Equal("middle", session.Markdown);
        }

        [Fact]
        public void RemovingOneListItemKeepsTheOthers()
        {
            var session = Session("- a\n- b\n- c");
            session.Remove(session.Pieces()[1]);
            Assert.Equal("- a\n- c", session.Markdown);
        }

        [Fact]
        public void RemoveCanBeUndone()
        {
            const string text = "one\n\ntwo";
            var session = Session(text);
            session.Remove(session.Pieces()[0]);
            session.Undo();
            Assert.Equal(text, session.Markdown);
        }

        [Fact]
        public void RemovingEverythingLeavesEmptyText()
        {
            var session = Session("only");
            session.Remove(session.Pieces()[0]);
            Assert.Equal("", session.Markdown);
            Assert.Empty(session.Pieces());
        }

        [Fact]
        public void ChangedFiresForCommitAndUndo()
        {
            var session = new EditSession();
            int count = 0;
            session.Changed += (s, e) => count++;
            session.Commit("a");
            session.Undo();
            Assert.Equal(2, count);
        }
    }

    public class SettingsTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "mdh-tests-" + Guid.NewGuid().ToString("N"));
        private string SettingsFile => Path.Combine(folder, "nested", "settings.json");

        public void Dispose()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Fact]
        public void DefaultsMatchTheDesign()
        {
            var settings = new AppSettings();
            Assert.Equal(NotifyMode.Popup, settings.NotifyMode);
            Assert.Equal(6, settings.PopupSeconds);
            Assert.Equal("Ctrl+Alt+M", settings.Hotkey);
            Assert.Equal(new[] { "VS Code", "Terminal" }, settings.IgnoredApps);
            Assert.True(settings.StartWithWindows);
        }

        [Fact]
        public void MissingFileGivesDefaults()
        {
            var settings = new SettingsStore(SettingsFile).Load();
            Assert.Equal(6, settings.PopupSeconds);
        }

        [Fact]
        public void SavedSettingsRoundTrip()
        {
            var store = new SettingsStore(SettingsFile);
            store.Save(new AppSettings
            {
                NotifyMode = NotifyMode.TrayOnly,
                PopupSeconds = 9,
                Hotkey = "Ctrl+Shift+K",
                IgnoredApps = { "notes" },
                StartWithWindows = false,
                ShowMarkdown = true,
                LastMode = EditorMode.RemovePieces
            });

            var loaded = store.Load();
            Assert.Equal(NotifyMode.TrayOnly, loaded.NotifyMode);
            Assert.Equal(9, loaded.PopupSeconds);
            Assert.Equal("Ctrl+Shift+K", loaded.Hotkey);
            Assert.Contains("notes", loaded.IgnoredApps);
            Assert.False(loaded.StartWithWindows);
            Assert.True(loaded.ShowMarkdown);
            Assert.Equal(EditorMode.RemovePieces, loaded.LastMode);
        }

        [Fact]
        public void SavingTwiceReplacesTheFile()
        {
            var store = new SettingsStore(SettingsFile);
            store.Save(new AppSettings { PopupSeconds = 8 });
            store.Save(new AppSettings { PopupSeconds = 9 });
            Assert.Equal(9, store.Load().PopupSeconds);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsFile)));
        }

        [Theory]
        [InlineData("not json at all {")]
        [InlineData("")]
        [InlineData("[1,2,3]")]
        public void DamagedFileGivesDefaults(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, content);
            Assert.Equal(6, new SettingsStore(SettingsFile).Load().PopupSeconds);
        }

        [Fact]
        public void PartialFileKeepsDefaultsForTheRest()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, "{\"PopupSeconds\":10}");
            var settings = new SettingsStore(SettingsFile).Load();
            Assert.Equal(10, settings.PopupSeconds);
            Assert.Equal("Ctrl+Alt+M", settings.Hotkey);
            Assert.True(settings.StartWithWindows);
            Assert.Equal(2, settings.IgnoredApps.Count);
        }

        [Fact]
        public void OutOfRangeValuesAreRepaired()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, "{\"PopupSeconds\":9999,\"Hotkey\":\"nonsense\",\"NotifyMode\":77}");
            var settings = new SettingsStore(SettingsFile).Load();
            Assert.Equal(AppSettings.MaxPopupSeconds, settings.PopupSeconds);
            Assert.Equal("Ctrl+Alt+M", settings.Hotkey);
            Assert.Equal(NotifyMode.Popup, settings.NotifyMode);
        }

        [Theory]
        [InlineData("Ctrl+Alt+M", "Ctrl+Alt+M")]
        [InlineData("alt + ctrl + m", "Ctrl+Alt+M")]
        [InlineData("Shift+Win+F5", "Shift+Win+F5")]
        public void HotkeysParse(string text, string expected)
        {
            Assert.True(HotkeyGesture.TryParse(text, out var gesture));
            Assert.Equal(expected, gesture.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("M")]
        [InlineData("Ctrl+Alt")]
        [InlineData("Banana+M")]
        public void BadHotkeysAreRejected(string text) => Assert.False(HotkeyGesture.TryParse(text, out _));
    }
}
