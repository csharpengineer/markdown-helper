using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MarkdownHelper.Core;

namespace MarkdownHelper.UI
{
    /// <summary>
    /// One removable piece. It is a real button, so the keyboard and screen readers can
    /// remove pieces too; hovering or focusing highlights it and shows the Remove label.
    /// </summary>
    internal sealed class PieceView : Button
    {
        private const int NameLength = 60;

        private readonly Border box;
        private readonly Border pill;

        public PieceView(Piece piece, ThemePalette palette, Action<Piece> remove)
        {
            Margin = new Thickness(0, 0, 0, 7);
            Cursor = Cursors.Hand;
            FocusVisualStyle = null;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };

            var document = FlowDocumentBuilder.BuildPiece(piece, palette);
            var rendering = new RichTextBox
            {
                Document = document,
                IsReadOnly = true,
                IsHitTestVisible = false,
                Focusable = false,
                IsTabStop = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            box = new Border
            {
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                Child = rendering
            };

            var label = new TextBlock { Text = "✕ Remove", FontSize = 12, FontWeight = FontWeights.SemiBold };
            label.SetResourceReference(TextBlock.ForegroundProperty, "OnDangerBrush");

            pill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -10, 10, 0),
                Visibility = Visibility.Collapsed,
                Child = label
            };
            pill.SetResourceReference(Border.BackgroundProperty, "DangerBrush");

            var content = new Grid { Background = Brushes.Transparent };
            content.Children.Add(box);
            content.Children.Add(pill);
            Content = content;

            string text = new TextRange(document.ContentStart, document.ContentEnd).Text.Trim();
            if (text.Length == 0) text = "horizontal line";
            AutomationProperties.SetName(this, "Remove: " + (text.Length > NameLength ? text.Substring(0, NameLength) : text));

            MouseEnter += (s, e) => UpdateHighlight();
            MouseLeave += (s, e) => UpdateHighlight();
            GotKeyboardFocus += (s, e) => UpdateHighlight();
            LostKeyboardFocus += (s, e) => UpdateHighlight();
            Click += (s, e) => remove(piece);
        }

        private void UpdateHighlight()
        {
            bool on = IsMouseOver || IsKeyboardFocused;
            pill.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (on)
            {
                box.SetResourceReference(Border.BackgroundProperty, "DangerSoftBrush");
                box.SetResourceReference(Border.BorderBrushProperty, "DangerLineBrush");
            }
            else
            {
                box.Background = Brushes.Transparent;
                box.BorderBrush = Brushes.Transparent;
            }
        }
    }
}
