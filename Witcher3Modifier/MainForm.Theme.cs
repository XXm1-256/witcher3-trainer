namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly bool offlinePreview;
    private Image? wolfImage;
    private FlowLayoutPanel? windowActions;
    private float contentScale = 1;
    private bool scalingContent;
    private readonly Dictionary<Control, Font> fixedFonts = [];
    private readonly List<Font> scaledFonts = [];

    private void InitializeContentScaling()
    {
        RememberFonts(this);
        FormClosed += (_, _) => { foreach (var font in scaledFonts) font.Dispose(); };
        UpdateContentScale();

        void RememberFonts(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Font.Equals(parent.Font)) fixedFonts[child] = child.Font;
                RememberFonts(child);
            }
        }
    }

    private void UpdateContentScale()
    {
        if (scalingContent || fixedFonts.Count == 0 || WindowState == FormWindowState.Minimized) return;
        float target = WindowState == FormWindowState.Maximized
            ? Math.Max(1, Math.Min(ClientSize.Width / 1715f, ClientSize.Height / 1131f)) : 1;
        if (Math.Abs(target - contentScale) < .005f) return;
        scalingContent = true;
        SuspendLayout();
        try
        {
            float ratio = target / contentScale;
            foreach (Control child in Controls) child.Scale(new SizeF(ratio, ratio));
            Padding = new Padding((int)Math.Round(8*target),(int)Math.Round(40*target),(int)Math.Round(8*target),(int)Math.Round(5*target));
            var bodyFont = new Font("Microsoft YaHei UI",18*target,FontStyle.Regular,GraphicsUnit.Pixel);
            Font = bodyFont;
            var previousFonts = scaledFonts.ToArray();
            scaledFonts.Clear(); scaledFonts.Add(bodyFont);
            foreach (var (control, original) in fixedFonts)
            {
                var font = new Font(original.FontFamily,original.Size*target,original.Style,original.Unit);
                control.Font = font; scaledFonts.Add(font);
            }
            foreach (var font in previousFonts) font.Dispose();
            contentScale = target;
            windowActions!.Left = ClientSize.Width-windowActions.Width-(int)Math.Round(10*target);
            foreach (var combo in Descendants(this).OfType<ComboBox>()) combo.ItemHeight=(int)Math.Round(32*target);
        }
        finally { ResumeLayout(true); scalingContent = false; }
        Invalidate(true);

        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(TrainerTheme.Surface);
        if (ClientSize.Width < 20 || ClientSize.Height < 55) return;
        e.Graphics.ScaleTransform(contentScale,contentScale);
        float width=ClientSize.Width/contentScale, height=ClientSize.Height/contentScale;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var outline = TrainerTheme.Outline(new RectangleF(1,39,width-3,height-41),26);
        using var metal = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(0,39,width,height-39),Color.FromArgb(105,105,112),Color.FromArgb(35,35,40),65);
        e.Graphics.FillPath(metal,outline);
        using var inset = TrainerTheme.Outline(new RectangleF(7,45,width-15,height-53),21);
        using var inner = new SolidBrush(TrainerTheme.Surface);
        e.Graphics.FillPath(inner,inset);
        using var edge = new Pen(Color.FromArgb(111,111,118));
        e.Graphics.DrawPath(edge,outline);
        if (wolfImage is not null) e.Graphics.DrawImage(wolfImage,new Rectangle(8,-12,160,160));
    }

    private Control BuildNavigation(TrainerTabs tabs)
    {
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 48, Padding = new Padding(180, 5, 0, 0), WrapContents = false };
        var buttons = new List<MetalButton>();
        foreach (Panel page in tabs.TabPages)
        {
            var button = new MetalButton { Text = page.Text, Width = 140, Height = 36, Navigation = true, Active = buttons.Count == 0 };
            button.Click += (_, _) => tabs.SelectedTab = page;
            buttons.Add(button);
            navigation.Controls.Add(button);
        }
        tabs.SelectedIndexChanged += (_, _) =>
        {
            for (int index = 0; index < buttons.Count; index++) { buttons[index].Active = index == tabs.SelectedIndex; buttons[index].Invalidate(); }
        };
        return navigation;
    }

    private void AddWolfHead(SwordHeader header)
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Witcher3Modifier.WolfHead.png")!;
        using var original = Image.FromStream(stream);
        var image = new Bitmap(original);
        wolfImage = image;
        header.WolfHead = image;
        FormClosed += (_, _) => image.Dispose();
    }

    private void InitializeWindowChrome(SwordHeader header)
    {
        windowActions = new FlowLayoutPanel { Size = new Size(96,28), Location = new Point(ClientSize.Width-106,6), Anchor = AnchorStyles.Top|AnchorStyles.Right, WrapContents = false };
        string[] names = ["最小化","最大化或还原","关闭"];
        for (int index=0;index<3;index++)
        {
            int action=index;
            var button=new WindowButton { ActionKind=index, AccessibleName=names[index], Margin=Padding.Empty };
            button.Click += (_,_) =>
            {
                if(action==0) WindowState=FormWindowState.Minimized;
                else if(action==1) { MaximizedBounds=Screen.FromControl(this).WorkingArea; WindowState=WindowState==FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
                else Close();
            };
            windowActions.Controls.Add(button);
        }
        Controls.Add(windowActions); windowActions.BringToFront();
        MouseDown += DragWindow;
        header.MouseDown += DragWindow;
        foreach(Control child in header.Controls) if(child is Label) child.MouseDown += DragWindow;
        UpdateWindowShape();
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if(e.Button!=MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle,0xA1,2,0);
    }

    private bool HandleWindowResize(ref Message message)
    {
        if(message.Msg!=0x84 || WindowState!=FormWindowState.Normal) return false;
        long coordinates=message.LParam.ToInt64();
        Point point=PointToClient(new Point((short)(coordinates&0xffff),(short)((coordinates>>16)&0xffff)));
        bool left=point.X<7, right=point.X>=ClientSize.Width-7, top=point.Y<7, bottom=point.Y>=ClientSize.Height-7;
        int hit=top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
        if(hit==0) return false;
        message.Result=hit; return true;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if(windowActions is not null) { UpdateContentScale(); UpdateWindowShape(); }
    }

    private void UpdateWindowShape()
    {
        if(ClientSize.Width<30 || ClientSize.Height<45) return;
        using var outline=TrainerTheme.Outline(new RectangleF(0,0,ClientSize.Width,ClientSize.Height),26);
        var previous=Region; Region=new Region(outline); previous?.Dispose();
        Invalidate();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint SendMessage(nint window,uint message,nint wParam,nint lParam);
}
