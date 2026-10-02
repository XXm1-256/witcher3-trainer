using System.Diagnostics;

namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private void CaptureRuntimeSettings()
    {
        draft.GoldMultiplier = goldMultiplier.Value;
        draft.GoldMultiplierEnabled = goldMultiplierToggle.Checked;
        draft.XpMultiplier = xp.Value;
        draft.XpMultiplierEnabled = xpMultiplierToggle.Checked;
        draft.KeepItems = keepItemsToggle?.Checked ?? false;
        draft.MoveSpeedMultiplier=moveSpeedValue.Value;
        draft.JumpHeightMultiplier=jumpHeightValue.Value;
        draft.Toggles["moveSpeed"]=moveSpeedToggle.Checked;
        draft.Toggles["jumpHeight"]=jumpHeightToggle.Checked;
        draft.SwimSpeedMultiplier=swimSpeedValue.Value;
        draft.Toggles["swimSpeed"]=swimSpeedToggle.Checked;
        draft.Toggles["autoLoot"]=autoLootToggle.Checked;
        draft.EnemySpeedMultiplier=enemySpeedValue.Value;
        draft.Toggles["enemySpeed"]=enemySpeedToggle.Checked;
        draft.Toggles["catVision"]=catVisionToggle.Checked;
        draft.Toggles["ignoreWeight"] = weightToggle?.Checked ?? false;
        draft.Toggles["ignoreEquipmentLevel"] = equipmentLevelToggle?.Checked ?? false;
        foreach(var (feature,box) in debugFactToggles) draft.Toggles[feature]=box.Checked;
        draft.RuntimeSettingsSaved = true;
    }

    private void SaveRuntimeSettings()
    {
        if (!draft.SaveSwitchesAndMultipliers) return;
        CaptureRuntimeSettings();
        SaveDraft();
    }

    private void LogFailure(string operation, Exception error, object? context = null) =>
        ErrorLog.Write(operation, error, new { Details = context, Runtime = GameItemScheduler.Diagnostics() });

    private void RestoreRuntimeSettings()
    {
        var processes = Process.GetProcessesByName("witcher3");
        int processId = processes.Length == 1 ? processes[0].Id : 0;
        foreach (var process in processes) process.Dispose();
        if (processId == 0)
        {
            settingsProcessId = 0;
            return;
        }
        if (settingsProcessId == processId) return;
        if (!draft.SaveSwitchesAndMultipliers) { settingsProcessId = processId; return; }
        try
        {
            if (!draft.RuntimeSettingsSaved)
            {
                // Import the existing session once when upgrading an older configuration.
                var current = GameInventoryHooks.Read();
                decimal experience = GameExperience.Read();
                draft.GoldMultiplier = current.Gold;
                draft.GoldMultiplierEnabled = current.Gold != 1;
                draft.KeepItems = current.KeepItems;
                draft.XpMultiplier = experience;
                draft.XpMultiplierEnabled = experience != 1;
                draft.Toggles["ignoreWeight"] = GameWeight.Read();
                draft.Toggles["ignoreEquipmentLevel"] = GameItemScheduler.ReadEquipmentLevelBypass();
                foreach(string feature in AuxiliaryFeatureKeys) draft.Toggles[feature]=ReadAuxiliaryFeature(feature);
                draft.RuntimeSettingsSaved = true;
                DraftStore.Save(draft);
            }
            else
            {
                if(!draft.Toggles.ContainsKey("noFallDamage"))
                {
                    draft.Toggles["noFallDamage"]=GameDebugFacts.Read("noFallDamage");
                    DraftStore.Save(draft);
                }
                GameInventoryHooks.Rebind();
                GameInventoryHooks.Set(draft.GoldMultiplierEnabled ? draft.GoldMultiplier : 1, draft.KeepItems);
                GameExperience.Set(draft.XpMultiplierEnabled ? draft.XpMultiplier : 1);
                GameWeight.Set(draft.Toggles.GetValueOrDefault("ignoreWeight"));
                GameItemScheduler.SetEquipmentLevelBypass(draft.Toggles.GetValueOrDefault("ignoreEquipmentLevel"));
                foreach(string feature in AuxiliaryFeatureKeys) SetAuxiliaryFeature(feature,draft.Toggles.GetValueOrDefault(feature));
                if(draft.Toggles.ContainsKey("catVision")) _ = RestoreCatVisionAsync();
            }
            settingsProcessId = processId;
            status.Text = "已连接游戏，保存的开关与倍率已恢复";
        }
        catch (Exception ex) { status.Text = $"等待恢复保存的设置：{ex.Message}"; }
    }
}
