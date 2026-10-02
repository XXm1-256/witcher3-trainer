using System.Drawing.Drawing2D;

namespace Witcher3Modifier;

internal sealed class HelpIcon : Control
{
    public HelpIcon()
    {
        Size = new Size(26, 26);
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float diameter = Math.Min(Width, Height) - 3;
        var circle = new RectangleF((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        using var edge = new Pen(TrainerTheme.Muted, 1.4f);
        e.Graphics.DrawEllipse(edge, circle);
        TextRenderer.DrawText(e.Graphics, "i", Font, ClientRectangle, TrainerTheme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
