namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly ComboBox itemLevel = LevelFilter();
    private readonly ComboBox gearLevel = LevelFilter();
    private readonly ComboBox inventoryLevel = LevelFilter();
    private readonly Dictionary<int,GameEquipmentLevels.Live> inventoryLevels = [];
    private static ComboBox LevelFilter()
    {
        var box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        box.Items.AddRange(["全部等级","等级未确定"]);
        for(int level=1;level<=100;level++) box.Items.Add($"{level} 级");
        box.SelectedIndex=0;
        return box;
    }
    private static bool MatchesLevel(ComboBox filter,GameEquipmentLevels.Profile? profile)
    {
        if(filter.SelectedIndex<=0) return true;
        var range=GameEquipmentLevels.Range(profile);
        return filter.SelectedIndex==1 ? profile is not null && range is null : range is {} value && value.Min<=filter.SelectedIndex-1 && value.Max>=filter.SelectedIndex-1;
    }
    private async Task ReadInventoryLevels()
    {
        if(offlinePreview || inventoryBusy || inventorySnapshot.Count==0) return;
        SetInventoryBusy(true);
        try
        {
            GameEquipmentLevels.NewGamePlus=GameDebugFacts.ReadNamed("NewGamePlus");
            var items=inventorySnapshot.Where(item=>item.IsEquipment && !inventoryLevels.ContainsKey(item.UniqueId)).ToArray();
            for(int index=0;index<items.Length;index++)
            {
                status.Text=$"读取装备需要等级 {index+1}/{items.Length}；请返回游戏保持运行";
                var item=items[index];
                inventoryLevels[item.UniqueId]=await Task.Run(()=>GameEquipmentLevels.Read(item));
                item.RequiredLevel=inventoryLevels[item.UniqueId].Level;
            }
            FilterInventory();
            status.Text=$"已读取 {inventoryLevels.Count} 件装备的需要等级";
        }
        catch(Exception ex) {LogFailure("读取装备需要等级",ex);FilterInventory();status.Text=$"等级读取未完成：{ex.Message}；请返回游戏后重试刷新背包。";}
        finally {SetInventoryBusy(false);}
    }
    private void RefreshReferenceLevels()
    {
        if(offlinePreview) return;
        try
        {
            bool plus=GameDebugFacts.ReadNamed("NewGamePlus");
            if(plus==GameEquipmentLevels.NewGamePlus) return;
            GameEquipmentLevels.NewGamePlus=plus;
            RefreshCatalog();
            RefreshGearBases();
        }
        catch { /* Game may be loading or closed; retain the last reference mode. */ }
    }
    private string EditLevelPreview(IReadOnlyDictionary<string,decimal> targets)
    {
        if(loadedEquipment is null || !inventoryLevels.TryGetValue(loadedEquipment.UniqueId,out var actual)) return "需要等级：读取中或未确定\r\n";
        return $"需要等级：{actual.Level} 级 → 预估 {GameEquipmentLevels.Describe(actual.Values,targets)}\r\n";
    }
}
