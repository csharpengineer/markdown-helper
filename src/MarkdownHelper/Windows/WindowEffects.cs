using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MarkdownHelper.UI;

namespace MarkdownHelper.Windows
{
    /// <summary>Applies the Windows 11 title bar, corners and Mica. Older versions ignore what they lack.</summary>
    internal static class WindowEffects
    {
        private const int MicaBuild = 22621;

        public static bool SupportsMica => Environment.OSVersion.Version.Build >= MicaBuild;

        public static void Apply(Window window, ThemePalette palette)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            int dark = palette.IsDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            int corners = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));

            if (!SupportsMica || palette.IsHighContrast)
            {
                window.Background = new SolidColorBrush(palette.Shell);
                return;
            }

            var source = HwndSource.FromHwnd(handle);
            if (source?.CompositionTarget != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;

            var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);

            int backdrop = NativeMethods.DWMSBT_MAINWINDOW;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

            // With the frame extended, Windows would draw its own buttons behind this app's.
            long style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_STYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_STYLE, new IntPtr(style & ~NativeMethods.WS_SYSMENU));
        }

        /// <summary>Stops a window from ever taking focus, and keeps it out of Alt+Tab.</summary>
        public static void MakeNonActivating(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            long style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
        }
    }
}
