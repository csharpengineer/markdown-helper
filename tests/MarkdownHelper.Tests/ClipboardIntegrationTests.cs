using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using MarkdownHelper.Core;
using MarkdownHelper.UI;
using MarkdownHelper.Windows;
using Xunit;

namespace MarkdownHelper.Tests
{
    /// <summary>Runs only when asked to, so a developer's real clipboard is left alone. CI turns it on.</summary>
    public sealed class ClipboardFactAttribute : FactAttribute
    {
        public ClipboardFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MDH_CLIPBOARD_TESTS") != "1")
            {
                Skip = "Uses the real clipboard. Set MDH_CLIPBOARD_TESTS=1 to run.";
            }
        }
    }

    public class ClipboardIntegrationTests
    {
        private static void OnDispatcher(Action<Dispatcher> body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { body(Dispatcher.CurrentDispatcher); }
                catch (Exception e) { failure = e; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        private static void Pump(TimeSpan time)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = time };
            timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        [ClipboardFact]
        public void WrittenFormatsReadBackExactly()
        {
            OnDispatcher(_ =>
            {
                using (var window = new MessageWindow())
                {
                    const string html = "<p><strong>$5 to $0</strong> § × 🎉</p>";
                    const string text = "**$5 to $0** § × 🎉";
                    Assert.True(ClipboardAccess.WriteFormatted(window.Handle, html, text));

                    Assert.Equal(text, ClipboardAccess.ReadText(window.Handle));

                    string raw = Encoding.UTF8.GetString(ClipboardAccess.ReadHtmlBytes(window.Handle)).TrimEnd('\0');
                    Assert.Contains(html, raw);
                    Assert.StartsWith("Version:0.9", raw);
                }
            });
        }


        private static void CopyFromAnotherApp(string text, params string[] markers)
        {
            var thread = new Thread(() =>
            {
                var data = new System.Windows.Forms.DataObject();
                data.SetData(System.Windows.Forms.DataFormats.UnicodeText, text);
                foreach (string marker in markers) data.SetData(marker, new System.IO.MemoryStream(BitConverter.GetBytes(0)));
                System.Windows.Forms.Clipboard.SetDataObject(data, true);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [ClipboardFact]
        public void RemoteDesktopCopiesAreOffered()
        {
            OnDispatcher(_ =>
            {
                using (var watcher = new MessageWindow())
                {
                    string seen = null;
                    watcher.MarkdownCopied += (s, text) => seen = text;
                    watcher.SetWatching(true);

                    // These are the markers the Remote Desktop client puts on every remote copy.
                    string copied = "# Remote " + Guid.NewGuid().ToString("N") + "\n\nSome **bold** words.";
                    CopyFromAnotherApp(copied, "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard");

                    Pump(TimeSpan.FromMilliseconds(800));
                    Assert.Equal(copied, seen);
                }
            });
        }

        [ClipboardFact]
        public void CopiesMarkedForMonitorsToIgnoreAreNotRead()
        {
            OnDispatcher(_ =>
            {
                using (var watcher = new MessageWindow())
                {
                    int count = 0;
                    watcher.MarkdownCopied += (s, text) => count++;
                    watcher.SetWatching(true);

                    CopyFromAnotherApp("# Secret " + Guid.NewGuid().ToString("N") + "\n\nSome **bold** words.", "ExcludeClipboardContentFromMonitorProcessing");

                    Pump(TimeSpan.FromMilliseconds(800));
                    Assert.Equal(0, count);
                }
            });
        }
        [ClipboardFact]
        public void CopiedMarkdownIsNoticedAndOwnWritesAreNot()
        {
            OnDispatcher(_ =>
            {
                using (var watcher = new MessageWindow())
                using (var other = new MessageWindow())
                {
                    string seen = null;
                    int count = 0;
                    watcher.MarkdownCopied += (s, text) => { seen = text; count++; };
                    watcher.SetWatching(true);

                    // This tool's own write must not fire.
                    ClipboardAccess.WriteFormatted(other.Handle, "<h1>Title</h1>", "# Title\n\nSome **bold** words.");
                    Pump(TimeSpan.FromMilliseconds(500));
                    Assert.Equal(0, count);

                    // A copy from another app must fire, once.
                    string copied = "# Notes " + Guid.NewGuid().ToString("N") + "\n\nSome **bold** words.";
                    var thread = new Thread(() => System.Windows.Forms.Clipboard.SetText(copied));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();

                    Pump(TimeSpan.FromMilliseconds(800));
                    Assert.Equal(copied, seen);
                    Assert.Equal(1, count);
                }
            });
        }
    }

    public class WindowsIntegrationTests
    {
        [Theory]
        [InlineData("M", 0x4D)]
        [InlineData("F5", 0x74)]
        [InlineData("D1", 0x31)]
        [InlineData("Space", 0x20)]
        public void HotkeyNamesMapToVirtualKeys(string name, uint expected)
        {
            Assert.Equal(expected, MessageWindow.VirtualKey(name));
        }

        [Fact]
        public void ThemesDefineEveryColor()
        {
            foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
            {
                var colors = typeof(ThemePalette).GetProperties()
                    .Where(p => p.PropertyType == typeof(System.Windows.Media.Color))
                    .Select(p => (System.Windows.Media.Color)p.GetValue(palette));
                Assert.All(colors, c => Assert.NotEqual(0, c.A));
            }
            Assert.True(ThemePalette.Dark.IsDark);
            Assert.False(ThemePalette.Light.IsDark);
            Assert.True(ThemePalette.HighContrast().IsHighContrast);
        }
    }
}
