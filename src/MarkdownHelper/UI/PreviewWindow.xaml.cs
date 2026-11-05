using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using MarkdownHelper.Core;
using MarkdownHelper.Windows;

namespace MarkdownHelper.UI
{
    public partial class PreviewWindow : Window
    {
        private const double ReadingWidth = 680;
        private const double DocumentPadding = 30;
        private const int EditSettleMilliseconds = 250;
        private const int CopiedMessageMilliseconds = 1600;

        private readonly EditSession session = new EditSession();
        private readonly DispatcherTimer editorSettle;
        private readonly DispatcherTimer sourceSettle;
        private readonly DispatcherTimer copiedReset;
        private readonly Func<string, string, bool> copyToClipboard;
        private bool suppressEvents;
        private bool ready;

        public PreviewWindow(AppSettings settings, Func<string, string, bool> copyToClipboard)
        {
            this.copyToClipboard = copyToClipboard;
            Settings = settings;
            InitializeComponent();

            editorSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(EditSettleMilliseconds) };
            editorSettle.Tick += (s, e) => { editorSettle.Stop(); CommitEditor(); };
            sourceSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(EditSettleMilliseconds) };
            sourceSettle.Tick += (s, e) => { sourceSettle.Stop(); CommitSource(); };
            copiedReset = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CopiedMessageMilliseconds) };
            copiedReset.Tick += (s, e) => { copiedReset.Stop(); ResetCopyButton(); };

            EditMode.IsChecked = settings.LastMode == EditorMode.Edit;
            RemoveMode.IsChecked = settings.LastMode == EditorMode.RemovePieces;
            MarkdownSwitch.IsChecked = settings.ShowMarkdown;

            Editor.Document = FlowDocumentBuilder.Build(new MarkdownDocument(), Theme.Current);
            DataObject.AddPastingHandler(Editor, OnEditorPaste);

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => OnUndo(s, e), (s, e) => e.CanExecute = session.CanUndo));
            Editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => OnUndo(s, e), (s, e) => e.CanExecute = session.CanUndo));
            Source.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => OnUndo(s, e), (s, e) => e.CanExecute = session.CanUndo));

            SourceInitialized += (s, e) => WindowEffects.Apply(this, Theme.Current);
            StateChanged += (s, e) => FitMaximized();
            Theme.Changed += OnThemeChanged;
            Closed += (s, e) => Theme.Changed -= OnThemeChanged;

            ready = true;
            ApplyLayout();
            RefreshAll();
        }

        public AppSettings Settings { get; }

        public event EventHandler SettingsRequested;
        public event EventHandler LayoutPreferenceChanged;

        /// <summary>Text the window is showing, in markdown.</summary>
        public string Markdown => session.Markdown;

        public void SetHotkeyHint(string gesture) => EmptyDetail.Text = "Press " + gesture + " any time to open this window";

        /// <summary>Loads new text. The previous text stays reachable through Undo.</summary>
        public void LoadMarkdown(string markdown)
        {
            FlushPendingEdits();
            session.Load(markdown);
            RefreshAll();
        }

        private bool IsRemoveMode => RemoveMode.IsChecked == true;
        private bool ShowsSource => MarkdownSwitch.IsChecked == true;

        private void ApplyLayout()
        {
            SourcePane.Visibility = ShowsSource ? Visibility.Visible : Visibility.Collapsed;
            SourceColumn.Width = ShowsSource ? new GridLength(0.72, GridUnitType.Star) : new GridLength(0);
            GapColumn.Width = ShowsSource ? new GridLength(10) : new GridLength(0);

            Editor.Visibility = IsRemoveMode ? Visibility.Collapsed : Visibility.Visible;
            PiecesScroller.Visibility = IsRemoveMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshAll()
        {
            RebuildEditor();
            if (IsRemoveMode) RebuildPieces();
            SetSourceText(session.Markdown);
            RefreshStatus();
        }

        private void RebuildEditor()
        {
            suppressEvents = true;
            // Moving blocks into the same document keeps screen readers attached to it.
            var built = FlowDocumentBuilder.Build(session.Parse(), Theme.Current);
            var target = Editor.Document;
            target.Foreground = built.Foreground;
            target.Blocks.Clear();
            target.Blocks.AddRange(built.Blocks.ToList());
            ApplyDocumentPadding();
            suppressEvents = false;
        }

        private void RebuildPieces()
        {
            PiecesList.Items.Clear();
            foreach (var piece in session.Pieces()) PiecesList.Items.Add(new PieceView(piece, Theme.Current, RemovePiece));
        }

        private void RemovePiece(Piece piece)
        {
            session.Remove(piece);
            RefreshAll();
        }

        private void SetSourceText(string text)
        {
            if (Source.Text == text) return;
            suppressEvents = true;
            Source.Text = text;
            suppressEvents = false;
        }

        private void RefreshStatus()
        {
            var document = session.Parse();
            int length = DiscordRenderer.Format(document).Length;
            bool over = length > DiscordRenderer.MessageLimit;

            StatusText.Text = length.ToString("N0") + " characters · " +
                (over ? "over Discord's " + DiscordRenderer.MessageLimit.ToString("N0") + " limit" : "fits in one Discord message");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, over ? "WarningBrush" : "TextSecondaryBrush");
            StatusDot.SetResourceReference(Shape_Fill, over ? "WarningBrush" : "SuccessBrush");

            UndoButton.IsEnabled = session.CanUndo;
            CopyButton.IsEnabled = session.Markdown.Trim().Length > 0;
            EmptyHint.Visibility = document.Blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static readonly DependencyProperty Shape_Fill = System.Windows.Shapes.Shape.FillProperty;

        private void FlushPendingEdits()
        {
            if (editorSettle.IsEnabled) { editorSettle.Stop(); CommitEditor(); }
            if (sourceSettle.IsEnabled) { sourceSettle.Stop(); CommitSource(); }
        }

        private void OnEditorChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents) return;
            editorSettle.Stop();
            editorSettle.Start();
        }

        private void CommitEditor()
        {
            session.Commit(FlowMarkdownReader.Read(Editor.Document));
            SetSourceText(session.Markdown);
            RefreshStatus();
        }

        private void OnSourceChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents) return;
            sourceSettle.Stop();
            sourceSettle.Start();
        }

        private void CommitSource()
        {
            session.Commit(Source.Text);
            RebuildEditor();
            if (IsRemoveMode) RebuildPieces();
            RefreshStatus();
        }

        /// <summary>Pasting into an empty window is treated as markdown; otherwise only text goes in.</summary>
        private void OnEditorPaste(object sender, DataObjectPastingEventArgs e)
        {
            string text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
            if (text == null) { e.CancelCommand(); return; }

            if (session.Markdown.Trim().Length == 0)
            {
                e.CancelCommand();
                LoadMarkdown(text);
                return;
            }

            var plain = new DataObject();
            plain.SetData(DataFormats.UnicodeText, text);
            e.DataObject = plain;
        }

        private void OnEditorSized(object sender, SizeChangedEventArgs e) => ApplyDocumentPadding();

        /// <summary>Keeps a comfortable line length while the scroll bar stays at the window edge.</summary>
        private void ApplyDocumentPadding()
        {
            double width = Editor.ActualWidth;
            double side = Math.Max(DocumentPadding, (width - ReadingWidth) / 2);
            bool narrow = ShowsSource;
            Editor.Document.PagePadding = new Thickness(narrow ? 22 : side, 24, narrow ? 22 : side, 24);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            WindowEffects.Apply(this, Theme.Current);
            FlushPendingEdits();
            RebuildEditor();
            if (IsRemoveMode) RebuildPieces();
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            FlushPendingEdits();
            Settings.LastMode = IsRemoveMode ? EditorMode.RemovePieces : EditorMode.Edit;
            LayoutPreferenceChanged?.Invoke(this, EventArgs.Empty);
            ApplyLayout();
            RefreshAll();
        }

        private void OnMarkdownSwitch(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            FlushPendingEdits();
            Settings.ShowMarkdown = ShowsSource;
            LayoutPreferenceChanged?.Invoke(this, EventArgs.Empty);
            ApplyLayout();
            ApplyDocumentPadding();
        }

        private void OnUndo(object sender, RoutedEventArgs e)
        {
            FlushPendingEdits();
            session.Undo();
            RefreshAll();
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            FlushPendingEdits();
            var document = session.Parse();
            bool ok = copyToClipboard(HtmlRenderer.Render(document), DiscordRenderer.Format(document));

            CopyButton.Content = ok ? "✓ Copied" : "Copy failed";
            if (ok)
            {
                CopyButton.SetResourceReference(BackgroundProperty, "SuccessBrush");
                CopyButton.SetResourceReference(ForegroundProperty, "OnSuccessBrush");
            }
            copiedReset.Stop();
            copiedReset.Start();
        }

        private void ResetCopyButton()
        {
            CopyButton.Content = "Copy";
            CopyButton.ClearValue(BackgroundProperty);
            CopyButton.ClearValue(ForegroundProperty);
        }

        private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        /// <summary>A maximized chrome-less window hangs over the screen edge by its resize border.</summary>
        private void FitMaximized()
        {
            var inset = SystemParameters.WindowResizeBorderThickness;
            Root.Margin = WindowState == WindowState.Maximized
                ? new Thickness(inset.Left + 2, inset.Top + 2, inset.Right + 2, inset.Bottom + 2)
                : new Thickness(0);
        }
    }
}
