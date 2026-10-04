namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private sealed class PendingItem(GameItem item, int quantity)
    {
        internal GameItem Item { get; } = item;
        internal int Quantity { get; set; } = quantity;
        internal string State { get; set; } = "待添加";
        public override string ToString() => $"{Item.DisplayName} ×{Quantity} · {State}";
    }

    private readonly ListBox pendingItems = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly NumericUpDown pendingItemQuantity = new() { Minimum = 1, Maximum = 9999, Value = 1, Dock = DockStyle.Fill };
    private readonly Button queueItem = new MetalButton() { Text = "加入待添加清单", AutoSize = true };
    private readonly Button removePendingItem = new MetalButton() { Text = "移除选中项", AutoSize = true };
    private readonly Button clearPendingItems = new MetalButton() { Text = "清空清单", AutoSize = true };
    private readonly Button stopItemBatch = new MetalButton() { Text = "完成当前项后停止", AutoSize = true, Enabled = false };
    private bool itemBatchBusy;
    private bool stopItemBatchRequested;
    private bool refreshingPendingQuantity;

    private TableLayoutPanel BuildItemBatchPanel()
    {
        var right = Grid(1);
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        right.Controls.Add(Info("待添加清单", "从左侧选物品，设置数量后加入清单；可继续搜索其他物品。"), 0, 0);
        right.Controls.Add(selectedItem, 0, 1);
        right.Controls.Add(Labeled("加入数量", itemQuantity), 0, 2);
        right.Controls.Add(queueItem, 0, 3);
        right.Controls.Add(pendingItems, 0, 4);
        right.Controls.Add(Labeled("选中项数量", pendingItemQuantity), 0, 5);
        var edits = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        edits.Controls.AddRange([removePendingItem, clearPendingItems]);
        right.Controls.Add(edits, 0, 6);
        itemAddNotice.Text = "清单为空。加入物品后一次提交，再返回游戏关闭暂停菜单。";
        itemAddNotice.AutoEllipsis = true;
        usageTips.SetToolTip(itemAddNotice, "添加进度与结果显示在此处；暂停时返回游戏即可继续处理。");
        right.Controls.Add(itemAddNotice, 0, 7);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.AddRange([giveItem, stopItemBatch]);
        right.Controls.Add(actions, 0, 8);
        queueItem.Click += (_, _) => QueueSelectedItem();
        itemCatalog.DoubleClick += (_, _) => QueueSelectedItem();
        pendingItems.SelectedIndexChanged += (_, _) => RefreshPendingQuantity();
        pendingItemQuantity.ValueChanged += (_, _) =>
        {
            if (refreshingPendingQuantity || itemBatchBusy || pendingItems.SelectedItem is not PendingItem { State: "待添加" } entry) return;
            entry.Quantity = (int)pendingItemQuantity.Value;
            RefreshPendingRow(entry);
        };
        removePendingItem.Click += (_, _) =>
        {
            if (!itemBatchBusy && pendingItems.SelectedIndex >= 0) pendingItems.Items.RemoveAt(pendingItems.SelectedIndex);
            RefreshPendingQuantity();
        };
        clearPendingItems.Click += (_, _) => { if (!itemBatchBusy) pendingItems.Items.Clear(); RefreshPendingQuantity(); };
        giveItem.Click += async (_, _) => await AddItemBatch((item, quantity, waiting) => GameItemScheduler.Give(item, quantity, waiting));
        stopItemBatch.Click += (_, _) => { stopItemBatchRequested = true; stopItemBatch.Enabled = false; status.Text = "当前项完成后停止，其余物品保留在清单"; };
        usageTips.SetToolTip(queueItem, "加入清单暂不发送到游戏。同一待添加物品再次加入时合并数量，最多9999。双击左侧物品也可加入。");
        usageTips.SetToolTip(pendingItemQuantity, "先选中右侧清单中的待添加物品，再修改数量。");
        usageTips.SetToolTip(giveItem, "依次添加清单中的待添加项。返回游戏并关闭暂停菜单后处理；已完成和结果未确认的项目不会重复提交。");
        usageTips.SetToolTip(stopItemBatch, "保留当前已提交请求，完成后停止。请返回游戏让当前项处理完毕。");
        RefreshPendingQuantity();
        FormClosing += (_, e) =>
        {
            if (closingWithoutGame || !itemBatchBusy) return;
            e.Cancel = true;
            stopItemBatchRequested = true;
            stopItemBatch.Enabled = false;
            status.Text = "当前添加请求尚未结束，请返回游戏处理完毕后关闭修改器";
        };
        return right;
    }

    private void QueueSelectedItem()
    {
        if (itemBatchBusy || itemCatalog.SelectedItem is not GameItem item) return;
        int quantity = (int)itemQuantity.Value;
        var existing = pendingItems.Items.Cast<PendingItem>().FirstOrDefault(entry => entry.Item.Name == item.Name && entry.State == "待添加");
        if (existing is null)
        {
            existing = new PendingItem(item, quantity);
            pendingItems.Items.Add(existing);
        }
        else
        {
            if (existing.Quantity + quantity > 9999) { status.Text = "同一物品的待添加数量最多9999，请调整数量"; return; }
            existing.Quantity += quantity;
            RefreshPendingRow(existing);
        }
        pendingItems.SelectedItem = existing;
        RefreshPendingQuantity();
        itemAddNotice.Text = $"清单共 {pendingItems.Items.Count} 项。可继续加入物品，或点击“添加清单全部物品”。";
    }

    private void RefreshPendingQuantity()
    {
        refreshingPendingQuantity = true;
        var selected = pendingItems.SelectedItem as PendingItem;
        pendingItemQuantity.Value = selected?.Quantity ?? 1;
        pendingItemQuantity.Enabled = !itemBatchBusy && selected?.State == "待添加";
        removePendingItem.Enabled = !itemBatchBusy && selected is not null;
        clearPendingItems.Enabled = !itemBatchBusy && pendingItems.Items.Count > 0;
        giveItem.Enabled = !itemBatchBusy && pendingItems.Items.Cast<PendingItem>().Any(entry => entry.State == "待添加");
        refreshingPendingQuantity = false;
    }

    private void RefreshPendingRow(PendingItem entry)
    {
        int index = pendingItems.Items.IndexOf(entry);
        if (index >= 0) pendingItems.Items[index] = entry;
    }

    private async Task AddItemBatch(Func<string, int, Action, (int Added, int ReturnedIds)> give)
    {
        if (itemBatchBusy) return;
        var batch = pendingItems.Items.Cast<PendingItem>().Where(entry => entry.State == "待添加").ToArray();
        if (batch.Length == 0) return;
        itemBatchBusy = true;
        stopItemBatchRequested = false;
        queueItem.Enabled = itemQuantity.Enabled = false;
        stopItemBatch.Enabled = true;
        RefreshPendingQuantity();
        int completed = 0;
        bool failed = false;
        try
        {
            foreach (var entry in batch)
            {
                if (stopItemBatchRequested) break;
                entry.State = "处理中";
                RefreshPendingRow(entry);
                itemAddNotice.Text = $"正在添加 {completed + 1}/{batch.Length}：{entry.Item.DisplayName} ×{entry.Quantity}。请返回游戏关闭暂停菜单。";
                status.Text = itemAddNotice.Text;
                try
                {
                    var result = await Task.Run(() => give(entry.Item.Name, entry.Quantity, () =>
                    {
                        if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() =>
                        {
                            if (!itemBatchBusy || entry.State != "处理中") return;
                            itemAddNotice.Text = $"{entry.Item.DisplayName} 正在等待游戏；返回游戏关闭暂停菜单后，清单会继续添加。";
                            status.Text = itemAddNotice.Text;
                        }));
                    }));
                    if (IsDisposed) return;
                    entry.State = $"已添加 {result.Added}";
                    completed++;
                    RefreshPendingRow(entry);
                    PlaySuccess();
                }
                catch (Exception ex)
                {
                    if (IsDisposed) return;
                    failed = true;
                    entry.State = "结果未确认";
                    RefreshPendingRow(entry);
                    LogFailure("批量添加物品", ex, new { entry.Item.Name, entry.Item.DisplayName, entry.Quantity, Completed = completed, Total = batch.Length });
                    itemAddNotice.Text = $"{entry.Item.DisplayName} 结果未确认：{ex.Message} 核对游戏背包后，可移除此项；其余待添加项可继续提交。";
                    usageTips.SetToolTip(itemAddNotice, itemAddNotice.Text);
                    status.Text = $"添加已停止：{entry.Item.DisplayName} 结果未确认；已完成 {completed} 项";
                    break;
                }
            }
            if (!failed && !IsDisposed)
            {
                itemAddNotice.Text = stopItemBatchRequested ? $"已停止，本次完成 {completed} 项；其余物品保留待添加。" : $"本次完成 {completed} 项。每件的实际增加数量已显示在清单。";
                status.Text = itemAddNotice.Text;
            }
        }
        finally
        {
            itemBatchBusy = false;
            if (!IsDisposed)
            {
                queueItem.Enabled = itemQuantity.Enabled = true;
                stopItemBatch.Enabled = false;
                RefreshPendingQuantity();
            }
        }
    }
}
