namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private CheckBox? equipmentLevelToggle;
    private bool refreshingEquipmentLevel;

    private void ApplyEquipmentLevel()
    {
        if(refreshingEquipmentLevel || equipmentLevelToggle is null) return;
        try
        {
            bool active=offlinePreview ? equipmentLevelToggle.Checked : GameItemScheduler.SetEquipmentLevelBypass(equipmentLevelToggle.Checked);
            SaveRuntimeSettings();
            status.Text=active ? "无视装备穿戴等级已开启" : "无视装备穿戴等级已关闭";
            if(!offlinePreview) ToggleSounds.Play(active);
        }
        catch(Exception ex)
        {
            LogFailure("无视装备穿戴等级",ex,new {Enabled=equipmentLevelToggle.Checked});
            refreshingEquipmentLevel=true;
            equipmentLevelToggle.Checked=!equipmentLevelToggle.Checked;
            refreshingEquipmentLevel=false;
            MessageBox.Show(this,ex.Message,"设置装备穿戴等级失败");
        }
    }

    private void RefreshEquipmentLevel()
    {
        if(equipmentLevelToggle is null) return;
        try
        {
            refreshingEquipmentLevel=true;
            equipmentLevelToggle.Checked=GameItemScheduler.ReadEquipmentLevelBypass();
        }
        catch(Exception ex) {status.Text=ex.Message;}
        finally {refreshingEquipmentLevel=false;}
    }
}
