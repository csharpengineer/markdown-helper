using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MarkdownHelper.Core;
using MarkdownHelper.Windows;

namespace MarkdownHelper.UI
{
    /// <summary>The small notice shown after a copy. It never takes focus and fades on its own.</summary>
    public partial class PopupWindow : Window
    {
        private const double FadeMilliseconds = 200;
        private const int ConfirmMilliseconds = 900;

        private readonly TimeSpan lifetime;
        private readonly DispatcherTimer closeTimer;
        private DateTime deadline;
        private TimeSpan remaining;
        private bool closing;

        public PopupWindow(string markdown, int seconds)
        {
            InitializeComponent();
            lifetime = TimeSpan.FromSeconds(seconds);
            remaining = lifetime;
            Snippet.Text = FirstLine(markdown);

            closeTimer = new DispatcherTimer();
            closeTimer.Tick += (s, e) => FadeOut();

            SourceInitialized += (s, e) => WindowEffects.MakeNonActivating(this);
            Loaded += (s, e) => { PlaceAboveTray(); FadeIn(); StartCountdown(); };
            MouseEnter += (s, e) => PauseCountdown();
            MouseLeave += (s, e) => StartCountdown();
        }

        public event EventHandler PreviewRequested;

        /// <summary>Returns true when the formatted text reached the clipboard.</summary>
        public Func<bool> CopyFormatted { get; set; }

        private static string FirstLine(string markdown)
        {
            var document = MarkdownParser.Parse(markdown);
            foreach (var block in document.Blocks)
            {
                switch (block)
                {
                    case Heading heading: return InlineParser.PlainText(heading.Inlines);
                    case Paragraph paragraph: return InlineParser.PlainText(paragraph.Inlines);
                }
            }
            return "Ready to preview";
        }

        private void PlaceAboveTray()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth;
            Top = area.Bottom - ActualHeight;
        }

        private void FadeIn()
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(FadeMilliseconds)));
        }

        private void StartCountdown()
        {
            if (closing) return;
            deadline = DateTime.UtcNow + remaining;
            closeTimer.Interval = remaining;
            closeTimer.Start();
            LifeScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, remaining));
        }

        private void PauseCountdown()
        {
            if (closing) return;
            closeTimer.Stop();
            remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(1)) remaining = TimeSpan.FromSeconds(1);

            double scale = LifeScale.ScaleX;
            LifeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            LifeScale.ScaleX = scale;
        }

        private void FadeOut()
        {
            if (closing) return;
            closing = true;
            closeTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(FadeMilliseconds));
            fade.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void OnDismiss(object sender, RoutedEventArgs e) => FadeOut();

        private void OnPreview(object sender, RoutedEventArgs e)
        {
            PreviewRequested?.Invoke(this, EventArgs.Empty);
            FadeOut();
        }

        private void OnCopyFormatted(object sender, RoutedEventArgs e)
        {
            bool ok = CopyFormatted != null && CopyFormatted();
            CopyFormattedButton.Content = ok ? "✓ Copied" : "Copy failed";
            closeTimer.Stop();
            closeTimer.Interval = TimeSpan.FromMilliseconds(ConfirmMilliseconds);
            closeTimer.Start();
        }
    }
}
