using System;
using System.Windows;
using MarkdownHelper.Core;
using MarkdownHelper.UI;
using MarkdownHelper.Windows;

namespace MarkdownHelper
{
    /// <summary>Connects the clipboard, tray, shortcut and windows.</summary>
    internal sealed class AppController : IDisposable
    {
        private readonly SettingsStore store;
        private readonly AppSettings settings;
        private readonly bool manageStartup;
        private readonly MessageWindow messages = new MessageWindow();
        private readonly ThemeWatcher themeWatcher = new ThemeWatcher();
        private readonly TrayIcon tray;

        private PreviewWindow preview;
        private SettingsWindow settingsWindow;
        private PopupWindow popup;
        private string pendingMarkdown;

        public AppController(SettingsStore store, bool manageStartup)
        {
            this.store = store;
            this.manageStartup = manageStartup;
            settings = store.Load();

            Theme.Apply(ThemeWatcher.Current());
            themeWatcher.Changed += (s, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                Theme.Apply(ThemeWatcher.Current());
                tray.Refresh();
            });

            tray = new TrayIcon();
            tray.OpenRequested += (s, e) => OpenPreview(TakeMarkdown());
            tray.SettingsRequested += (s, e) => OpenSettings();
            tray.QuitRequested += (s, e) => Application.Current.Shutdown();

            messages.MarkdownCopied += OnMarkdownCopied;
            messages.HotkeyPressed += (s, e) => OpenPreview(TakeMarkdown());

            ApplySettings();
            if (HotkeyGesture.TryParse(settings.Hotkey, out var gesture)) ApplyHotkey(gesture);
        }

        private void ApplySettings()
        {
            messages.IgnoredApps = settings.IgnoredApps;
            messages.SetWatching(settings.NotifyMode != NotifyMode.ShortcutOnly);
            if (settings.NotifyMode != NotifyMode.TrayOnly) tray.Marked = false;
            if (manageStartup) StartupRegistration.Apply(settings.StartWithWindows);
            tray.SetHotkeyText(settings.Hotkey);
            preview?.SetHotkeyHint(settings.Hotkey);
        }

        private bool ApplyHotkey(HotkeyGesture gesture)
        {
            bool ok = messages.SetHotkey(gesture);
            if (ok) tray.SetHotkeyText(gesture.ToString());
            return ok;
        }

        private void SaveSettings()
        {
            try
            {
                store.Save(settings);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                // Settings still apply for this run if the file cannot be written.
            }
        }

        private void OnMarkdownCopied(object sender, string markdown)
        {
            switch (settings.NotifyMode)
            {
                case NotifyMode.Popup:
                    ShowPopup(markdown);
                    break;
                case NotifyMode.TrayOnly:
                    pendingMarkdown = markdown;
                    tray.Marked = true;
                    break;
                case NotifyMode.OpenPreview:
                    OpenPreview(markdown);
                    break;
            }
        }

        private void ShowPopup(string markdown)
        {
            popup?.Close();
            var next = new PopupWindow(markdown, settings.PopupSeconds);
            next.PreviewRequested += (s, e) => OpenPreview(markdown);
            next.CopyFormatted = () => CopyFormatted(markdown);
            next.Closed += (s, e) => { if (popup == next) popup = null; };
            popup = next;
            next.Show();
        }

        private bool CopyFormatted(string markdown)
        {
            var document = MarkdownParser.Parse(markdown);
            return WriteClipboard(HtmlRenderer.Render(document), DiscordRenderer.Format(document));
        }

        private bool WriteClipboard(string html, string text) => ClipboardAccess.WriteFormatted(messages.Handle, html, text);

        /// <summary>What to show when asked to open: waiting text first, otherwise the clipboard.</summary>
        private string TakeMarkdown()
        {
            string text = pendingMarkdown ?? ReadFreshClipboard();
            pendingMarkdown = null;
            tray.Marked = false;
            return text;
        }

        /// <summary>Clipboard text, unless it is the output of this tool or marked private.</summary>
        private string ReadFreshClipboard()
        {
            if (ClipboardAccess.IsOwnContent || ClipboardAccess.IsMarkedPrivate(messages.Handle)) return null;
            return ClipboardAccess.ReadText(messages.Handle);
        }

        public void OpenPreview(string markdown)
        {
            if (preview == null)
            {
                preview = new PreviewWindow(settings, WriteClipboard);
                preview.SetHotkeyHint(settings.Hotkey);
                preview.SettingsRequested += (s, e) => OpenSettings();
                preview.LayoutPreferenceChanged += (s, e) => SaveSettings();
                preview.Closed += (s, e) => preview = null;
                preview.Show();
            }

            if (!string.IsNullOrWhiteSpace(markdown)) preview.LoadMarkdown(markdown);

            if (preview.WindowState == WindowState.Minimized) preview.WindowState = WindowState.Normal;
            preview.Activate();
        }

        public void OpenPreviewFromClipboard() => OpenPreview(TakeMarkdown());

        /// <summary>Shows the popup as if the text had just been copied.</summary>
        public void DemonstratePopup(string markdown) => ShowPopup(markdown);

        private void OpenSettings()
        {
            if (settingsWindow != null)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new SettingsWindow(settings, ApplyHotkey);
            settingsWindow.Changed += (s, e) => { ApplySettings(); SaveSettings(); };
            settingsWindow.Closed += (s, e) => settingsWindow = null;
            settingsWindow.Show();
        }

        public void Dispose()
        {
            tray.Dispose();
            messages.Dispose();
            themeWatcher.Dispose();
        }
    }
}
