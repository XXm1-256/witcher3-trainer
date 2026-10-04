using System.Runtime.InteropServices;

namespace Witcher3Modifier;

public sealed partial class MainForm : Form
{
    private bool teleportBusy;
    private bool closingWithoutGame;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var games = System.Diagnostics.Process.GetProcessesByName("witcher3");
        try { closingWithoutGame = games.Length == 0; }
        finally { foreach (var game in games) game.Dispose(); }
        base.OnFormClosing(e);
    }

    private readonly ToolTip usageTips = new() { ShowAlways = true, AutoPopDelay = 15000 };
    private readonly CheckBox saveSettingsToggle = new TrainerSwitch() { Text = "保存开关与倍率", AutoSize = true, Anchor = AnchorStyles.Right, Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold, GraphicsUnit.Pixel) };
    private readonly Draft draft;
    private readonly ToolStripStatusLabel status = new();
    private readonly TextBox itemSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索中文或英文物品名" };
    private readonly CheckBox fuzzySearch = new() { Text = "模糊搜索（文字间隔／中文错一字）", AutoSize = true };
    private readonly ListBox itemCatalog = new() { Dock = DockStyle.Fill };
    private readonly IReadOnlyList<GameItem> availableItems = GameItem.LoadCatalog();
    private readonly Label selectedItem = new() { Text = "尚未选择物品", AutoSize = true };
    private readonly NumericUpDown itemQuantity = new() { Minimum = 1, Maximum = 9999, Value = 1, Dock = DockStyle.Fill };
    private readonly Label itemAddNotice = new() { Text = "添加后请返回游戏并关闭暂停菜单；上一笔完成后可继续添加。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button giveItem = new MetalButton() { Text = "添加清单全部物品", AutoSize = true };
    private readonly ComboBox gearType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox gearBase = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 650 };
    private readonly TextBox gearSearch = new() { PlaceholderText = "搜索中文名称或内部名称", Dock = DockStyle.Fill };
    private readonly TextBox gearName = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly NumericUpDown runeSlots = new() { Minimum = 0, Maximum = 3, Dock = DockStyle.Fill };
    private readonly CheckedListBox affixes = new() { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly ComboBox enchantment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button generateGear = new MetalButton() { Text = "生成装备到背包", AutoSize = true };
    private readonly TextBox gearPreview = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly ListBox pendingGear = new() { Dock = DockStyle.Fill };
    private readonly CheckBox setMoney = new() { Text = "设置金钱", AutoSize = true };
    private readonly NumericUpDown money = new() { Minimum = 0, Maximum = GameMoney.MaxMoney, Width = 160 };
    private readonly Label currentMoney = new() { Text = "当前金钱：读取中", AutoSize = true };
    private readonly CheckBox setSkills = new() { Text = "设置技能点", AutoSize = true };
    private readonly NumericUpDown skills = new() { Minimum = 0, Maximum = 99999, Width = 160 };
    private readonly NumericUpDown xp = new() { Minimum = 1, Maximum = 1000, DecimalPlaces = 1, Increment = 0.1M, Width = 160 };
    private readonly NumericUpDown goldMultiplier = new() { Minimum = 1, Maximum = 1000, DecimalPlaces = 1, Increment = 0.1M, Value = 1, Width = 160 };
    private readonly CheckBox goldMultiplierToggle = new TrainerSwitch() { Text = "金币收入倍率 ×", AutoSize = true };
    private readonly CheckBox xpMultiplierToggle = new TrainerSwitch() { Text = "经验倍率 ×", AutoSize = true };
    private bool refreshingExperience;
    private CheckBox? weightToggle;
    private CheckBox? keepItemsToggle;
    private bool refreshingWeight;
    private bool refreshingInventoryHooks;
    private int settingsProcessId;
    private readonly System.Windows.Forms.Timer inventoryBinding = new() { Interval = 5000 };

    private static readonly (string Key, string Text)[] Toggles = [
        ("unlimitedHealth", "无限生命"),
        ("unlimitedStamina", "无限活力"),
        ("unlimitedBreath", "无限呼吸"),
        ("noToxicity", "无毒性"),
        ("horseStamina", "马匹无限耐力"),
        ("horseFear", "马匹无所畏惧"),
        ("freeCrafting", "制作无需材料"),
        ("infiniteDurability", "装备无限耐久")
    ];

    public MainForm(bool offlinePreview = false)
    {
        this.offlinePreview = offlinePreview;
        Text = "巫师 3 修改器";
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(8, 40, 8, 5);
        MinimumSize = new Size(700, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Regular, GraphicsUnit.Pixel);
        usageTips.OwnerDraw=true;
        usageTips.Popup+=(_,e)=>
        {
            int width=Math.Min(520*DeviceDpi/96,Screen.FromControl(e.AssociatedControl??this).WorkingArea.Width-24);
            e.ToolTipSize=MeasureUsageTip(usageTips.GetToolTip(e.AssociatedControl)??"",Font,width);
        };
        usageTips.Draw+=(_,e)=>
        {
            e.DrawBackground();e.DrawBorder();
            TextRenderer.DrawText(e.Graphics,e.ToolTipText,Font,Rectangle.Inflate(e.Bounds,-8,-8),usageTips.ForeColor,
                TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        };
        try { draft = DraftStore.Load(); }
        catch { draft = new Draft(); status.Text = "草案读取失败，已打开空白草案"; }

        RestoreWindowBounds();
        var tabs = new TrainerTabs { Dock = DockStyle.Fill };
        tabs.AddPage(BuildHome());
        tabs.AddPage(BuildItems());
        tabs.AddPage(BuildGear());
        tabs.AddPage(BuildInventory());
        tabs.AddPage(BuildFun());
        var header = new SwordHeader { Dock = DockStyle.Top, Height = 112, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 0, 15, 0) };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "巫师 3 修改器", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold, GraphicsUnit.Pixel), Margin = new Padding(190, 0, 0, 6), BackColor = Color.Transparent }, 0, 0);
        saveSettingsToggle.Checked = draft.SaveSwitchesAndMultipliers;
        header.Controls.Add(saveSettingsToggle, 1, 0);
        hotkeysToggle.Checked=draft.HotkeysEnabled;
        header.Controls.Add(hotkeysToggle,2,0);
        usageTips.SetToolTip(hotkeysToggle,"快捷键仅在游戏窗口内生效。使用游戏控制台或文字输入时，请关闭此开关。");
        hotkeysToggle.CheckedChanged+=(_,_)=>
        {
            draft.HotkeysEnabled=hotkeysToggle.Checked;
            lastHotkeyForeground=-1;RefreshHotkeyAvailability();
            if(!offlinePreview) ToggleSounds.Play(hotkeysToggle.Checked);
            SaveDraft();
            status.Text=hotkeysToggle.Checked?"快捷键已开启，仅在游戏前台生效":"快捷键已关闭，小键盘可正常输入";
        };
        hotkeyFocusTimer.Tick+=(_,_)=>RefreshHotkeyAvailability();
        usageTips.SetToolTip(saveSettingsToggle, "记住功能开关和倍率，下次打开自动恢复。关闭后停止保存和恢复，当前效果继续生效。");
        saveSettingsToggle.CheckedChanged += (_, _) =>
        {
            draft.SaveSwitchesAndMultipliers = saveSettingsToggle.Checked;
            if (saveSettingsToggle.Checked) CaptureRuntimeSettings();
            if (SaveDraft() && !offlinePreview) ToggleSounds.Play(saveSettingsToggle.Checked);
            status.Text = saveSettingsToggle.Checked ? "保存开关与倍率已开启" : "保存开关与倍率已关闭，当前游戏效果保持不变";
        };
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(status);
        Controls.Add(tabs);
        var navigation = BuildNavigation(tabs);
        header.Controls.Add(navigation, 0, 1);
        header.SetColumnSpan(navigation, 3);
        Controls.Add(header);
        Controls.Add(statusStrip);
        TrainerTheme.Apply(this);
        navigation.BackColor = Color.Transparent;
        foreach (Control child in header.Controls) if (child is Label or TrainerSwitch) child.BackColor = Color.Transparent;
        AddWolfHead(header);
        InitializeWindowChrome(header);
        InitializeContentScaling();
        status.Text = string.IsNullOrEmpty(status.Text) ? "已载入游戏物品目录" : status.Text;
        RefreshAll();
        if(!offlinePreview)
        {
            ToggleSounds.Preload();
        }
        shortcutsReady=true;
        Shown += (_, _) => { if (offlinePreview) return; lastHotkeyForeground=-1;RefreshHotkeyAvailability();hotkeyFocusTimer.Start(); RestoreRuntimeSettings(); RefreshGameMoney(); RefreshGameWeight(); RefreshEquipmentLevel(); RefreshInventoryHooks(); RefreshExperience(); RefreshDebugFacts(); inventoryBinding.Start(); };
        Activated += (_, _) => { if (offlinePreview) return; RestoreRuntimeSettings(); RefreshGameWeight(); RefreshEquipmentLevel(); RefreshInventoryHooks(); RefreshExperience(); RefreshDebugFacts(); };
        inventoryBinding.Tick += (_, _) => { _ = RefreshPlayerMotion(false); RestoreRuntimeSettings(); try { GameInventoryHooks.Rebind(); GameItemScheduler.RebindEquipmentLevelBypass(); GameItemScheduler.RebindAuxiliary(); } catch { /* The game may be loading or closed. */ } };
        FormClosing += (_, _) => SaveWindowBounds();
        Shown += (_,_) => RefreshReferenceLevels();
        Activated += (_,_) => RefreshReferenceLevels();
        FormClosed += (_, _) => { inventoryBinding.Dispose();hotkeyFocusTimer.Dispose(); usageTips.Dispose(); };
        AttachUsageTipPositions(this);
    }

    private void AttachUsageTipPositions(Control parent)
    {
        foreach(Control control in parent.Controls)
        {
            if(!string.IsNullOrEmpty(usageTips.GetToolTip(control)))
            {
                control.MouseEnter+=(_,_)=>
                {
                    string text=usageTips.GetToolTip(control)??"";
                    Control anchor=control is HelpIcon && control.Parent is FlowLayoutPanel ? control.Parent : control;
                    Rectangle area=Screen.FromControl(control).WorkingArea;
                    Size size=MeasureUsageTip(text,Font,Math.Min(520*DeviceDpi/96,area.Width-24));
                    Point point=UsageTipPosition(anchor.RectangleToScreen(anchor.ClientRectangle),size,area);
                    usageTips.Show(text,control,control.PointToClient(point),15000);
                };
                control.MouseLeave+=(_,_)=>usageTips.Hide(control);
            }
            AttachUsageTipPositions(control);
        }
    }

    private static Point UsageTipPosition(Rectangle anchor,Size size,Rectangle area)
    {
        int x=Math.Clamp(anchor.Left,area.Left,Math.Max(area.Left,area.Right-size.Width));
        int y=anchor.Bottom+6;
        if(y+size.Height>area.Bottom) y=Math.Max(area.Top,anchor.Top-size.Height-6);
        return new Point(x,y);
    }

    private static Size MeasureUsageTip(string text,Font font,int maximumWidth)
    {
        Size measured=TextRenderer.MeasureText(text,font,new Size(Math.Max(1,maximumWidth-16),int.MaxValue),
            TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        return new Size(measured.Width+16,measured.Height+16);
    }

    private void PlaySuccess(bool confirmed = true)
    {
        if(confirmed && !offlinePreview) ToggleSounds.PlayAction();
    }

    private TabPage BuildItems()
    {
        var page = new TabPage("添加物品");
        var columns = Grid(2);
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var left = Grid(1);
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(Info("游戏物品目录", $"已收录 {availableItems.Count} 个物品；可搜索中文或英文名称。"), 0, 0);
        left.Controls.Add(itemSearch, 0, 1);
        left.Controls.Add(fuzzySearch, 0, 2);
        left.RowStyles.Insert(3,new RowStyle(SizeType.Absolute,40));
        left.Controls.Add(Labeled("参考等级",itemLevel), 0, 3);
        left.Controls.Add(itemCatalog, 0, 4);
        columns.Controls.Add(left, 0, 0);

        var right = BuildItemBatchPanel();
        usageTips.SetToolTip(fuzzySearch, "允许名称中间漏字；三个字以上的中文名称允许错一个字。准确匹配优先显示。");
        columns.Controls.Add(right, 1, 0);
        page.Controls.Add(columns);

        itemSearch.TextChanged += (_, _) => RefreshCatalog();
        fuzzySearch.CheckedChanged += (_, _) => RefreshCatalog();
        itemLevel.SelectedIndexChanged += (_, _) => RefreshCatalog();
        usageTips.SetToolTip(itemLevel,"按参考需要等级筛选，可同时搜索名称。随机属性和二周目可能改变实际等级。");
        fuzzySearch.Click += (_, _) => {if(!offlinePreview) ToggleSounds.Play(fuzzySearch.Checked);};
        itemCatalog.SelectedIndexChanged += (_, _) =>
            selectedItem.Text = itemCatalog.SelectedItem is GameItem item ? $"已选择：{item.DisplayName}" : "尚未选择物品";
        return page;
    }

    private TabPage BuildGear()
    {
        var page = new TabPage("自定义装备");
        var root = Grid(1);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        top.RowCount = 1;
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        gearType.Items.AddRange(["钢剑", "银剑", "胸甲", "靴子", "裤子", "手套", "弩"]);
        gearType.SelectedIndex = 0;
        top.Controls.Add(gearType, 0, 0);
        top.Controls.Add(gearSearch, 1, 0);
        top.Controls.Add(gearBase, 2, 0);
        root.RowStyles[0].Height=88;
        top.RowCount=2;
        top.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        top.Controls.Add(new Label {Text="参考等级",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,1);
        top.Controls.Add(gearLevel,1,1);
        gearLevel.SelectedIndexChanged += (_, _) => RefreshGearBases();
        usageTips.SetToolTip(gearLevel,"按基础装备的参考需要等级筛选。修改属性后的预估等级见预览。");
        root.Controls.Add(top, 0, 0);
        var columns = new SplitContainer { Size = new Size(1000, 400), Dock = DockStyle.Fill, SplitterDistance = 380, Panel1MinSize = 260, Panel2MinSize = 260 };
        var left = Grid(1);
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 304));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.Controls.Add(new Label { Text = "基础名称与外观沿用所选装备", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        left.Controls.Add(Labeled("最低槽位", runeSlots), 0, 1);
        left.Controls.Add(BuildNumericFields(), 0, 2);
        left.Controls.Add(gearPreview, 0, 3);
        var configs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        configs.RowCount = 1;
        configs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        configs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        configs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        configs.Controls.Add(pendingGear, 0, 0);
        var remove = new MetalButton { Text = "删除配置", Dock = DockStyle.Fill, Margin = Padding.Empty };
        remove.Click += (_, _) => RemoveGear();
        configs.Controls.Add(remove, 1, 0);
        left.Controls.Add(configs, 0, 4);
        columns.Panel1.Controls.Add(left);
        var right = Grid(1);
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.Controls.Add(affixSearch, 0, 0);
        right.Controls.Add(affixes, 0, 1);
        right.Controls.Add(BuildAffixNumericEditor(affixes,numericFields,RefreshGearPreview),0,2);
        var enchantRow = Grid(2);
        enchantRow.Padding = Padding.Empty;
        enchantRow.Margin = Padding.Empty;
        enchantRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        enchantRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        enchantRow.Controls.Add(enchantSearch, 0, 0);
        enchantRow.Controls.Add(Labeled("附魔", enchantment), 1, 0);
        right.Controls.Add(enchantRow, 0, 3);
        columns.Panel2.Controls.Add(right);
        root.Controls.Add(columns, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        generateGear.Click += async (_, _) => await GenerateGear();
        actions.Controls.Add(generateGear);
        var save = new MetalButton { Text = "保存装备配置", AutoSize = true };
        save.Click += (_, _) => AddGear();
        actions.Controls.Add(save);
        root.Controls.Add(actions, 0, 2);
        page.Controls.Add(root);
        gearType.SelectedIndexChanged += (_, _) => RefreshGearBases();
        gearSearch.TextChanged += (_, _) => RefreshGearBases();
        enchantSearch.TextChanged += (_, _) => RefreshGearBases();
        gearBase.SelectedIndexChanged += (_, _) =>
        {
            gearName.Text = (gearBase.SelectedItem as GameItem)?.DisplayName ?? "";
            RefreshGearPreview();
        };
        affixSearch.TextChanged += (_, _) => FilterAffixes();
        affixes.ItemCheck += (_, e) =>
        {
            if (filteringAffixes) return;
            var effect=(EquipmentEffect)affixes.Items[e.Index];
            if(e.NewValue==CheckState.Checked && !effect.AppliesTo(CurrentGearCategory()))
            {
                e.NewValue=e.CurrentValue;
                status.Text=$"{effect.DisplayName}仅适用于{effect.TypeHint}，当前装备不适用。";
                return;
            }
            string id = effect.Id;
            if (e.NewValue == CheckState.Checked) selectedAffixes.Add(id); else selectedAffixes.Remove(id);
            if (IsHandleCreated) BeginInvoke(new Action(RefreshGearPreview));
        };
        FilterAffixes();
        RefreshGearBases();
        runeSlots.ValueChanged += (_, _) => RefreshGearPreview();
        enchantment.SelectedIndexChanged += (_, _) => RefreshGearPreview();
        return page;
    }

    private void InitializeValues()
    {
        setMoney.Checked = draft.Money.HasValue;
        money.Value = Math.Clamp(draft.Money ?? 0, 0, GameMoney.MaxMoney);
        setSkills.Checked = draft.SkillPoints.HasValue;
        skills.Value = Math.Clamp(draft.SkillPoints ?? 0, 0, 99999);
        xp.Value = Math.Clamp(draft.XpMultiplier, 1, 1000);
        goldMultiplier.Value = Math.Clamp(draft.GoldMultiplier, 1, 1000);
        goldMultiplierToggle.Checked = draft.GoldMultiplierEnabled;
        xpMultiplierToggle.Checked = draft.XpMultiplierEnabled;
        void SaveValues(object? _, EventArgs __)
        {
            draft.Money = setMoney.Checked ? (int)money.Value : null;
            draft.SkillPoints = setSkills.Checked ? (int)skills.Value : null;
            if (draft.SaveSwitchesAndMultipliers) draft.XpMultiplier = xp.Value;
            SaveDraft();
        }
        setMoney.CheckedChanged += SaveValues;
        money.ValueChanged += SaveValues;
        setSkills.CheckedChanged += SaveValues;
        skills.ValueChanged += SaveValues;
        xp.ValueChanged += SaveValues;
        goldMultiplierToggle.CheckedChanged += (_, _) => ApplyGoldMultiplier(true);
        xpMultiplierToggle.CheckedChanged += (_, _) => ApplyExperience(true);
        goldMultiplier.ValueChanged += (_, _) =>
        {
            if (refreshingInventoryHooks) return;
            if (draft.SaveSwitchesAndMultipliers) { draft.GoldMultiplier = goldMultiplier.Value; SaveDraft(); }
            if (goldMultiplierToggle.Checked) ApplyGoldMultiplier(true);
        };
        xp.ValueChanged += (_, _) => { if (xpMultiplierToggle.Checked) ApplyExperience(true); };
    }

    private TabPage BuildHome()
    {
        var page = new TabPage("常用功能");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(12, 4, 12, 4), MinimumSize = new Size(700, 0) };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var homeRows = new List<FlowLayoutPanel>();
        bool arrangingRows = false;
        scroll.SizeChanged += (_, _) => ArrangeRows();
        rows.Layout += (_, _) => ArrangeRows();
        scroll.Controls.Add(rows);
        page.Controls.Add(scroll);
        InitializeValues();
        var amount = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        currentMoney.AutoSize = false;
        currentMoney.Size = new Size(185, 36);
        currentMoney.TextAlign = ContentAlignment.MiddleLeft;
        money.Width = 95;
        amount.Controls.Add(currentMoney);
        amount.Controls.Add(money);
        var apply = new MetalButton { Text = "设置金钱", AutoSize = true };
        apply.Click += (_, _) => ApplyGameMoney();
        amount.Controls.Add(apply);
        var read = new MetalButton { Text = "读取", AutoSize = true };
        read.Click += (_, _) => RefreshGameMoney(true);
        amount.Controls.Add(read);
        Row(amount, "输入目标金额后设置；读取按钮更新当前金币。");
        goldMultiplier.Width = xp.Width = 85;
        Row(ValueRow(goldMultiplierToggle, goldMultiplier), "正向金币收入按倍率计算，支出照常；关闭恢复1倍。");
        Row(ValueRow(xpMultiplierToggle, xp), "正向经验按倍率计算，支持小数；关闭恢复1倍。");
        foreach (var (key, label) in Toggles)
        {
            bool supported=AuxiliaryFeatureKeys.Contains(key);
            var box = new TrainerSwitch { Text = label, AutoSize = true, Enabled = supported };
            if(supported) AttachDebugFact(box,key);
            string help=key switch
            {
                "unlimitedHealth" => "阻止玩家常规伤害与生命扣减；开启不会补满已损失生命。",
                "unlimitedBreath" => "阻止玩家呼吸值消耗；开启前建议先浮出水面恢复呼吸。",
                "infiniteDurability" => "阻止玩家装备耐久扣减；已有磨损可用一键恢复耐久修复。",
                "noToxicity" => "清除并阻止药水、煎药毒性。剧情中可能无法开启，请在角色可移动时使用。",
                "unlimitedStamina" => "角色活力不消耗；关闭后恢复正常活力消耗。",
                "horseFear" => "骑乘时马匹不受惊。",
                "freeCrafting" => "工匠制作及炼金无需材料；开启后重新选择配方，工匠条件和金币费用照常。",
                _ => supported ? "骑乘时马匹耐力不消耗。" : "此功能暂不可用。"
            };
            Row(WithFeatureShortcut(box,key),help);
        }
        keepItemsToggle = new TrainerSwitch { Text = "非金币物品数量不减", AutoSize = true, Checked = draft.KeepItems };
        keepItemsToggle.CheckedChanged += (_, _) => ApplyKeepItems();
        Row(WithFeatureShortcut(keepItemsToggle, "keepItems"), "物品数量及药水、炸弹使用次数不减少。金币照常消耗，仍可主动删除物品。");
        var oneHitKill=new TrainerSwitch {Text="一击必杀",AutoSize=true};
        AttachDebugFact(oneHitKill,"oneHitKill");
        Row(WithFeatureShortcut(oneHitKill,"oneHitKill"),"强化玩家造成的伤害；剧情保护和伤害免疫仍由游戏判定。");
        weightToggle = new TrainerSwitch { Text = "无视负重", AutoSize = true, Checked = draft.Toggles.GetValueOrDefault("ignoreWeight") };
        weightToggle.CheckedChanged += (_, _) => ApplyGameWeight();
        equipmentLevelToggle=new TrainerSwitch {Text="无视装备穿戴等级",AutoSize=true,Checked=draft.Toggles.GetValueOrDefault("ignoreEquipmentLevel")};
        equipmentLevelToggle.CheckedChanged+=(_,_)=>ApplyEquipmentLevel();
        var teleport = new MetalButton { Text = "传送到地图标记", Width = 245 };
        teleport.Click += async (_, _) => await TeleportToMapPin();
        Row(WithFeatureShortcut(teleport, "teleport"), "传送到当前区域的地图标记。关闭地图和暂停菜单后生效；多层建筑无法指定楼层。落点可能在空中，建议开启“无落地伤害”。若停在空中或角色不显示，换一个标记再传送即可。");
        Row(WithFeatureShortcut(equipmentLevelToggle,"ignoreEquipmentLevel"),"允许装备高于角色等级的装备；装备提示的需要等级保持原值。");
        Row(WithFeatureShortcut(weightToggle,"ignoreWeight"), "解除负重限制；开启后游戏负重显示可能隐藏。");
        var keepAmmo=new TrainerSwitch {Text="弹药不减",AutoSize=true};
        AttachDebugFact(keepAmmo,"keepAmmo");
        Row(WithFeatureShortcut(keepAmmo,"keepAmmo"),"射击时弩箭不消耗；出售、丢弃和主动删除仍按物品不减开关处理。");
        var repair = new MetalButton { Text = "恢复装备耐久", Width = 245 };
        repair.Click += (_, _) => RepairEquipment();
        Row(WithFeatureShortcut(repair,"repairEquipment"), "单次修复当前装备及背包内可修复装备。");
        var noFallDamage=new TrainerSwitch {Text="无落地伤害",AutoSize=true};
        AttachDebugFact(noFallDamage,"noFallDamage");
        Row(WithFeatureShortcut(noFallDamage,"noFallDamage"),"免受坠落伤害，剧情死亡区域仍会致死。此效果可能随游戏存档保留，关闭可恢复正常。");
        var gwentWin = new MetalButton { Text = "昆特牌立即获胜", Width = 245 };
        gwentWin.Click += async (_, _) => await WinGwent();
        Row(WithFeatureShortcut(gwentWin,"gwentWin"),"在普通昆特牌对局的出牌界面使用，立即获胜。请返回游戏等待结算。");
        var resetBuild = new MetalButton { Text = "一键洗点", Width = 245 };
        resetBuild.Click += async (_, _) => await ResetBuild();
        Row(WithFeatureShortcut(resetBuild,"resetBuild"),"重置普通技能，返还技能点，并卸下技能与突变诱发物。保留DLC突变研究。请先保存，返回游戏等待完成后重新打开角色页。");
        var adrenaline=new TrainerSwitch {Text="无限肾上腺素",AutoSize=true};
        AttachDebugFact(adrenaline,"unlimitedAdrenaline");
        Row(WithFeatureShortcut(adrenaline,"unlimitedAdrenaline"),"补满肾上腺素并阻止消耗。关闭后恢复正常消耗。请在角色可移动时使用。");
        Row(BuildEnemySpeed(),"调整附近80米内敌人的移动与战斗动画速度，范围0.1～10倍。弩箭等投射物速度不受影响。请返回游戏等待生效；关闭可恢复。");
        Row(BuildCatVision(),"获得猫药水式夜视，无需使用药水。剧情或药水可能覆盖效果；请在角色可移动时开启。");
        Row(BuildPlayerMotion("moveSpeed"),"调整步行、奔跑和冲刺速度，范围0.5～10倍。攻击速度保持正常；剧情可能限制移动。");
        Row(BuildPlayerMotion("jumpHeight"),"调整普通跳跃高度，范围0.5～10倍。高跳建议配合“无落地伤害”；攀爬和剧情跳跃可能不受影响。");
        Row(BuildSwimSpeed(),"加快游泳动作，范围0.5～10倍。实际游泳速度受碰撞和动作影响，离开水面后恢复。");
        Row(BuildAutoLoot(),"自动拾取五米内箱子、袋子和尸体中的物品，并采集草药。暂停或读档时等待，返回游玩场景后自动继续。跳过上锁、任务、特殊交互及会被判定为偷窃的容器。");
        ArrangeRows();
        return page;

        void ArrangeRows()
        {
            if (arrangingRows || homeRows.Count < 4) return;
            int widest = homeRows.Skip(3).Max(control => control.GetPreferredSize(Size.Empty).Width);
            int columns = scroll.ClientSize.Width >= widest * 2 + rows.Padding.Horizontal + 32 ? 2 : 1;
            if (rows.ColumnCount == columns && rows.RowCount == 3 + (homeRows.Count - 3 + columns - 1) / columns) return;
            arrangingRows = true;
            rows.SuspendLayout();
            try
            {
                rows.ColumnCount = columns;
                rows.ColumnStyles.Clear();
                for (int column = 0; column < columns; column++)
                    rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                rows.RowCount = 3 + (homeRows.Count - 3 + columns - 1) / columns;
                rows.RowStyles.Clear();
                for (int row = 0; row < rows.RowCount; row++)
                    rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 52 * contentScale));
                for (int index = 0; index < homeRows.Count; index++)
                {
                    var control = homeRows[index];
                    rows.SetColumnSpan(control, 1);
                    rows.SetCellPosition(control, index < 3 ? new TableLayoutPanelCellPosition(0, index)
                        : new TableLayoutPanelCellPosition((index - 3) % columns, 3 + (index - 3) / columns));
                    if (index < 3) rows.SetColumnSpan(control, columns);
                }
            }
            finally { rows.ResumeLayout(true); arrangingRows = false; }
        }

        void Row(Control action, string description)
        {
            int row = rows.RowCount++;
            rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            var inline = action as FlowLayoutPanel ?? new FlowLayoutPanel { WrapContents = false };
            if (inline != action) inline.Controls.Add(action);
            inline.Dock = DockStyle.None;
            inline.AutoSize = true;
            inline.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            inline.Anchor = AnchorStyles.Left;
            inline.Margin = new Padding(3, 2, 3, 2);
            var help = new HelpIcon { AccessibleName = "功能说明", AccessibleDescription = description, Margin = new Padding(8, 5, 0, 0) };
            // Keep the help next to the action, before its shortcut label.
            int helpIndex = inline.Controls.Count;
            if (helpIndex > 1 && inline.Controls[helpIndex - 1] is Label) helpIndex--;
            inline.Controls.Add(help);
            inline.Controls.SetChildIndex(help, helpIndex);
            rows.Controls.Add(inline, 0, row);
            homeRows.Add(inline);
            usageTips.SetToolTip(help, description);
        }
    }

    private static Control WithShortcut(Control action, string shortcut)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        row.Controls.Add(action);
        row.Controls.Add(new Label { Text = shortcut, AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        return row;
    }

    private void RefreshGameMoney(bool sound = false)
    {
        try
        {
            int actual = GameMoney.Read();
            currentMoney.Text = $"当前金钱：{actual}";
            if (!setMoney.Checked) money.Value = Math.Clamp(actual, 0, GameMoney.MaxMoney);
            status.Text = "已连接游戏并读取金钱";
            PlaySuccess(sound);
        }
        catch (Exception ex)
        {
            currentMoney.Text = "当前金钱：未读取";
            status.Text = ex.Message;
        }
    }

    private void ApplyGameMoney()
    {
        try
        {
            int target = (int)money.Value;
            int actual = GameMoney.Set(target);
            setMoney.Checked = true;
            currentMoney.Text = $"当前金钱：{actual}";
            status.Text = $"已写入并回读金钱 {actual}；请在游戏内确认显示与存档";
            PlaySuccess();
        }
        catch (Exception ex) { LogFailure("设置金钱", ex, new { Value = money.Value }); MessageBox.Show(this, ex.Message, "应用金钱失败"); }
    }

    private async Task ResetBuild()
    {
        if(offlinePreview) return;
        var button=shortcutControls["resetBuild"];
        if(!button.Enabled) return;
        button.Enabled=false;
        status.Text="正在洗点，请返回游戏等待执行";
        try
        {
            int refunded=await Task.Run(GameItemScheduler.ResetBuild);
            status.Text=refunded==0?"洗点状态已确认，没有需要返还的普通技能点":"洗点已完成，返还 "+refunded+" 个技能点；请重新打开角色页";
            PlaySuccess();
        }
        catch(Exception ex) { LogFailure("一键洗点",ex); status.Text=$"洗点未确认：{ex.Message}"; }
        finally { if(!IsDisposed) button.Enabled=true; }
    }

    private async Task WinGwent()
    {
        if (offlinePreview) return;
        var button = shortcutControls["gwentWin"];
        if (!button.Enabled) return;
        button.Enabled = false;
        status.Text = "正在请求昆特牌获胜，请返回游戏等待结算";
        try
        {
            await Task.Run(GameItemScheduler.WinGwent);
            status.Text = "昆特牌已按玩家获胜结算";
            PlaySuccess();
        }
        catch (Exception ex) { LogFailure("昆特牌立即获胜", ex); status.Text = $"昆特牌操作未确认：{ex.Message}"; }
        finally { if (!IsDisposed) button.Enabled = true; }
    }

    private void RepairEquipment()
    {
        try
        {
            int repaired = GameDurability.RepairAll();
            status.Text = repaired == 0 ? "当前装备和背包中没有需要修复的装备" : $"已修复 {repaired} 件装备；请在游戏背包中确认";
            PlaySuccess(repaired > 0);
        }
        catch (Exception ex) { LogFailure("恢复耐久", ex); MessageBox.Show(this, ex.Message, "恢复耐久失败"); }
    }

    private void RefreshGameWeight()
    {
        if (weightToggle is null) return;
        try
        {
            bool active = GameWeight.Read();
            refreshingWeight = true;
            weightToggle.Checked = active;
            refreshingWeight = false;
            status.Text = active ? "游戏内无视负重已开启" : "游戏内无视负重已关闭";
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { refreshingWeight = false; }
    }

    private void RefreshInventoryHooks()
    {
        if (keepItemsToggle is null) return;
        try
        {
            GameInventoryHooks.Rebind();
            var active = GameInventoryHooks.Read();
            refreshingInventoryHooks = true;
            if (active.Gold != 1) goldMultiplier.Value = active.Gold;
            goldMultiplierToggle.Checked = active.Gold != 1 || (draft.RuntimeSettingsSaved && draft.GoldMultiplierEnabled && draft.GoldMultiplier == 1);
            keepItemsToggle.Checked = active.KeepItems;
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { refreshingInventoryHooks = false; }
    }

    private void ApplyGoldMultiplier(bool sound = false)
    {
        if (refreshingInventoryHooks) return;
        try
        {
            GameInventoryHooks.Rebind();
            var current = GameInventoryHooks.Read();
            var active = GameInventoryHooks.Set(goldMultiplierToggle.Checked ? goldMultiplier.Value : 1, current.KeepItems);
            SaveRuntimeSettings();
            status.Text = goldMultiplierToggle.Checked ? $"金币收入倍率已开启 ×{active.Gold:0.0}" : "金币收入倍率已关闭（1 倍）";
            if (sound && !offlinePreview) ToggleSounds.Play(goldMultiplierToggle.Checked);
        }
        catch (Exception ex) { LogFailure("金币倍率", ex, new { Enabled = goldMultiplierToggle.Checked, Value = goldMultiplier.Value }); MessageBox.Show(this, ex.Message, "设置金币收入倍率失败"); RefreshInventoryHooks(); }
    }

    private void RefreshExperience()
    {
        refreshingExperience = true;
        try
        {
            decimal active = GameExperience.Read();
            if (active != 1) xp.Value = active;
            xpMultiplierToggle.Checked = active != 1 || (draft.RuntimeSettingsSaved && draft.XpMultiplierEnabled && draft.XpMultiplier == 1);
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { refreshingExperience = false; }
    }

    private void ApplyExperience(bool sound = false)
    {
        if (refreshingExperience) return;
        try
        {
            decimal active = GameExperience.Set(xpMultiplierToggle.Checked ? xp.Value : 1);
            SaveRuntimeSettings();
            status.Text = xpMultiplierToggle.Checked ? $"经验倍率已开启 ×{active:0.0}" : "经验倍率已关闭（1 倍）";
            if (sound && !offlinePreview) ToggleSounds.Play(xpMultiplierToggle.Checked);
        }
        catch (Exception ex) { LogFailure("经验倍率", ex, new { Enabled = xpMultiplierToggle.Checked, Value = xp.Value }); MessageBox.Show(this, ex.Message, "设置经验倍率失败"); RefreshExperience(); }
    }

    private void ApplyKeepItems()
    {
        if (refreshingInventoryHooks || keepItemsToggle is null) return;
        try
        {
            GameInventoryHooks.Rebind();
            var current = GameInventoryHooks.Read();
            var active = GameInventoryHooks.Set(current.Gold, keepItemsToggle.Checked);
            SaveRuntimeSettings();
            status.Text = active.KeepItems ? "非金币物品数量不减已开启" : "非金币物品数量不减已关闭";
            if (!offlinePreview) ToggleSounds.Play(active.KeepItems);
        }
        catch (Exception ex) { LogFailure("物品不减", ex, new { Enabled = keepItemsToggle.Checked }); MessageBox.Show(this, ex.Message, "设置物品数量失败"); RefreshInventoryHooks(); }
    }

    private void ApplyGameWeight()
    {
        if (refreshingWeight || weightToggle is null) return;
        try
        {
            bool active = GameWeight.Set(weightToggle.Checked);
            SaveRuntimeSettings();
            status.Text = active ? "游戏内无视负重已开启" : "游戏内无视负重已关闭";
            if (!offlinePreview) ToggleSounds.Play(active);
        }
        catch (Exception ex)
        {
            LogFailure("无视负重", ex, new { Enabled = weightToggle.Checked });
            refreshingWeight = true;
            weightToggle.Checked = !weightToggle.Checked;
            refreshingWeight = false;
            MessageBox.Show(this, ex.Message, "设置无视负重失败");
        }
    }

    private void RefreshCatalog()
    {
        var query = itemSearch.Text.Trim();
        var selectedName = (itemCatalog.SelectedItem as GameItem)?.Name;
        itemCatalog.Items.Clear();
        foreach (var item in availableItems.Where(item => item.Matches(query, fuzzySearch.Checked) && MatchesLevel(itemLevel,GameEquipmentLevels.Reference(item.Name)))) itemCatalog.Items.Add(item);
        for (int i = 0; i < itemCatalog.Items.Count; i++)
        {
            if ((itemCatalog.Items[i] as GameItem)?.Name != selectedName) continue;
            itemCatalog.SelectedIndex = i;
            break;
        }
    }

    private async Task TeleportToMapPin()
    {
        if (teleportBusy) return;
        teleportBusy = true;
        var fallToggle=debugFactToggles["noFallDamage"];
        fallToggle.Enabled=false;
        status.Text = "正在传送到地图标记；请返回游戏，关闭地图和暂停菜单";
        try
        {
            var progress=new Progress<string>(message=> {if(!IsDisposed) status.Text=message;});
            var result = await Task.Run(() => GameTeleport.Execute(progress:progress));
            status.Text = result.Confirmed
                ? $"已传送到地图标记附近（{result.Position.X:0.0}, {result.Position.Y:0.0}, {result.Position.Z:0.0}），位置回读通过"
                : "传送已提交，位置尚未回读确认；请关闭地图和暂停菜单返回游戏，先观察结果再重试";
            PlaySuccess(result.Confirmed);
        }
        catch (Exception ex) { LogFailure("传送到地图标记", ex); status.Text = $"传送未完成：{ex.Message}"; }
        finally { fallToggle.Enabled=true; teleportBusy = false; }
    }

    private async Task GenerateGear()
    {
        if (gearBase.SelectedItem is not GameItem item) return;
        string[] effects = selectedAffixes.ToArray();
        string word = (enchantment.SelectedItem as EquipmentEffect)?.Id ?? "";
        int slots = (int)runeSlots.Value;
        var numericTargets = ReadNumericTargets();
        generateGear.Enabled = false;
        gearSearch.Enabled = gearBase.Enabled = gearType.Enabled = gearLevel.Enabled = false;
        status.Text = $"正在生成 {item.DisplayName}…";
        try
        {
            int id = await Task.Run(() => GameEquipment.Create(item.Name, effects, word, slots,numericTargets));
            status.Text = $"{item.DisplayName} 已生成到背包，装备配置回读通过（ID {id}）";
            PlaySuccess();
        }
        catch (Exception ex) { LogFailure("生成装备", ex, new { item.Name, Effects = effects, Enchantment = word, Slots = slots, NumericTargets = numericTargets }); MessageBox.Show(this, ex.Message, "装备生成未完成"); status.Text = "装备生成未完成，请检查提示与背包"; }
        finally
        {
            gearSearch.Enabled = gearBase.Enabled = gearType.Enabled = gearLevel.Enabled = true;
            generateGear.Enabled = gearBase.SelectedItem is GameItem;
        }
    }

    private void RefreshGearPreview()
    {
        string validation;
        try
        {
            if(gearBase.SelectedItem is not GameItem) throw new InvalidOperationException("请先选择基础装备。");
            GameEquipment.ValidateConfiguration(CurrentGearCategory(),selectedAffixes,(enchantment.SelectedItem as EquipmentEffect)?.Id ?? "",(int)runeSlots.Value,ReadNumericTargets());
            validation="类型、词条及目标范围检查通过；数值可达性与槽位上限需生成后校验。";
        }
        catch(InvalidOperationException ex) {validation=$"配置需调整：{ex.Message}";}
        gearPreview.Text = $"名称：{(string.IsNullOrWhiteSpace(gearName.Text) ? "未选择装备" : gearName.Text.Trim())}\r\n"
            + $"基础装备：{(gearBase.SelectedItem as GameItem)?.DisplayName}\r\n"
            + $"类型：{gearType.Text}\r\n基础属性：沿用基础装备\r\n最低槽位：{(enchantment.SelectedItem is EquipmentEffect { Id.Length: > 0 } ? 3 : runeSlots.Value)}\r\n"
            + $"词条：\r\n{string.Join("\r\n", GameEquipment.Presets.Affixes.Where(effect => selectedAffixes.Contains(effect.Id)).Select(effect => effect.DisplayName))}\r\n"
            + $"附魔：{enchantment.Text}\r\n"
            + NumericPreview(ReadNumericTargets())
            + $"\r\n参考需要等级：{GameEquipmentLevels.Describe(GameEquipmentLevels.Reference((gearBase.SelectedItem as GameItem)?.Name ?? ""))} → 修改后预估 {GameEquipmentLevels.Describe(GameEquipmentLevels.Reference((gearBase.SelectedItem as GameItem)?.Name ?? ""),ReadNumericTargets())}\r\n"
            + "\r\n"+validation
            + "\r\n部分目标数值受装备随机属性限制，可在背包页使用“可达值”调整。修改基础伤害或护甲可能改变需要等级。";
    }

    private void RefreshGearBases()
    {
        string? selectedBase = (gearBase.SelectedItem as GameItem)?.Name;
        string? selectedWord = (enchantment.SelectedItem as EquipmentEffect)?.Id;
        var category = gearType.Text switch
        {
            "钢剑" => "steelsword", "银剑" => "silversword", "胸甲" => "armor", "靴子" => "boots",
            "裤子" => "pants", "手套" => "gloves", "弩" => "crossbow", _ => ""
        };
        UpdateNumericTypes(category);
        selectedAffixes.RemoveWhere(id=>!GameEquipment.Presets.Affixes.Single(effect=>effect.Id==id).AppliesTo(category));
        FilterAffixes();
        enchantment.Items.Clear();
        enchantment.Items.Add(new EquipmentEffect { DisplayName = "无附魔" });
        string wordType = category is "steelsword" or "silversword" ? "剑" : category == "armor" ? "胸甲" : "";
        foreach (var effect in GameEquipment.Presets.Enchantments.Where(effect => effect.Equipment == wordType && (effect.Id == selectedWord || effect.DisplayName.Contains(enchantSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)))) enchantment.Items.Add(effect);
        enchantment.SelectedIndex = 0;
        for (int index = 1; index < enchantment.Items.Count; index++)
            if (enchantment.Items[index] is EquipmentEffect effect && effect.Id == selectedWord) enchantment.SelectedIndex = index;
        gearBase.Items.Clear();
        foreach (var item in availableItems.Where(item => item.Category == category && item.Matches(gearSearch.Text.Trim(), false) && MatchesLevel(gearLevel,GameEquipmentLevels.Reference(item.Name)))) gearBase.Items.Add(item);
        if (gearBase.Items.Count > 0) gearBase.SelectedIndex = 0;
        for (int index = 0; index < gearBase.Items.Count; index++)
            if (gearBase.Items[index] is GameItem item && item.Name == selectedBase) gearBase.SelectedIndex = index;
        generateGear.Enabled = gearBase.SelectedItem is GameItem;
        gearName.Text = (gearBase.SelectedItem as GameItem)?.DisplayName ?? "";
        RefreshGearPreview();
    }

    private void AddGear()
    {
        if (string.IsNullOrWhiteSpace(gearName.Text))
        {
            MessageBox.Show(this, "请先选择基础装备。", "自定义装备");
            return;
        }
        draft.Gear.Add(new GearEntry
        {
            BaseType = gearType.Text,
            BaseItem = (gearBase.SelectedItem as GameItem)?.Name ?? "",
            Name = gearName.Text.Trim(),
            RuneSlots = (int)runeSlots.Value,
            Affixes = selectedAffixes.ToList(),
            NumericTargets = ReadNumericTargets(),
            Enchantment = (enchantment.SelectedItem as EquipmentEffect)?.Id ?? ""
        });
        if(SaveDraft()) PlaySuccess();
    }

    private void RemoveGear()
    {
        if (pendingGear.SelectedIndex < 0) return;
        draft.Gear.RemoveAt(pendingGear.SelectedIndex);
        if(SaveDraft()) PlaySuccess();
    }

    private bool SaveDraft()
    {
        try { DraftStore.Save(draft); status.Text = "草案已保存在本机"; RefreshAll(); return true; }
        catch (Exception ex) { ErrorLog.Write("保存配置", ex); status.Text = $"配置保存失败：{ex.Message}"; return false; }
    }

    private void RefreshAll()
    {
        RefreshCatalog();
        pendingGear.Items.Clear();
        foreach (var gear in draft.Gear) pendingGear.Items.Add($"{gear.Name} · {gear.BaseType} · {gear.Affixes.Count} 项词条");
        RefreshGearPreview();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (offlinePreview) return;
        lastHotkeyForeground=-1;RefreshHotkeyAvailability();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        foreach(int id in registeredHotkeys) UnregisterHotKey(hotkeyWindow,id);
        registeredHotkeys.Clear();
        hotkeyWindow=0;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (HandleWindowResize(ref message)) return;
        if (message.Msg == 0x312 && ActivateFeatureShortcut(message.WParam.ToInt32())) return;
        base.WndProc(ref message);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);

    private static TableLayoutPanel Grid(int columns)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            Padding = new Padding(8),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        if (columns > 1)
        {
            panel.RowCount = 1;
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }
        return panel;
    }

    private static Control Info(string title, string detail)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Width = 470, Height = 70 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 0, 0, 4), Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold, GraphicsUnit.Pixel) }, 0, 0);
        panel.Controls.Add(new Label { Text = detail, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, ForeColor = SystemColors.GrayText }, 0, 1);
        return panel;
    }

    private static Control Labeled(string title, Control control)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        panel.RowCount = 1;
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty }, 0, 0);
        control.Margin = new Padding(1);
        panel.Controls.Add(control, 1, 0);
        return panel;
    }

    private static Control ValueRow(Control label, Control control)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, WrapContents = false };
        label.Width = 150;
        panel.Controls.Add(label);
        panel.Controls.Add(control);
        return panel;
    }

}
