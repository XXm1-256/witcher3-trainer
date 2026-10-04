namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TextBox inventorySearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索当前背包中文名或内部名" };
    private readonly ComboBox inventoryType = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, MaxDropDownItems = 12 };
    private sealed record InventoryTypeFilter(string Key, string Label)
    {
        public override string ToString() => Label;
    }
    private readonly ListBox inventoryList = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, SelectionMode = SelectionMode.MultiExtended };
    private readonly TextBox inventoryDetails = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly CheckedListBox editAffixes = new() { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly TextBox editAffixSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索词条名称或效果" };
    private readonly HashSet<string> selectedEditAffixes = [];
    private bool filteringEditAffixes;
    private readonly ComboBox editEnchantment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
    private readonly NumericUpDown editSlots = new() { Minimum = 0, Maximum = 3, Dock = DockStyle.Fill };
    private readonly NumericUpDown deleteQuantity = new() { Minimum = 1, Maximum = 1, Value = 1, Width = 90 };
    private readonly Button readInventory = new MetalButton() { Text = "刷新背包", AutoSize = true };
    private readonly Button inspectEquipment = new MetalButton() { Text = "读取选中装备", AutoSize = true, Enabled = false };
    private readonly Button applyEquipment = new MetalButton() { Text = "应用装备修改", AutoSize = true, Enabled = false };
    private readonly Button checkEquipment = new MetalButton() { Text = "校验装备配置", AutoSize = true, Enabled = false };
    private readonly TextBox editPreview = new() { Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="装备修改预览" };
    private readonly Dictionary<string,decimal> loadedNumericValues = [];
    private string editCheckResult="尚未校验；校验不会修改装备。";
    private readonly Button deleteItem = new MetalButton() { Text = "删除选中物品", AutoSize = true, Enabled = false };
    private List<InventoryItem> inventorySnapshot = [];
    private InventoryItem? loadedEquipment;
    private bool inventoryBusy;
    private readonly Dictionary<string,(CheckBox Use,NumericUpDown Value)> editNumericFields = [];

    private TabPage BuildInventory()
    {
        var page = new TabPage("背包与装备");
        var columns = Grid(2);
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var left = Grid(1);
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        left.Controls.Add(Info("当前背包", "Ctrl 点选多件、Shift 连选；装备编辑请选择单件。多选删除按每件指定数量执行，不足时删完该件。"), 0, 0);
        left.Controls.Add(readInventory, 0, 1);
        left.Controls.Add(inventorySearch, 0, 2);
        left.RowStyles.Insert(3,new RowStyle(SizeType.Absolute,40));
        left.RowStyles.Insert(3,new RowStyle(SizeType.Absolute,40));
        inventoryType.Items.AddRange(new object[] { new InventoryTypeFilter("", "全部类型"), new InventoryTypeFilter("$weapons", "全部武器"), new InventoryTypeFilter("$armor", "全部护甲") });
        foreach (var category in availableItems.GroupBy(item => item.Category).OrderBy(group => group.First().CategoryName, StringComparer.CurrentCulture))
            inventoryType.Items.Add(new InventoryTypeFilter(category.Key, category.First().CategoryName));
        inventoryType.SelectedIndex = 0;
        left.Controls.Add(Labeled("物品类型",inventoryType),0,3);
        left.Controls.Add(Labeled("需要等级",inventoryLevel),0,4);
        left.Controls.Add(inventoryList, 0, 5);
        left.Controls.Add(editPreview,0,6);
        usageTips.SetToolTip(inventoryType,"按类型筛选，可同时搜索名称和筛选等级。切换类型后，删除只作用于当前列表中选中的物品。");
        inventoryType.SelectedIndexChanged += (_, _) => FilterInventory();
        usageTips.SetToolTip(inventoryLevel,"按背包装备的实际需要等级筛选。请返回游戏等待读取；换装或读档后可刷新背包。");
        inventoryLevel.SelectedIndexChanged += async (_,_) => {if(inventoryLevel.SelectedIndex>0) await ReadInventoryLevels();FilterInventory();};
        columns.Controls.Add(left, 0, 0);
        var right = Grid(1);
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 304));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.Controls.Add(inventoryDetails, 0, 0);
        var inspectRow = Grid(2);
        inspectRow.Padding = Padding.Empty;
        inspectRow.Margin = Padding.Empty;
        inspectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        inspectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        inspectEquipment.Dock = DockStyle.Fill;
        inspectEquipment.Margin = Padding.Empty;
        inspectRow.Controls.Add(inspectEquipment, 0, 0);
        inspectRow.Controls.Add(Labeled("最低槽位", editSlots), 1, 0);
        right.Controls.Add(inspectRow, 0, 1);
        right.Controls.Add(editAffixSearch, 0, 2);
        editAffixSearch.TextChanged += (_, _) => FilterEditAffixes();
        editAffixes.ItemCheck += (_, e) =>
        {
            if (filteringEditAffixes) return;
            var effect=(EquipmentEffect)editAffixes.Items[e.Index];
            if(e.NewValue==CheckState.Checked && e.CurrentValue!=CheckState.Checked && !effect.AppliesTo(loadedEquipment?.Category ?? ""))
            {
                e.NewValue=e.CurrentValue;
                status.Text=$"{effect.DisplayName}仅适用于{effect.TypeHint}，当前装备不适用。";
                return;
            }
            string id = effect.Id;
            if (e.NewValue == CheckState.Checked) selectedEditAffixes.Add(id); else selectedEditAffixes.Remove(id);
            RefreshEditPreview();
        };
        FilterEditAffixes();
        right.Controls.Add(editAffixes, 0, 3);
        right.Controls.Add(BuildNumericFields(editNumericFields, RefreshEditPreview), 0, 5);
        right.Controls.Add(BuildAffixNumericEditor(editAffixes,editNumericFields,RefreshEditPreview),0,4);
        LoadEditNumericValues(new Dictionary<string,decimal>());
        right.Controls.Add(Labeled("附魔", editEnchantment), 0, 6);
        var editActions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
        editActions.Controls.Add(checkEquipment); editActions.Controls.Add(applyEquipment);
        var nearestBase=new MetalButton {Text="使用可达目标",AutoSize=true};
        editActions.Controls.Add(nearestBase);
        usageTips.SetToolTip(nearestBase,"先读取装备，勾选基础伤害或护甲并填写目标。本按钮填入最接近的可用数值；点击“应用装备修改”后生效。");
        nearestBase.Click+=async (_,_)=>
        {
            if(offlinePreview || loadedEquipment is not {} item || !applyEquipment.Enabled) return;
            var requested=ReadNumericTargets(editNumericFields).Where(pair=>pair.Key is "damage" or "armor").ToArray();
            if(requested.Length==0) {status.Text="请先勾选基础伤害或护甲值并输入目标。";return;}
            SetInventoryBusy(true);
            try
            {
                var reachable=await Task.Run(()=>GameItemScheduler.RunForItem(item,()=>requested.ToDictionary(pair=>pair.Key,pair=>GameEquipmentNumbers.NearestTarget(item.UniqueId,item.Category,pair.Key,pair.Value))));
                foreach(var pair in reachable) editNumericFields[pair.Key].Value.Value=Math.Clamp(pair.Value,editNumericFields[pair.Key].Value.Minimum,editNumericFields[pair.Key].Value.Maximum);
                status.Text="可达目标已填入；点击应用装备修改后生效。";PlaySuccess();
            }
            catch(Exception ex) {LogFailure("计算基础属性可达目标",ex,new {item.UniqueId});status.Text=ex.Message;}
            finally {SetInventoryBusy(false);}
        };
        right.Controls.Add(editActions, 0, 7);
        var removal = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        removal.Controls.Add(new Label { Text = "删除数量", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        removal.Controls.Add(deleteQuantity);
        deleteItem.Margin = new Padding(3, 0, 3, 0);
        removal.Controls.Add(deleteItem);
        right.Controls.Add(removal, 0, 8);
        columns.Controls.Add(right, 1, 0);
        page.Controls.Add(columns);
        usageTips.SetToolTip(applyEquipment, "修改当前选中的一件装备。请先读取装备，再设置词条、附魔和属性；槽位只能增加。完成后保存游戏。");
        usageTips.SetToolTip(deleteItem, "Ctrl点选或Shift连选多件。删除数量分别作用于每件物品，数量不足时全部删除。开启物品不减也可删除；完成后保存游戏。");
        readInventory.Click += async (_, _) => await LoadInventory();
        inventorySearch.TextChanged += (_, _) => FilterInventory();
        inventoryList.SelectedIndexChanged += (_, _) => InventorySelectionChanged();
        inspectEquipment.Click += async (_, _) => await InspectSelectedEquipment();
        applyEquipment.Click += async (_, _) => await EditSelectedEquipment();
        checkEquipment.Click += async (_,_)=>await CheckSelectedEquipment();
        editSlots.ValueChanged+=(_,_)=>RefreshEditPreview();
        editEnchantment.SelectedIndexChanged+=(_,_)=>RefreshEditPreview();
        usageTips.SetToolTip(checkEquipment,"检查词条、附魔、槽位和目标数值是否可用。检查通过后再点击“应用装备修改”。");
        deleteItem.Click += async (_, _) => await DeleteSelectedItem();
        return page;
    }

    private void FilterInventory()
    {
        var previous = inventoryList.SelectedItems.Cast<InventoryItem>().Select(item => item.UniqueId).ToHashSet();
        string query = inventorySearch.Text.Trim();
        inventoryList.BeginUpdate();
        inventoryList.Items.Clear();
        foreach (var item in inventorySnapshot.Where(item => (query.Length == 0 ||
            item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            InventoryTypeMatches((inventoryType.SelectedItem as InventoryTypeFilter)?.Key ?? "", item.Category) &&
            (inventoryLevel.SelectedIndex<=0 || item.IsEquipment && (inventoryLevel.SelectedIndex==1
                ? !inventoryLevels.ContainsKey(item.UniqueId)
                : inventoryLevels.TryGetValue(item.UniqueId,out var level) && level.Level==inventoryLevel.SelectedIndex-1))))
            inventoryList.Items.Add(item);
        for (int i = 0; i < inventoryList.Items.Count; i++)
            if (previous.Contains(((InventoryItem)inventoryList.Items[i]).UniqueId)) inventoryList.SetSelected(i, true);
        inventoryList.EndUpdate();
        InventorySelectionChanged();
    }

    private static bool InventoryTypeMatches(string filter, string category) => filter switch
    {
        "" => true,
        "$weapons" => category is "steelsword" or "silversword" or "crossbow" or "secondary" or "work_secondary" or "monster_weapon" or "axe1h" or "axe2h" or "blunt1h" or "hammer2h" or "halberd2h" or "spear2h" or "staff2h" or "polearm" or "cleaver1h" or "bow",
        "$armor" => category is "armor" or "boots" or "pants" or "gloves",
        _ => filter == category
    };

    private void InventorySelectionChanged()
    {
        loadedEquipment = null;
        loadedNumericValues.Clear();
        RefreshEditPreview();
        applyEquipment.Enabled = false;
        LoadEditNumericValues(new Dictionary<string,decimal>());
        editAffixes.Enabled = editEnchantment.Enabled = editSlots.Enabled = false;
        var selected = inventoryList.SelectedItems.Cast<InventoryItem>().ToArray();
        var item = selected.Length == 1 ? selected[0] : null;
        inspectEquipment.Enabled = !inventoryBusy && item?.IsEquipment == true;
        deleteItem.Enabled = !inventoryBusy && selected.Length != 0;
        deleteItem.Text = selected.Length > 1 ? $"删除选中 {selected.Length} 件" : "删除选中物品";
        deleteQuantity.Maximum = Math.Max(1, selected.Select(entry => entry.Quantity).DefaultIfEmpty(1).Max());
        inventoryDetails.Text = selected.Length > 1 ? $"已选择 {selected.Length} 件物品；删除数量对每件分别生效。\r\n编辑装备时请选择单件。" : item is null ? "选择背包中的物品。" :
            $"{item.DisplayName}\r\n内部名：{item.Name}\r\n唯一 ID：{item.UniqueId}；数量：{item.Quantity}\r\n装备需先点击“读取选中装备”。";
    }

    private void SetInventoryBusy(bool busy)
    {
        inventoryBusy = busy;
        readInventory.Enabled = inventorySearch.Enabled = inventoryList.Enabled = !busy;
        inventoryLevel.Enabled=!busy;
        inventoryType.Enabled=!busy;
        inspectEquipment.Enabled = !busy && inventoryList.SelectedItems.Count == 1 && (inventoryList.SelectedItem as InventoryItem)?.IsEquipment == true;
        deleteItem.Enabled = !busy && inventoryList.SelectedItem is InventoryItem;
        applyEquipment.Enabled = !busy && loadedEquipment is not null;
        checkEquipment.Enabled = !busy && loadedEquipment is not null;
        editAffixes.Enabled = editEnchantment.Enabled = editSlots.Enabled = !busy && loadedEquipment is not null;
        deleteQuantity.Enabled = !busy;
        SetEditNumericEnabled(!busy);
    }

    private async Task LoadInventory(bool sound = true, bool updateStatus = true)
    {
        SetInventoryBusy(true);
        try
        {
            inventorySnapshot = await Task.Run(GameInventory.Read);
            inventoryLevels.Clear();
            FilterInventory();
            if(updateStatus) status.Text = $"已读取 {inventorySnapshot.Count} 项背包记录";
            PlaySuccess(sound);
        }
        catch (Exception ex) { LogFailure("读取背包", ex); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "读取背包失败"); }
        finally { SetInventoryBusy(false); }
        if(inventoryLevel.SelectedIndex>0) await ReadInventoryLevels();
    }

    private async Task InspectSelectedEquipment(bool sound = true)
    {
        if (inventoryList.SelectedItems.Count != 1 || inventoryList.SelectedItem is not InventoryItem item || !item.IsEquipment) return;
        SetInventoryBusy(true);
        loadedEquipment = null;
        try
        {
            var result = await Task.Run(() => GameItemScheduler.RunForItem(item, () =>
            {
                var state = GameEquipment.Inspect(item);
                int[] ids = GameEquipment.Presets.Affixes.Select(effect => GameItemScheduler.ResolveEquipmentAbility(effect.Id)).ToArray();
                var numbers=GameEquipmentNumbers.Read(item.UniqueId,item.Category);
                return (state, ids, numbers, minimums:GameEquipmentNumbers.MinimumTargets(item.UniqueId,item.Category,numbers,state.Crafted), level:GameEquipmentLevels.Read(item));
            }));
            inventoryLevels[item.UniqueId]=result.level;
            item.RequiredLevel=result.level.Level;
            selectedEditAffixes.Clear();
            for (int i = 0; i < result.ids.Length; i++)
                if (result.state.Crafted.Contains(result.ids[i])) selectedEditAffixes.Add(GameEquipment.Presets.Affixes[i].Id);
            FilterEditAffixes();
            editSlots.Minimum = 0;
            editSlots.Maximum = Math.Max(result.state.Slots, result.state.Limit);
            editSlots.Value = result.state.Slots;
            editSlots.Minimum = result.state.Slots;
            editEnchantment.Items.Clear();
            editEnchantment.Items.Add(new EquipmentEffect { DisplayName = "无附魔" });
            string type = item.Category is "steelsword" or "silversword" ? "剑" : item.Category == "armor" ? "胸甲" : "";
            foreach (var effect in GameEquipment.Presets.Enchantments.Where(effect => effect.Equipment == type)) editEnchantment.Items.Add(effect);
            if (result.state.Enchantment.Length != 0 && !editEnchantment.Items.Cast<EquipmentEffect>().Any(effect => effect.Id == result.state.Enchantment))
                editEnchantment.Items.Add(new EquipmentEffect { Id = result.state.Enchantment, DisplayName = $"当前附魔：{result.state.Enchantment}" });
            editEnchantment.SelectedItem = editEnchantment.Items.Cast<EquipmentEffect>().Single(effect => effect.Id == result.state.Enchantment);
            loadedEquipment = item;
            LoadEditNumericValues(result.numbers,result.minimums);
            RefreshEditPreview();
            inventoryDetails.Text = $"{item.DisplayName} · ID {item.UniqueId}\r\n{(result.state.Equipped ? "当前已装备" : "背包装备")}；槽位 {result.state.Slots}/{result.state.Limit}\r\n未列出的原有能力保留；槽位只增加。\r\n附魔选“无附魔”可清除当前附魔。";
            status.Text = "选中装备已读取，可调整词条、附魔及勾选的目标数值";
            PlaySuccess(sound);
        }
        catch (Exception ex) { LogFailure("读取装备", ex, new { item.Name, item.UniqueId }); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "读取装备失败"); }
        finally { SetInventoryBusy(false); }
    }

    private async Task EditSelectedEquipment()
    {
        if (loadedEquipment is not InventoryItem item || inventoryList.SelectedItem as InventoryItem != item) return;
        string[] effects = selectedEditAffixes.ToArray();
        string word = (editEnchantment.SelectedItem as EquipmentEffect)?.Id ?? "";
        int slots = (int)editSlots.Value;
        var numericTargets=ReadNumericTargets(editNumericFields);
        bool submitted=false;
        SetInventoryBusy(true);
        try
        {
            await Task.Run(() => GameEquipment.CheckEdit(item,effects,word,slots,numericTargets));
            submitted=true;
            await Task.Run(() => GameEquipment.Edit(item, effects, word, slots,numericTargets));
            status.Text = $"{item.DisplayName}（ID {item.UniqueId}）编辑回读通过；请在游戏背包确认";
            PlaySuccess();
        }
        catch (Exception ex)
        {
            LogFailure("编辑装备", ex, new { item.Name, item.UniqueId, Effects = effects, Enchantment = word, Slots = slots, NumericTargets = numericTargets });
            if(submitted) loadedEquipment = null;
            status.Text = submitted ? "装备修改未完成，请重新读取；部分操作可能已执行" : "配置校验未通过，装备未修改";
            MessageBox.Show(this, ex.Message+ (submitted ? "\r\n请重新读取装备；部分操作可能已执行。" : "\r\n本次未修改装备，请调整目标后重试。"), "装备修改未完成");
            if(!submitted) return;
        }
        finally { SetInventoryBusy(false); }
        if (loadedEquipment is not null) await InspectSelectedEquipment(false);
    }

    private async Task DeleteSelectedItem()
    {
        var selected = inventoryList.SelectedItems.Cast<InventoryItem>().ToArray();
        if (selected.Length == 0) return;
        int quantity = (int)deleteQuantity.Value;
        string summary = string.Join("\r\n", selected.Select(item => $"{item.DisplayName} ×{Math.Min(quantity,item.Quantity)} · ID {item.UniqueId}"));
        if (MessageBox.Show(this, $"删除以下 {selected.Length} 件物品？\r\n{summary}\r\n此操作立即影响当前存档背包。", "确认删除物品", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        SetInventoryBusy(true);
        try
        {
            var result = await Task.Run(() =>
            {
                int completed = 0, removed = 0;
                var failures = new List<string>();
                foreach (var item in selected)
                {
                    int requested = Math.Min(quantity,item.Quantity);
                    try
                    {
                        int actual = GameInventory.Delete(item,requested);
                        completed++;
                        removed += actual;
                        ErrorLog.Write("删除物品完成",null,new {item.Name,item.UniqueId,Quantity=actual});
                    }
                    catch (Exception ex)
                    {
                        LogFailure("删除物品",ex,new {item.Name,item.UniqueId,Quantity=requested});
                        failures.Add($"{item.DisplayName}（ID {item.UniqueId}）：{ex.Message}");
                    }
                }
                return (completed,removed,failures);
            });
            status.Text = $"删除完成 {result.completed}/{selected.Length} 件，共 {result.removed} 个已确认删除；未完成 {result.failures.Count} 件";
            PlaySuccess(result.completed > 0);
            if(result.failures.Count != 0)
                MessageBox.Show(this,string.Join("\r\n",result.failures),"部分删除未完成，请核对刷新后的背包");
        }
        catch (Exception ex) { LogFailure("批量删除物品", ex); loadedEquipment = null; status.Text = ex.Message; MessageBox.Show(this, ex.Message, "删除未完成，请刷新背包"); }
        finally { SetInventoryBusy(false); }
        await LoadInventory(false,false);
    }
}
