using System;
using System.Windows;
using System.Windows.Media;

namespace MarkdownHelper.UI
{
    /// <summary>Publishes the palette as brushes that every window binds to.</summary>
    public static class Theme
    {
        public static ThemePalette Current { get; private set; } = ThemePalette.Light;

        public static event EventHandler Changed;

        public static void Apply(ThemePalette palette)
        {
            Current = palette;
            var resources = Application.Current.Resources;

            void Set(string key, Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                resources[key] = brush;
            }

            Set("ShellBrush", palette.Shell);
            Set("LayerBrush", palette.Layer);
            Set("CardBrush", palette.Card);
            Set("StrokeBrush", palette.Stroke);
            Set("StrokeStrongBrush", palette.StrokeStrong);
            Set("TextBrush", palette.Text);
            Set("TextSecondaryBrush", palette.TextSecondary);
            Set("TextTertiaryBrush", palette.TextTertiary);
            Set("AccentBrush", palette.Accent);
            Set("AccentHoverBrush", palette.AccentHover);
            Set("OnAccentBrush", palette.OnAccent);
            Set("AccentSoftBrush", palette.AccentSoft);
            Set("HoverBrush", palette.Hover);
            Set("PressBrush", palette.Press);
            Set("CodeBrush", palette.CodeBackground);
            Set("SourceBrush", palette.SourceBackground);
            Set("DangerBrush", palette.Danger);
            Set("OnDangerBrush", palette.OnDanger);
            Set("DangerSoftBrush", palette.DangerSoft);
            Set("DangerLineBrush", palette.DangerLine);
            Set("WarningBrush", palette.Warning);
            Set("SuccessBrush", palette.Success);
            Set("OnSuccessBrush", palette.OnSuccess);

            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
