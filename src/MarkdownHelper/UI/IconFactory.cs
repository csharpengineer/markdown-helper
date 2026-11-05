using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MarkdownHelper.UI
{
    /// <summary>Draws the app mark at any size, so the tray icon stays sharp at every DPI.</summary>
    public static class IconFactory
    {
        public static readonly Color DefaultBackground = Color.FromArgb(0, 95, 184);

        public static Bitmap Draw(int size, Color background, Color foreground, Color? dot = null)
        {
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float unit = size / 20f;

                using (var brush = new SolidBrush(background))
                using (var shape = RoundedRect(0, 0, size, size, 5 * unit))
                {
                    g.FillPath(brush, shape);
                }

                using (var brush = new SolidBrush(foreground))
                using (var letter = new GraphicsPath())
                {
                    letter.AddPolygon(Scale(unit,
                        new PointF(3.6f, 13.6f), new PointF(3.6f, 6.4f), new PointF(5.5f, 6.4f),
                        new PointF(7.5f, 9.1f), new PointF(9.5f, 6.4f), new PointF(11.4f, 6.4f),
                        new PointF(11.4f, 13.6f), new PointF(9.5f, 13.6f), new PointF(9.5f, 9.3f),
                        new PointF(7.5f, 11.9f), new PointF(5.5f, 9.3f), new PointF(5.5f, 13.6f)));
                    g.FillPath(brush, letter);
                }

                using (var pen = new Pen(foreground, Math.Max(1f, 1.6f * unit)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLine(pen, 14.7f * unit, 7.5f * unit, 14.7f * unit, 12.6f * unit);
                    g.DrawLines(pen, Scale(unit, new PointF(12.6f, 10.6f), new PointF(14.7f, 13.2f), new PointF(16.8f, 10.6f)));
                }

                if (dot != null)
                {
                    float d = size * 0.44f;
                    using (var ring = new SolidBrush(Color.FromArgb(32, 32, 32)))
                    using (var brush = new SolidBrush(dot.Value))
                    {
                        g.FillEllipse(ring, size - d - 0.5f, -0.5f, d + 1, d + 1);
                        g.FillEllipse(brush, size - d, 0, d, d);
                    }
                }
            }
            return bitmap;
        }

        private static PointF[] Scale(float unit, params PointF[] points)
        {
            var result = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = new PointF(points[i].X * unit, points[i].Y * unit);
            return result;
        }

        private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
