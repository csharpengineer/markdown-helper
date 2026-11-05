using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MarkdownHelper.UI;
using WinForms = System.Windows.Forms;

namespace MarkdownHelper.Windows
{
    /// <summary>The tray icon and its themed menu.</summary>
    internal sealed class TrayIcon : IDisposable
    {
        private const string Tooltip = "Markdown Helper";

        private readonly WinForms.NotifyIcon icon;
        private readonly ContextMenu menu;
        private readonly MenuItem openItem;
        private IntPtr iconHandle;
        private bool marked;

        public TrayIcon()
        {
            icon = new WinForms.NotifyIcon { Text = Tooltip };
            icon.MouseUp += OnMouseUp;

            openItem = new MenuItem { Header = "Open preview" };
            openItem.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
            var settings = new MenuItem { Header = "Settings" };
            settings.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            var quit = new MenuItem { Header = "Quit" };
            quit.Click += (s, e) => QuitRequested?.Invoke(this, EventArgs.Empty);

            menu = new ContextMenu { Style = (Style)Application.Current.FindResource("TrayMenu") };
            menu.Items.Add(openItem);
            menu.Items.Add(settings);
            menu.Items.Add(new Separator());
            menu.Items.Add(quit);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.Opened += OnMenuOpened;

            Refresh();
            icon.Visible = true;
        }

        public event EventHandler OpenRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler QuitRequested;

        /// <summary>Shows or clears the dot that means markdown is waiting.</summary>
        public bool Marked
        {
            get => marked;
            set
            {
                if (marked == value) return;
                marked = value;
                Refresh();
            }
        }

        public void SetHotkeyText(string gesture) => openItem.InputGestureText = gesture;

        public void Refresh()
        {
            int size = WinForms.SystemInformation.SmallIconSize.Width;

            // The tray stays dark or light with the taskbar, so the mark keeps its own colors.
            Color? dot = marked ? (Color?)Color.FromArgb(255, 176, 32) : null;
            using (var bitmap = IconFactory.Draw(size, IconFactory.DefaultBackground, Color.White, dot))
            {
                IntPtr next = bitmap.GetHicon();
                using (var created = Icon.FromHandle(next)) icon.Icon = created;
                if (iconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(iconHandle);
                iconHandle = next;
            }
        }

        private void OnMouseUp(object sender, WinForms.MouseEventArgs e)
        {
            if (e.Button == WinForms.MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
            else if (e.Button == WinForms.MouseButtons.Right) menu.IsOpen = true;
        }

        /// <summary>A tray menu only closes on an outside click if its window is in the foreground.</summary>
        private void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            if (PresentationSource.FromVisual(menu) is HwndSource source) NativeMethods.SetForegroundWindow(source.Handle);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            if (iconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(iconHandle);
        }
    }
}
