using System.Drawing.Drawing2D;

namespace Witcher3Modifier;

internal static class TrainerTheme
{
    internal static readonly Color Surface = Color.FromArgb(20, 20, 22);
    internal static readonly Color Input = Color.FromArgb(31, 31, 34);
    internal static readonly Color Ink = Color.FromArgb(239, 239, 242);
    internal static readonly Color Muted = Color.FromArgb(185, 185, 190);
    internal static readonly Color Red = Color.FromArgb(116, 33, 40);

    internal static GraphicsPath Outline(RectangleF r, float corner = 12)
    {
        float radius = Math.Min(corner, Math.Min(r.Width, r.Height) / 2);
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
        path.AddLine(r.Left + radius, r.Top, r.Right - 2, r.Top);
        path.AddArc(r.Right - 4, r.Top, 4, 4, 270, 90);
        path.AddLine(r.Right, r.Top + 2, r.Right, r.Bottom - radius);
        path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddLine(r.Right - radius, r.Bottom, r.Left + 2, r.Bottom);
        path.AddArc(r.Left, r.Bottom - 4, 4, 4, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void Apply(Control control)
    {
        control.BackColor = Surface;
        control.ForeColor = Ink;
        if (control is Label label && label.Font.Size < 10) label.ForeColor = Muted;
        if (control is TextBox or NumericUpDown or ListBox or ComboBox) control.BackColor = Input;
        if (control is TextBox { Multiline: false } inputText) { inputText.AutoSize = false; inputText.MinimumSize = new Size(0,36); }
        if (control is NumericUpDown number) number.MinimumSize = new Size(0,36);
        if (control is TextBox text) text.BorderStyle = BorderStyle.FixedSingle;
        if (control is ListBox list) { list.BorderStyle = BorderStyle.FixedSingle; list.IntegralHeight = false; }
        if (control is ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 32;
            combo.DrawItem += (_, e) =>
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using var brush = new SolidBrush(selected ? Red : Input);
                e.Graphics.FillRectangle(brush, e.Bounds);
                string text = (e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text) ?? "";
                TextRenderer.DrawText(e.Graphics, text, combo.Font, Rectangle.Inflate(e.Bounds, -5, 0),
                    combo.Enabled ? Ink : Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
        }
        if (control is StatusStrip strip) { strip.Renderer = new DarkStripRenderer(); strip.SizingGrip = true; strip.Font = control.Parent?.Font ?? control.Font; }
        foreach (Control child in control.Controls) Apply(child);
    }

    private sealed class DarkStripColors : ProfessionalColorTable
    {
        public override Color StatusStripGradientBegin => Input;
        public override Color StatusStripGradientEnd => Surface;
    }

    private sealed class DarkStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkStripRenderer() : base(new DarkStripColors()) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var edge = new Pen(Color.FromArgb(76,76,82));
            e.Graphics.DrawLine(edge,0,0,e.ToolStrip.Width,0);
        }
    }
}

internal sealed class MetalButton : Button
{
    public bool Navigation { get; set; }
    public bool Active { get; set; }
    private bool hovered;
    private bool pressed;
    private float shine;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };

