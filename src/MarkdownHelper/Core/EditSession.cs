using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>A removable part of the document, as shown in Remove pieces mode.</summary>
    public sealed class Piece
    {
        public Piece(Block block, int startLine, int endLine, ListBlock owner = null, int itemNumber = 1)
        {
            Block = block;
            StartLine = startLine;
            EndLine = endLine;
            OwnerList = owner;
            ItemNumber = itemNumber;
        }

        public Block Block { get; }
        public int StartLine { get; }
        public int EndLine { get; }

        /// <summary>The list this item belongs to, or null when the piece is a whole block.</summary>
        public ListBlock OwnerList { get; }
        public int ItemNumber { get; }
    }

    /// <summary>
    /// The text being worked on and its undo history. Markdown is the single source of
    /// truth: every view is derived from <see cref="Markdown"/>.
    /// </summary>
    public sealed class EditSession
    {
        private const int HistoryLimit = 200;

        private readonly List<string> history = new List<string>();

        public string Markdown { get; private set; } = "";

        public bool CanUndo => history.Count > 0;

        public event EventHandler Changed;

        /// <summary>Records new text. Unchanged text makes no history entry.</summary>
        public void Commit(string markdown)
        {
            string next = markdown ?? "";
            if (next == Markdown) return;

            history.Add(Markdown);
            if (history.Count > HistoryLimit) history.RemoveAt(0);
            Markdown = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (!CanUndo) return;
            Markdown = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public MarkdownDocument Parse() => MarkdownParser.Parse(Markdown);

        public IReadOnlyList<Piece> Pieces()
        {
            var pieces = new List<Piece>();
            foreach (var block in Parse().Blocks)
            {
                if (block is ListBlock list)
                {
                    for (int i = 0; i < list.Items.Count; i++)
                    {
                        var item = list.Items[i];
                        pieces.Add(new Piece(item, item.StartLine, item.EndLine, list, list.Start + i));
                    }
                }
                else
                {
                    pieces.Add(new Piece(block, block.StartLine, block.EndLine));
                }
            }
            return pieces;
        }

        /// <summary>Removes a piece by its source lines, so the rest of the text is untouched.</summary>
        public void Remove(Piece piece)
        {
            var lines = MarkdownParser.SplitLines(Markdown).ToList();
            int start = Math.Max(0, piece.StartLine);
            int end = Math.Min(lines.Count - 1, piece.EndLine);
            if (start > end) return;

            lines.RemoveRange(start, end - start + 1);

            // Drop one of the blank lines that would otherwise be left doubled up.
            bool blankBefore = start == 0 || string.IsNullOrWhiteSpace(lines[start - 1]);
            if (start < lines.Count && blankBefore && string.IsNullOrWhiteSpace(lines[start])) lines.RemoveAt(start);

            Commit(string.Join("\n", lines).Trim('\n'));
        }
    }
}
