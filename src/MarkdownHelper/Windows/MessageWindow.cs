using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MarkdownHelper.Core;

namespace MarkdownHelper.Windows
{
    /// <summary>
    /// A hidden window that receives clipboard and shortcut notifications. It is the one
    /// place that talks to the watcher APIs, and it raises plain events for the app.
    /// </summary>
    internal sealed class MessageWindow : IDisposable
    {
        private const int HotkeyId = 1;
        private const int SettleMilliseconds = 120;
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);

        private readonly HwndSource source;
        private readonly DispatcherTimer settle;
        private readonly CopyFilter filter = new CopyFilter();
        private bool hotkeyRegistered;
        private bool watching;

        public MessageWindow()
        {
            var parameters = new HwndSourceParameters("MarkdownHelperMessages")
            {
                ParentWindow = MessageOnlyParent,
                WindowStyle = 0,
                Width = 0,
                Height = 0
            };
            source = new HwndSource(parameters);
            source.AddHook(WindowProc);

            // Apps often update the clipboard several times in a row, so wait for it to settle.
            settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettleMilliseconds) };
            settle.Tick += (s, e) => { settle.Stop(); InspectClipboard(); };
        }

        public IntPtr Handle => source.Handle;

        public IEnumerable<IgnoredApp> IgnoredApps { get; set; } = new IgnoredApp[0];

        public event EventHandler<string> MarkdownCopied;
        public event EventHandler HotkeyPressed;

        public void SetWatching(bool enable)
        {
            if (enable == watching) return;
            if (enable) NativeMethods.AddClipboardFormatListener(Handle);
            else NativeMethods.RemoveClipboardFormatListener(Handle);
            watching = enable;
        }

        /// <summary>Registers the shortcut. Returns false if another app already holds it.</summary>
        public bool SetHotkey(HotkeyGesture gesture)
        {
            if (hotkeyRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
                hotkeyRegistered = false;
            }

            uint virtualKey = VirtualKey(gesture.Key);
            if (virtualKey == 0) return false;

            uint modifiers = NativeMethods.MOD_NOREPEAT;
            if (gesture.Ctrl) modifiers |= NativeMethods.MOD_CONTROL;
            if (gesture.Alt) modifiers |= NativeMethods.MOD_ALT;
            if (gesture.Shift) modifiers |= NativeMethods.MOD_SHIFT;
            if (gesture.Win) modifiers |= NativeMethods.MOD_WIN;

            hotkeyRegistered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers, virtualKey);
            return hotkeyRegistered;
        }

        internal static uint VirtualKey(string name)
        {
            return new KeyConverter().ConvertFromInvariantString(name) is Key key ? (uint)KeyInterop.VirtualKeyFromKey(key) : 0;
        }

        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (message)
            {
                case NativeMethods.WM_CLIPBOARDUPDATE:
                    DiagnosticLog.Write("Clipboard changed (#" + NativeMethods.GetClipboardSequenceNumber() + ")");
                    settle.Stop();
                    settle.Start();
                    handled = true;
                    break;

                case NativeMethods.WM_HOTKEY:
                    if (wParam.ToInt32() == HotkeyId) HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private void InspectClipboard()
        {
            bool ownWrite = ClipboardAccess.IsOwnContent;
            string process = ClipboardAccess.OwnerProcessName(out bool ownProcess);
            string privacy = ClipboardAccess.PrivacyMarker(Handle);
            uint sequence = NativeMethods.GetClipboardSequenceNumber();
            if (DiagnosticLog.Enabled)
            {
                DiagnosticLog.Write("Copy #" + sequence + " from " + (process ?? "unknown app") + (ownProcess || ownWrite ? " (this tool)" : "") +
                    ". Formats: " + DiagnosticLog.FormatNames(Handle) + ". Privacy marker: " + (privacy ?? "none"));
            }
            var copy = new CopyInfo
            {
                IsOwnWrite = ownWrite || ownProcess,
                IsPrivate = privacy != null,
                ProcessName = process
            };

            // Text is only read once the copy has passed every check that does not need it.
            if (copy.IsOwnWrite || copy.IsPrivate)
            {
                DiagnosticLog.Write("  Skipped: " + (copy.IsOwnWrite ? CopyDecision.OwnWrite : CopyDecision.Private));
                return;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            copy.Text = ClipboardAccess.ReadText(Handle);

            var decision = filter.Decide(copy, IgnoredApps);
            DiagnosticLog.Write("  Text: " + (copy.Text == null ? "none" : copy.Text.Length + " characters, " + MarkdownDetector.Explain(copy.Text)) + ", read in " + timer.ElapsedMilliseconds + " ms. Result: " + decision);
            if (decision == CopyDecision.Offer) MarkdownCopied?.Invoke(this, copy.Text);
        }

        public void Dispose()
        {
            settle.Stop();
            SetWatching(false);
            if (hotkeyRegistered) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            source.Dispose();
        }
    }
}