    public MetalButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        Padding = new Padding(8, 3, 8, 3);
        MinimumSize = new Size(75, 36);
        animation.Tick += (_, _) => { shine += .11f; if (shine >= 1) animation.Stop(); Invalidate(); };
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnClick(EventArgs e) { shine = 0; animation.Start(); base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 9 || Height < 10) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? TrainerTheme.Surface);
        var bounds = new RectangleF(2, pressed ? 4 : 1, Width - 5, Height - 7);
        using var shadowPath = TrainerTheme.Outline(new RectangleF(2, 5, Width - 5, Height - 7));
        using var shadow = new SolidBrush(Color.FromArgb(6,6,8));
        e.Graphics.FillPath(shadow, shadowPath);
        using var thicknessPath = TrainerTheme.Outline(new RectangleF(2, 4, Width - 5, Height - 7));
        using var thickness = new SolidBrush(Navigation ? Color.FromArgb(35,10,16) : Color.FromArgb(54,54,60));
        e.Graphics.FillPath(thickness, thicknessPath);
        using var path = TrainerTheme.Outline(bounds);
        using var gradient = new LinearGradientBrush(bounds, Color.White, Color.Gray, 78);
        gradient.InterpolationColors = new ColorBlend
        {
            Positions = [0, .18f, .46f, .49f, .55f, .86f, 1],
            Colors = Enabled ? [Color.FromArgb(238,238,239), Color.FromArgb(196,196,199), Color.FromArgb(174,174,178), Color.FromArgb(152,152,156), Color.FromArgb(210,210,213), Color.FromArgb(172,172,176), Color.FromArgb(222,222,225)]
                : [Color.FromArgb(86,86,90), Color.FromArgb(80,80,84), Color.FromArgb(74,74,78), Color.FromArgb(68,68,72), Color.FromArgb(80,80,84), Color.FromArgb(72,72,76), Color.FromArgb(86,86,90)]
        };
        if (Navigation)
        {
            using var navigationFill = new LinearGradientBrush(bounds, Color.White, Color.Black, 75);
            navigationFill.InterpolationColors = new ColorBlend
            {
                Positions = [0, .49f, .53f, 1],
                Colors = Active ? [Color.FromArgb(204,83,91), TrainerTheme.Red, Color.FromArgb(65,15,22), Color.FromArgb(125,37,47)]
                    : [Color.FromArgb(61,61,66), Color.FromArgb(31,31,35), Color.FromArgb(12,12,15), Color.FromArgb(43,43,48)]
            };
            e.Graphics.FillPath(navigationFill, path);
        }
        else e.Graphics.FillPath(gradient, path);
        using var border = new Pen(hovered && Enabled ? Color.White : Color.FromArgb(155,155,160));
        e.Graphics.DrawPath(border, path);
        var bevelState = e.Graphics.Save();
        e.Graphics.SetClip(path);
        using var bevel = new Pen(Color.FromArgb(Navigation ? 85 : 165, Color.White));
        e.Graphics.DrawLine(bevel,bounds.Left+10,bounds.Top+1,bounds.Right-3,bounds.Top+1);
        using var lowerEdge = new Pen(Color.FromArgb(95,0,0,0),2);
        e.Graphics.DrawLine(lowerEdge,bounds.Left+2,bounds.Bottom-1,bounds.Right-9,bounds.Bottom-1);
        e.Graphics.Restore(bevelState);
        if (animation.Enabled)
        {
            var state = e.Graphics.Save(); e.Graphics.SetClip(path);
            using var reflection = new SolidBrush(Color.FromArgb(70, Color.White));
            float x = shine * (Width + 50) - 50;
            e.Graphics.FillPolygon(reflection, [new PointF(x,0), new PointF(x+18,0), new PointF(x+38,Height), new PointF(x+20,Height)]);
            e.Graphics.Restore(state);
        }
        var textBounds = Rectangle.Round(bounds); textBounds.Inflate(-7, -2);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Navigation ? TrainerTheme.Ink : Enabled ? Color.FromArgb(21,21,23) : Color.FromArgb(207,207,212),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(textBounds, -1, -1), Color.Black, Color.Silver);
    }

    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

internal sealed class TrainerSwitch : CheckBox
{
    private float position;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    public TrainerSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MinimumSize = new Size(70, 30);
        animation.Tick += (_, _) => { float target = Checked ? 1 : 0; position += (target-position)*.35f; if (Math.Abs(target-position)<.02f) { position=target; animation.Stop(); } Invalidate(); };
    }
    public override Size GetPreferredSize(Size proposedSize) => new(TextRenderer.MeasureText(Text, Font).Width + (int)Math.Round(69*Font.Size/18f), Math.Max((int)Math.Round(36*Font.Size/18f), Font.Height + 10));
    protected override void OnCheckedChanged(EventArgs e) { if (IsHandleCreated) animation.Start(); else position = Checked ? 1 : 0; base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? TrainerTheme.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int labelWidth = TextRenderer.MeasureText(Text,Font).Width;
        float scale = Font.Size / 18f;
        var track = new RectangleF(labelWidth + 9*scale, (Height-28*scale)/2f, 55*scale, 28*scale);
        using var path = TrainerTheme.Outline(track);
        using var brush = new LinearGradientBrush(track, Checked && Enabled ? TrainerTheme.Red : Color.FromArgb(8,8,10), Color.FromArgb(42,42,46), 90);
        e.Graphics.FillPath(brush,path);
        using var edge = new Pen(Checked && Enabled ? Color.FromArgb(184,101,110) : Color.FromArgb(108,108,114)); e.Graphics.DrawPath(edge,path);
        var knob = new RectangleF(track.X+(3+position*26)*scale,track.Y+3*scale,23*scale,22*scale);
        using var steel = new LinearGradientBrush(knob,Color.White,Color.Gray,75);
        steel.InterpolationColors = new ColorBlend { Positions=[0,.45f,.5f,1], Colors=[Color.FromArgb(233,233,236),Color.FromArgb(159,159,162),Color.FromArgb(70,70,76),Color.FromArgb(196,196,199)] };
        using var knobPath = TrainerTheme.Outline(knob); e.Graphics.FillPath(steel,knobPath);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(0,0,labelWidth,Height),Enabled ? TrainerTheme.Ink : TrainerTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle,TrainerTheme.Ink,TrainerTheme.Surface);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

