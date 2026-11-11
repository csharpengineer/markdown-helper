using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MarkdownHelper.Core;

namespace MarkdownHelper.Windows
{
    /// <summary>
    /// Direct Win32 clipboard access. The bytes of "HTML Format" must be exact, and other
    /// apps often hold the clipboard open for a moment, so every open is retried.
    /// </summary>
    internal static class ClipboardAccess
    {
        private const int OpenAttempts = 10;
        private const int OpenRetryMilliseconds = 25;

        private static readonly uint HtmlFormat = NativeMethods.RegisterClipboardFormat("HTML Format");
        private static readonly uint ExcludeFormat = NativeMethods.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        private static readonly uint ViewerIgnoreFormat = NativeMethods.RegisterClipboardFormat("Clipboard Viewer Ignore");

        /// <summary>The sequence number of this tool's last write, so the watcher can skip it.</summary>
        public static uint LastWriteSequence { get; private set; }

        /// <summary>True while the clipboard still holds what this tool last wrote.</summary>
        public static bool IsOwnContent => LastWriteSequence != 0 && NativeMethods.GetClipboardSequenceNumber() == LastWriteSequence;

        private static bool Open(IntPtr owner)
        {
            for (int attempt = 0; attempt < OpenAttempts; attempt++)
            {
                if (NativeMethods.OpenClipboard(owner)) return true;
                Thread.Sleep(OpenRetryMilliseconds);
            }
            return false;
        }

        /// <summary>Reads clipboard text, or null when there is none or it stays locked.</summary>
        public static string ReadText(IntPtr owner)
        {
            if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT)) return null;
            if (!Open(owner)) return null;
            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
                if (handle == IntPtr.Zero) return null;

                IntPtr pointer = NativeMethods.GlobalLock(handle);
                if (pointer == IntPtr.Zero) return null;
                try
                {
                    return Marshal.PtrToStringUni(pointer);
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>True when the copying app asked monitors like this one to look away.</summary>
        public static bool IsMarkedPrivate(IntPtr owner) => PrivacyMarker(owner) != null;

        /// <summary>
        /// Which privacy marker the clipboard carries, or null when there is none. Only markers
        /// meant for clipboard monitors count. "CanIncludeInClipboardHistory" only keeps a copy
        /// out of Windows clipboard history, and Remote Desktop sets it on every remote copy.
        /// </summary>
        public static string PrivacyMarker(IntPtr owner)
        {
            if (NativeMethods.IsClipboardFormatAvailable(ExcludeFormat)) return "ExcludeClipboardContentFromMonitorProcessing";
            if (NativeMethods.IsClipboardFormatAvailable(ViewerIgnoreFormat)) return "Clipboard Viewer Ignore";
            return null;
        }

        /// <summary>The name of the process that made the current copy, or null if unknown.</summary>
        public static string OwnerProcessName(out bool isOwnProcess)
        {
            isOwnProcess = false;
            IntPtr window = NativeMethods.GetClipboardOwner();
            if (window == IntPtr.Zero) return null;

            NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0) return null;
            isOwnProcess = processId == (uint)Process.GetCurrentProcess().Id;

            try
            {
                using (var process = Process.GetProcessById((int)processId)) return process.ProcessName;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Puts both versions on the clipboard in one step. Returns false if it stayed locked.</summary>
        public static bool WriteFormatted(IntPtr owner, string htmlFragment, string plainText)
        {
            byte[] html = ClipboardHtml.Encode(htmlFragment);
            byte[] text = Encoding.Unicode.GetBytes(plainText);

            if (!Open(owner)) return false;
            try
            {
                if (!NativeMethods.EmptyClipboard()) return false;
                bool wroteHtml = SetBytes(HtmlFormat, html, terminatorBytes: 1);
                bool wroteText = SetBytes(NativeMethods.CF_UNICODETEXT, text, terminatorBytes: 2);
                return wroteHtml && wroteText;
            }
            finally
            {
                NativeMethods.CloseClipboard();
                LastWriteSequence = NativeMethods.GetClipboardSequenceNumber();
            }
        }

        private static bool SetBytes(uint format, byte[] data, int terminatorBytes)
        {
            int size = data.Length + terminatorBytes;
            IntPtr handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)size);
            if (handle == IntPtr.Zero) return false;

            IntPtr pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                return false;
            }

            Marshal.Copy(data, 0, pointer, data.Length);
            for (int i = 0; i < terminatorBytes; i++) Marshal.WriteByte(pointer, data.Length + i, 0);
            NativeMethods.GlobalUnlock(handle);

            if (NativeMethods.SetClipboardData(format, handle) != IntPtr.Zero) return true;

            // The clipboard only takes ownership of the memory on success.
            NativeMethods.GlobalFree(handle);
            return false;
        }

        /// <summary>Reads the raw "HTML Format" bytes. Used to check what was written.</summary>
        public static byte[] ReadHtmlBytes(IntPtr owner)
        {
            if (!Open(owner)) return null;
            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(HtmlFormat);
                if (handle == IntPtr.Zero) return null;
                int size = (int)(uint)NativeMethods.GlobalSize(handle);
                IntPtr pointer = NativeMethods.GlobalLock(handle);
                if (pointer == IntPtr.Zero) return null;
                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(pointer, bytes, 0, size);
                    return bytes;
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }
    }
}
