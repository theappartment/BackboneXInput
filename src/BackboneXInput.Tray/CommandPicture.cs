using System.Drawing.Drawing2D;
using BackboneXInput.Core;
using Control = BackboneXInput.Core.Control;

namespace BackboneXInput.Tray;

internal sealed class CommandPicture : System.Windows.Forms.Control
{
    public Control Target { get; set; }
    public CaptureStage Stage { get; set; }
    private static readonly Color accent = Color.FromArgb(0, 125, 125);

    public CommandPicture() { DoubleBuffered = true; BackColor = Color.White; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(Width / 340f, Height / 154f);
        if (scale <= 0) return;
        g.TranslateTransform((Width - 340 * scale) / 2, (Height - 154 * scale) / 2);
        g.ScaleTransform(scale, scale);
        using var body = new SolidBrush(Color.FromArgb(235, 238, 240));
        using var outline = new Pen(Color.FromArgb(170, 180, 185), 1.5f);
        PointF[] shape = [new(68, 32), new(272, 32), new(308, 139), new(270, 149), new(235, 117), new(105, 117), new(70, 149), new(32, 139)];
        g.FillPolygon(body, shape); g.DrawPolygon(outline, shape);
        var active = Stage is CaptureStage.Positive or CaptureStage.Opposite or CaptureStage.Review;
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        using var text = new SolidBrush(Color.FromArgb(35, 45, 50));
        using var white = new SolidBrush(Color.White);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        void Pad(string label, RectangleF area, bool highlight)
        {
            using var brush = new SolidBrush(highlight && active ? accent : Color.FromArgb(205, 212, 217));
            g.FillRectangle(brush, area); g.DrawString(label, font, highlight && active ? white : text, area, centered);
        }
        void Circle(string label, float x, float y, Color color, bool highlight)
        {
            using var fill = new SolidBrush(highlight && active ? color : Color.FromArgb(212, 220, 224));
            var area = new RectangleF(x - 11, y - 11, 22, 22);
            g.FillEllipse(fill, area);
            g.DrawString(label, font, highlight && active ? white : text, area, centered);
            if (highlight && active) { using var ring = new Pen(accent, 2.5f); g.DrawEllipse(ring, x - 14, y - 14, 28, 28); }
        }
        Pad("LT", new(75, 4, 48, 21), Target == Control.LT);
        Pad("RT", new(217, 4, 48, 21), Target == Control.RT);
        Pad("LB", new(63, 27, 74, 20), Target == Control.LB);
        Pad("RB", new(203, 27, 74, 20), Target == Control.RB);
        Circle("Y", 258, 59, Color.FromArgb(175, 135, 0), Target == Control.Y);
        Circle("B", 282, 83, Color.FromArgb(185, 45, 45), Target == Control.B);
        Circle("A", 258, 107, Color.FromArgb(30, 125, 60), Target == Control.A);
        Circle("X", 234, 83, Color.FromArgb(35, 105, 180), Target == Control.X);
        Pad("View", new(136, 70, 33, 20), Target == Control.View);
        Pad("Menu", new(175, 70, 37, 20), Target == Control.Menu);
        Pad("\u2191", new(93, 97, 15, 16), Target == Control.Up);
        Pad("\u2193", new(93, 126, 15, 16), Target == Control.Down);
        Pad("\u2190", new(77, 112, 16, 15), Target == Control.Left);
        Pad("\u2192", new(108, 112, 16, 15), Target == Control.Right);

        void Stick(float x, float y, bool left)
        {
            var selected = left ? Target is Control.LeftX or Control.LeftY or Control.L3 : Target is Control.RightX or Control.RightY or Control.R3;
            using var pen = new Pen(selected && active ? accent : Color.FromArgb(135, 150, 160), selected && active ? 3 : 2);
            g.FillEllipse(body, x - 20, y - 20, 40, 40); g.DrawEllipse(pen, x - 20, y - 20, 40, 40);
            var click = Target is Control.L3 or Control.R3;
            if (selected && active && !click)
            {
                var horizontal = Target is Control.LeftX or Control.RightX;
                var direction = Stage == CaptureStage.Opposite ? -1 : 1;
                using var arrow = new Pen(accent, 3) { EndCap = LineCap.ArrowAnchor };
                g.DrawLine(arrow, x, y, x + (horizontal ? 17 * direction : 0), y + (horizontal ? 0 : -17 * direction));
            }
            else g.DrawString(left ? "L3" : "R3", font, text, new RectangleF(x - 20, y - 20, 40, 40), centered);
        }
        Stick(95, 72, true); Stick(186, 120, false);
    }
}