internal sealed class SwordHeader : TableLayoutPanel
{
    public Image? WolfHead { get; set; }
    public SwordHeader() { DoubleBuffered = true; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var gradient = new LinearGradientBrush(ClientRectangle,Color.FromArgb(73,73,78),TrainerTheme.Surface,75);
        e.Graphics.FillRectangle(gradient,ClientRectangle);
        float scale = Font.Size / 18f;
        e.Graphics.ScaleTransform(scale,scale);
        float width = Width/scale;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var ridge = new GraphicsPath();
        ridge.AddBezier(180,56,width*.27f,72,width*.40f,27,width*.58f,44);
        ridge.AddBezier(width*.58f,44,width*.77f,63,width*.86f,68,width-8,32);
        using var shadow = new Pen(Color.FromArgb(7,7,9),21); e.Graphics.DrawPath(shadow,ridge);
        using var steel = new LinearGradientBrush(new RectangleF(0,25,width,50),Color.White,Color.Black,90);
        steel.InterpolationColors = new ColorBlend { Positions=[0,.44f,.48f,.54f,1], Colors=[Color.FromArgb(36,36,39),Color.FromArgb(159,159,163),Color.FromArgb(226,226,229),Color.FromArgb(54,54,58),Color.FromArgb(22,22,25)] };
        using var spine = new Pen(steel,15); e.Graphics.DrawPath(spine,ridge);
        using var highlight = new Pen(Color.FromArgb(170,203,203,206),1); e.Graphics.DrawPath(highlight,ridge);
        if (WolfHead is not null) e.Graphics.DrawImage(WolfHead, new RectangleF(0,-Top/scale-12,160,160));
    }
}

public sealed class TrainerTabs : Panel
{
    public List<Panel> TabPages { get; } = [];
    private int selectedIndex;
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < 0 || value >= TabPages.Count) return;
            selectedIndex = value;
            for (int index=0; index<TabPages.Count; index++) TabPages[index].Visible = index == value;
            TabPages[value].BringToFront();
            SelectedIndexChanged?.Invoke(this,EventArgs.Empty);
        }
    }
    public Panel SelectedTab { get => TabPages[selectedIndex]; set => SelectedIndex = TabPages.IndexOf(value); }
    public void AddPage(TabPage source)
    {
        var page = new Panel { Text = source.Text, Dock = DockStyle.Fill, Visible = TabPages.Count == 0 };
        foreach (Control child in source.Controls.Cast<Control>().ToArray()) page.Controls.Add(child);
        source.Dispose();
        TabPages.Add(page); Controls.Add(page);
    }
}

internal sealed class WindowButton : Button
{
    public int ActionKind { get; init; }
    private bool hover;
    public WindowButton()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        FlatStyle=FlatStyle.Flat; Size=new Size(30,24); TabStop=true;
    }
    protected override void OnMouseEnter(EventArgs e) { hover=true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(hover ? (ActionKind==2 ? TrainerTheme.Red : Color.FromArgb(65,65,70)) : TrainerTheme.Surface);
        using var pen=new Pen(TrainerTheme.Ink,1.4f);
        if(ActionKind==0) e.Graphics.DrawLine(pen,10,16,20,16);
        if(ActionKind==1) e.Graphics.DrawRectangle(pen,10,7,10,9);
        if(ActionKind==2) { e.Graphics.DrawLine(pen,10,7,20,17); e.Graphics.DrawLine(pen,20,7,10,17); }
        if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
}
