using System.Collections.Generic;

namespace MarkdownHelper.Core
{
    /// <summary>A parsed document. Line numbers on blocks refer to the source text.</summary>
    public sealed class MarkdownDocument
    {
        public List<Block> Blocks { get; } = new List<Block>();
    }

    public abstract class Block
    {
        /// <summary>First source line of this block, zero-based.</summary>
        public int StartLine { get; set; }

        /// <summary>Last source line of this block, zero-based and inclusive.</summary>
        public int EndLine { get; set; }
    }

    public sealed class Heading : Block
    {
        public int Level { get; set; }
        public List<Inline> Inlines { get; set; } = new List<Inline>();
    }

    public sealed class Paragraph : Block
    {
        public List<Inline> Inlines { get; set; } = new List<Inline>();
    }

    public sealed class ThematicBreak : Block
    {
    }

    public sealed class CodeBlock : Block
    {
        public string Language { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public sealed class Quote : Block
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public sealed class ListBlock : Block
    {
        public bool Ordered { get; set; }
        public int Start { get; set; } = 1;
        public bool Tight { get; set; } = true;
        public List<ListItem> Items { get; set; } = new List<ListItem>();
    }

    public sealed class ListItem : Block
    {
        public List<Block> Blocks { get; set; } = new List<Block>();

        /// <summary>Null for a normal item; true or false for a task list item.</summary>
        public bool? Checked { get; set; }
    }

    public enum ColumnAlign
    {
        None,
        Left,
        Center,
        Right
    }

    public sealed class Table : Block
    {
        public List<ColumnAlign> Aligns { get; set; } = new List<ColumnAlign>();
        public List<List<Inline>> Header { get; set; } = new List<List<Inline>>();
        public List<List<List<Inline>>> Rows { get; set; } = new List<List<List<Inline>>>();
    }

    public abstract class Inline
    {
    }

    public sealed class Text : Inline
    {
        public Text(string value) { Value = value; }
        public string Value { get; set; }
    }

    public sealed class SoftBreak : Inline
    {
    }

    public sealed class HardBreak : Inline
    {
    }

    public sealed class CodeSpan : Inline
    {
        public CodeSpan(string code) { Code = code; }
        public string Code { get; set; }
    }

    public abstract class ContainerInline : Inline
    {
        public List<Inline> Children { get; set; } = new List<Inline>();
    }

    public sealed class Strong : ContainerInline
    {
    }

    public sealed class Emphasis : ContainerInline
    {
    }

    public sealed class Strike : ContainerInline
    {
    }

    public sealed class Link : ContainerInline
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public sealed class Image : Inline
    {
        public string Url { get; set; } = "";
        public string Alt { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
