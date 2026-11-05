using System.Windows;
using System.Windows.Media;

namespace MarkdownHelper.UI
{
    /// <summary>The colors of one theme. Values come from the approved design.</summary>
    public sealed class ThemePalette
    {
        public bool IsDark { get; private set; }
        public bool IsHighContrast { get; private set; }

        public Color Shell { get; private set; }
        public Color Layer { get; private set; }
        public Color Card { get; private set; }
        public Color Stroke { get; private set; }
        public Color StrokeStrong { get; private set; }
        public Color Text { get; private set; }
        public Color TextSecondary { get; private set; }
        public Color TextTertiary { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentHover { get; private set; }
        public Color OnAccent { get; private set; }
        public Color AccentSoft { get; private set; }
        public Color Hover { get; private set; }
        public Color Press { get; private set; }
        public Color CodeBackground { get; private set; }
        public Color SourceBackground { get; private set; }
        public Color Danger { get; private set; }
        public Color OnDanger { get; private set; }
        public Color DangerSoft { get; private set; }
        public Color DangerLine { get; private set; }
        public Color Warning { get; private set; }
        public Color Success { get; private set; }
        public Color OnSuccess { get; private set; }

        private static Color Hex(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        private static Color Alpha(byte a, uint rgb) => Color.FromArgb(a, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        public static readonly ThemePalette Light = new ThemePalette
        {
            Shell = Hex(0xF3F3F3),
            Layer = Hex(0xFBFBFB),
            Card = Hex(0xFFFFFF),
            Stroke = Alpha(0x17, 0x000000),
            StrokeStrong = Alpha(0x29, 0x000000),
            Text = Hex(0x1B1B1B),
            TextSecondary = Hex(0x5C5C5C),
            TextTertiary = Hex(0x8A8A8A),
            Accent = Hex(0x005FB8),
            AccentHover = Hex(0x196EBF),
            OnAccent = Hex(0xFFFFFF),
            AccentSoft = Alpha(0x1A, 0x005FB8),
            Hover = Alpha(0x0A, 0x000000),
            Press = Alpha(0x12, 0x000000),
            CodeBackground = Hex(0xF4F4F5),
            SourceBackground = Hex(0xF7F7F8),
            Danger = Hex(0xC42B1C),
            OnDanger = Hex(0xFFFFFF),
            DangerSoft = Alpha(0x12, 0xC42B1C),
            DangerLine = Alpha(0x59, 0xC42B1C),
            Warning = Hex(0x9D5D00),
            Success = Hex(0x0F7B0F),
            OnSuccess = Hex(0xFFFFFF)
        };

        public static readonly ThemePalette Dark = new ThemePalette
        {
            IsDark = true,
            Shell = Hex(0x202020),
            Layer = Hex(0x272727),
            Card = Hex(0x2C2C2C),
            Stroke = Alpha(0x14, 0xFFFFFF),
            StrokeStrong = Alpha(0x29, 0xFFFFFF),
            Text = Hex(0xFFFFFF),
            TextSecondary = Hex(0xC8C8C8),
            TextTertiary = Hex(0x9B9B9B),
            Accent = Hex(0x60CDFF),
            AccentHover = Hex(0x7BD6FF),
            OnAccent = Hex(0x06202E),
            AccentSoft = Alpha(0x21, 0x60CDFF),
            Hover = Alpha(0x0F, 0xFFFFFF),
            Press = Alpha(0x0A, 0xFFFFFF),
            CodeBackground = Hex(0x1F1F1F),
            SourceBackground = Hex(0x1E1E1E),
            Danger = Hex(0xFF99A4),
            OnDanger = Hex(0x2C0B0E),
            DangerSoft = Alpha(0x17, 0xFF99A4),
            DangerLine = Alpha(0x66, 0xFF99A4),
            Warning = Hex(0xFCE100),
            Success = Hex(0x6CCB5F),
            OnSuccess = Hex(0x08240A)
        };

        /// <summary>Built from the live system colors, because those are what high contrast users chose.</summary>
        public static ThemePalette HighContrast()
        {
            Color window = SystemColors.WindowColor;
            Color text = SystemColors.WindowTextColor;
            Color highlight = SystemColors.HighlightColor;
            Color onHighlight = SystemColors.HighlightTextColor;
            return new ThemePalette
            {
                IsHighContrast = true,
                IsDark = window.R + window.G + window.B < 384,
                Shell = window,
                Layer = window,
                Card = window,
                Stroke = text,
                StrokeStrong = text,
                Text = text,
                TextSecondary = text,
                TextTertiary = text,
                Accent = highlight,
                AccentHover = highlight,
                OnAccent = onHighlight,
                AccentSoft = window,
                Hover = window,
                Press = window,
                CodeBackground = window,
                SourceBackground = window,
                Danger = highlight,
                OnDanger = onHighlight,
                DangerSoft = window,
                DangerLine = text,
                Warning = text,
                Success = highlight,
                OnSuccess = onHighlight
            };
        }
    }
}
