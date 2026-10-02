namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private static string NumericPreview(IReadOnlyDictionary<string,decimal> targets,IReadOnlyDictionary<string,decimal>? current=null) =>
        string.Join("\r\n",targets.Select(pair=>
        {
            var field=GameEquipmentNumbers.Fields.Single(field=>field.Id==pair.Key);
            string before=current is not null && current.TryGetValue(pair.Key,out decimal value) ? value.ToString("0.###") : "生成后读取";
            return $"{field.Label}：{before} → 目标 {pair.Value:0.###}";
        }));

    private void RefreshEditPreview()
    {
        editCheckResult="配置已更新，尚未校验；应用时重新检查。";
        RenderEditPreview();
    }

    private void RenderEditPreview()
    {
        if(loadedEquipment is null)
        {
            editPreview.Text="修改预览\r\n先读取选中装备，再调整配置。";
            return;
        }
        var targets=ReadNumericTargets(editNumericFields);
        string effects=string.Join("、",GameEquipment.Presets.Affixes.Where(effect=>selectedEditAffixes.Contains(effect.Id)).Select(effect=>effect.DisplayName));
        editPreview.Text=$"{loadedEquipment.DisplayName} · 修改预览\r\n"
            + EditLevelPreview(targets)
            + NumericPreview(targets,loadedNumericValues)
            + (targets.Count==0 ? "未设置数值目标\r\n" : "\r\n")
            + $"最低槽位：{(editEnchantment.SelectedItem is EquipmentEffect {Id.Length:>0} ? Math.Max(3,editSlots.Value) : editSlots.Value)}；附魔：{editEnchantment.Text}\r\n词条：{(effects.Length==0?"无所列词条":effects)}\r\n"
            + editCheckResult
            + "\r\n目标值需校验可达性；词条或附魔变化可能影响未勾选属性。最终伤害区间与需要等级在游戏中确认。";
    }

    private async Task CheckSelectedEquipment()
    {
        if(offlinePreview || loadedEquipment is not InventoryItem item || inventoryList.SelectedItem as InventoryItem!=item) return;
        var targets=ReadNumericTargets(editNumericFields);
        string[] effects=selectedEditAffixes.ToArray();
        string word=(editEnchantment.SelectedItem as EquipmentEffect)?.Id ?? "";
        int slots=(int)editSlots.Value;
        string result;
        SetInventoryBusy(true);
        try
        {
            bool complete=await Task.Run(()=>GameEquipment.CheckEdit(item,effects,word,slots,targets));
            result=complete ? "当前配置校验通过；应用时重新检查并回读。" : "类型、词条、附魔及槽位检查通过；更换词条或附魔后仍需校验数值组合，应用可能部分完成。";
            PlaySuccess();
        }
        catch(Exception ex)
        {
            LogFailure("校验装备配置",ex,new {item.Name,item.UniqueId,Effects=effects,Enchantment=word,Slots=slots,NumericTargets=targets});
            result=$"校验未通过：{ex.Message}";
        }
        finally
        {
            SetInventoryBusy(false);
        }
        editCheckResult=result;
        status.Text=result;
        RenderEditPreview();
    }
}
