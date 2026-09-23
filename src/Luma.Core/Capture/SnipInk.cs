using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Luma.Core.Capture;

public enum SnipMarkKind
{
    Rectangle,
    Ellipse,
    Arrow,
    Text,
    Pen
}

public sealed class SnipMark
{
    public const int DefaultColor = unchecked((int)0xFFE81123);

    public SnipMark(SnipMarkKind kind, int x1, int y1, int x2, int y2, string? text, float fontSize, int argb = DefaultColor, int[]? points = null)
    {
        Kind = kind;
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        Text = text;
        FontSize = fontSize;
        Argb = argb;
        Points = points;
    }

    public SnipMarkKind Kind { get; }
    public int X1 { get; }
    public int Y1 { get; }
    public int X2 { get; }
    public int Y2 { get; }
    public string? Text { get; }
    public float FontSize { get; }
    public int Argb { get; }
    public int[]? Points { get; }
}

public static class SnipInk
{
    public const float StrokeWidth = 3;
    public const float PenWidth = 5;
    public static readonly int[] Palette =
    [
        SnipMark.DefaultColor,
        unchecked((int)0xFFFF8C00),
        unchecked((int)0xFFFFC83D),
        unchecked((int)0xFF10893E),
        unchecked((int)0xFF0078D4),
        unchecked((int)0xFF8764B8),
        unchecked((int)0xFFFFFFFF),
        unchecked((int)0xFF1A1A1A)
    ];

    public static void Draw(Graphics graphics, int originX, int originY, IReadOnlyList<SnipMark> marks)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        foreach (var mark in marks)
        {
            var color = Color.FromArgb(mark.Argb);
            using var pen = new Pen(color, mark.Kind == SnipMarkKind.Pen ? PenWidth : StrokeWidth);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            using var brush = new SolidBrush(color);
            switch (mark.Kind)
            {
                case SnipMarkKind.Rectangle:
                    DrawBox(graphics, pen, originX, originY, mark, ellipse: false);
                    break;
                case SnipMarkKind.Ellipse:
                    DrawBox(graphics, pen, originX, originY, mark, ellipse: true);
                    break;
                case SnipMarkKind.Arrow:
                    using (var arrow = (Pen)pen.Clone())
                    using (var cap = new AdjustableArrowCap(5, 6, true))
                    {
                        arrow.CustomEndCap = cap;
                        graphics.DrawLine(arrow, mark.X1 - originX, mark.Y1 - originY, mark.X2 - originX, mark.Y2 - originY);
                    }

                    break;
                case SnipMarkKind.Pen:
                    DrawPen(graphics, pen, brush, originX, originY, mark);
                    break;
                case SnipMarkKind.Text when !string.IsNullOrWhiteSpace(mark.Text):
                    using (var font = new Font("Segoe UI", mark.FontSize > 0 ? mark.FontSize : 18, FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        graphics.DrawString(mark.Text, font, brush, mark.X1 - originX, mark.Y1 - originY);
                    }

                    break;
            }
        }
    }

    private static void DrawPen(Graphics graphics, Pen pen, Brush brush, int originX, int originY, SnipMark mark)
    {
        if (mark.Points is not { Length: >= 2 })
        {
            return;
        }

        var count = mark.Points.Length / 2;
        var points = new PointF[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = new PointF(mark.Points[i * 2] - originX, mark.Points[i * 2 + 1] - originY);
        }

        if (count < 2 || (Math.Abs(points[0].X - points[^1].X) < 1 && Math.Abs(points[0].Y - points[^1].Y) < 1))
        {
            graphics.FillEllipse(brush, points[0].X - PenWidth / 2, points[0].Y - PenWidth / 2, PenWidth, PenWidth);
            return;
        }

        graphics.DrawLines(pen, points);
    }

    private static void DrawBox(Graphics graphics, Pen pen, int originX, int originY, SnipMark mark, bool ellipse)
    {
        var left = Math.Min(mark.X1, mark.X2) - originX;
        var top = Math.Min(mark.Y1, mark.Y2) - originY;
        var width = Math.Abs(mark.X2 - mark.X1);
        var height = Math.Abs(mark.Y2 - mark.Y1);
        if (width < 1 || height < 1)
        {
            return;
        }

        if (ellipse)
        {
            graphics.DrawEllipse(pen, left, top, width, height);
        }
        else
        {
            graphics.DrawRectangle(pen, left, top, width, height);
        }
    }
}
