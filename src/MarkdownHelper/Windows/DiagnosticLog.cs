using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MarkdownHelper.Windows
{
    /// <summary>
    /// An opt-in log of what the clipboard watcher sees, for tracking down copies it misses.
    /// It records which app copied, which formats were offered and why a copy was skipped,
    /// but never the copied text itself.
    /// </summary>
    internal static class DiagnosticLog
    {
        public const string Argument = "--diagnose";

        private static readonly object Gate = new object();

        public static string FilePath { get; private set; }

        public static bool Enabled => FilePath != null;

        public static void Start()
        {
            FilePath = Path.Combine(Path.GetTempPath(), "MarkdownHelper-diagnostics.log");
            Write("Started. Windows " + Environment.OSVersion.Version + ", session " + (System.Windows.Forms.SystemInformation.TerminalServerSession ? "remote" : "local"));
        }

        public static void Write(string line)
        {
            if (!Enabled) return;
            lock (Gate)
            {
                try
                {
                    File.AppendAllText(FilePath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException)
                {
                    // A missed log line must never stop the tool.
                }
            }
        }

        /// <summary>Lists the formats on the clipboard by name. Reading names never renders any data.</summary>
        public static string FormatNames(IntPtr owner)
        {
            if (!NativeMethods.OpenClipboard(owner)) return "(clipboard locked)";
            try
            {
                var names = new List<string>();
                uint format = 0;
                while ((format = NativeMethods.EnumClipboardFormats(format)) != 0) names.Add(FormatName(format));
                return string.Join(", ", names);
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        private static string FormatName(uint format)
        {
            switch (format)
            {
                case 1: return "CF_TEXT";
                case 2: return "CF_BITMAP";
                case 7: return "CF_OEMTEXT";
                case 8: return "CF_DIB";
                case 13: return "CF_UNICODETEXT";
                case 15: return "CF_HDROP";
                case 16: return "CF_LOCALE";
                case 17: return "CF_DIBV5";
            }

            var name = new StringBuilder(256);
            return NativeMethods.GetClipboardFormatName(format, name, name.Capacity) > 0 ? name.ToString() : "#" + format;
        }
    }
}
