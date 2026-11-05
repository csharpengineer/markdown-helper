using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MarkdownHelper.Core;
using MarkdownHelper.Windows;

namespace MarkdownHelper.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings settings;
        private readonly Func<HotkeyGesture, bool> applyHotkey;
        private bool recording;
        private bool ready;

        public SettingsWindow(AppSettings settings, Func<HotkeyGesture, bool> applyHotkey)
        {
            this.settings = settings;
            this.applyHotkey = applyHotkey;
            InitializeComponent();

            ModePopup.IsChecked = settings.NotifyMode == NotifyMode.Popup;
            ModeTray.IsChecked = settings.NotifyMode == NotifyMode.TrayOnly;
            ModeOpen.IsChecked = settings.NotifyMode == NotifyMode.OpenPreview;
            ModeShortcut.IsChecked = settings.NotifyMode == NotifyMode.ShortcutOnly;
            StartupSwitch.IsChecked = settings.StartWithWindows;

            ShowHotkey();
            ShowSeconds();
            ShowChips();
            ready = true;

            SourceInitialized += (s, e) => WindowEffects.Apply(this, Theme.Current);
            Theme.Changed += OnThemeChanged;
            Closed += (s, e) => Theme.Changed -= OnThemeChanged;
        }

        public event EventHandler Changed;

        private void OnThemeChanged(object sender, EventArgs e) => WindowEffects.Apply(this, Theme.Current);

        private void Save() => Changed?.Invoke(this, EventArgs.Empty);

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnNotifyChanged(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            if (ModePopup.IsChecked == true) settings.NotifyMode = NotifyMode.Popup;
            else if (ModeTray.IsChecked == true) settings.NotifyMode = NotifyMode.TrayOnly;
            else if (ModeOpen.IsChecked == true) settings.NotifyMode = NotifyMode.OpenPreview;
            else settings.NotifyMode = NotifyMode.ShortcutOnly;
            Save();
        }

        private void ShowHotkey()
        {
            HotkeyGesture.TryParse(settings.Hotkey, out var gesture);
            HotkeyKeys.ItemsSource = gesture?.Parts().ToList();
        }

        private void ShowMessage(string text)
        {
            HotkeyMessage.Text = text;
            HotkeyMessage.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnRecordHotkey(object sender, RoutedEventArgs e)
        {
            recording = true;
            HotkeyKeys.ItemsSource = new[] { "Press keys" };
            ShowMessage("Press the new shortcut, or Esc to cancel");
            HotkeyButton.Focus();
        }

        private void OnHotkeyKey(object sender, KeyEventArgs e)
        {
            if (!recording) return;
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { StopRecording(""); return; }
            if (IsModifier(key)) return;

            var mods = Keyboard.Modifiers;
            bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
            if (mods == ModifierKeys.None && !win)
            {
                ShowMessage("Include Ctrl, Alt, Shift or Win");
                return;
            }

            string name = new KeyConverter().ConvertToInvariantString(key);
            var gesture = new HotkeyGesture(
                mods.HasFlag(ModifierKeys.Control), mods.HasFlag(ModifierKeys.Alt), mods.HasFlag(ModifierKeys.Shift), win, name);

            if (applyHotkey(gesture))
            {
                settings.Hotkey = gesture.ToString();
                Save();
                StopRecording("");
            }
            else
            {
                StopRecording(gesture + " is already used by another app");
            }
        }

        private static bool IsModifier(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin;
        }

        private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (recording) StopRecording("");
        }

        private void StopRecording(string message)
        {
            recording = false;
            ShowHotkey();
            ShowMessage(message);
        }

        private void ShowSeconds() => SecondsText.Text = settings.PopupSeconds + " seconds";

        private void ChangeSeconds(int delta)
        {
            settings.PopupSeconds = Math.Max(AppSettings.MinPopupSeconds, Math.Min(AppSettings.MaxPopupSeconds, settings.PopupSeconds + delta));
            ShowSeconds();
            Save();
        }

        private void OnSecondsDown(object sender, RoutedEventArgs e) => ChangeSeconds(-1);

        private void OnSecondsUp(object sender, RoutedEventArgs e) => ChangeSeconds(1);

        private void ShowChips()
        {
            IgnoreChips.Children.Clear();
            foreach (var app in settings.IgnoredApps) IgnoreChips.Children.Add(MakeChip(app));

            var add = new Button { Content = "+ Add", Style = (Style)FindResource("Btn"), Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 5, 5) };
            add.SetResourceReference(ForegroundProperty, "AccentBrush");
            add.Click += (s, e) => ShowAppList();
            IgnoreChips.Children.Add(add);
        }

        private UIElement MakeChip(IgnoredApp app)
        {
            var remove = new Button
            {
                Style = (Style)FindResource("IconBtn"),
                Width = 18,
                Height = 18,
                Margin = new Thickness(4, 0, -4, 0),
                Content = new TextBlock { Text = "✕", FontSize = 9 }
            };
            System.Windows.Automation.AutomationProperties.SetName(remove, "Stop ignoring " + app.Label);
            remove.Click += (s, e) =>
            {
                settings.IgnoredApps.Remove(app);
                ShowChips();
                Save();
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var chipIcon = AppIcon.FromBase64(app.IconPng);
            if (chipIcon != null) panel.Children.Add(new System.Windows.Controls.Image { Source = chipIcon, Width = 14, Height = 14, Margin = new Thickness(-3, 0, 6, 0) });
            panel.Children.Add(new TextBlock { Text = app.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(remove);

            var chip = new Border
            {
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 8, 3),
                Margin = new Thickness(0, 0, 5, 5),
                Child = panel
            };
            chip.SetResourceReference(Border.BackgroundProperty, "HoverBrush");
            chip.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
            return chip;
        }

        /// <summary>Lists what is open now, so nobody has to know an app's process name.</summary>
        private void ShowAppList()
        {
            string self = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var choices = AppPicker.Choices(RunningApps.Read(), settings.IgnoredApps, self);

            AppList.Children.Clear();
            foreach (var app in choices) AppList.Children.Add(MakeAppRow(app));
            if (choices.Count == 0)
            {
                AppList.Children.Add(new TextBlock { Text = "No other apps are open", Style = (Style)FindResource("Sub"), Margin = new Thickness(8, 5, 8, 5) });
            }

            AppPanel.Visibility = Visibility.Visible;
        }

        private UIElement MakeAppRow(RunningApp app)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = AppIcon.FromFile(app.Path);
            panel.Children.Add(new System.Windows.Controls.Image { Source = icon, Width = 18, Height = 18, Margin = new Thickness(0, 0, 9, 0) });
            panel.Children.Add(new TextBlock { Text = app.Label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });

            var row = new Button { Style = (Style)FindResource("AppRow"), Content = panel };
            System.Windows.Automation.AutomationProperties.SetName(row, "Ignore copies from " + app.Label);
            row.Click += (s, e) =>
            {
                settings.IgnoredApps.Add(new IgnoredApp(app.Label, app.ProcessName, AppIcon.ToBase64(icon)));
                AppPanel.Visibility = Visibility.Collapsed;
                ShowChips();
                Save();
            };
            return row;
        }


        private void OnCancelPick(object sender, RoutedEventArgs e) => AppPanel.Visibility = Visibility.Collapsed;

        private void OnStartupChanged(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            settings.StartWithWindows = StartupSwitch.IsChecked == true;
            Save();
        }
    }
}
