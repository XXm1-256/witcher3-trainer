namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private sealed record ShortcutDefinition(int Id,string Feature,uint Modifiers,uint Key,string Label);
    private static readonly ShortcutDefinition[] Shortcuts =
    [
        new(0x5741,"unlimitedHealth",0x4000,0x61,"小键盘 1"),
        new(0x5742,"unlimitedStamina",0x4000,0x62,"小键盘 2"),
        new(0x5743,"unlimitedBreath",0x4000,0x63,"小键盘 3"),
        new(0x5744,"noToxicity",0x4000,0x64,"小键盘 4"),
        new(0x5745,"horseStamina",0x4000,0x65,"小键盘 5"),
        new(0x5746,"horseFear",0x4000,0x66,"小键盘 6"),
        new(0x5747,"freeCrafting",0x4000,0x67,"小键盘 7"),
        new(0x5748,"infiniteDurability",0x4000,0x68,"小键盘 8"),
        new(0x5749,"keepItems",0x4000,0x69,"小键盘 9"),
        new(0x5740,"oneHitKill",0x4000,0x60,"小键盘 0"),
        new(0x5751,"teleport",0x4002,0x61,"Ctrl + 小键盘 1"),
        new(0x5752,"ignoreEquipmentLevel",0x4002,0x62,"Ctrl + 小键盘 2"),
        new(0x5753,"ignoreWeight",0x4002,0x63,"Ctrl + 小键盘 3"),
        new(0x5754,"keepAmmo",0x4002,0x64,"Ctrl + 小键盘 4"),
        new(0x5755,"repairEquipment",0x4002,0x65,"Ctrl + 小键盘 5"),
        new(0x5756,"noFallDamage",0x4002,0x66,"Ctrl + 小键盘 6"),
        new(0x5757,"gwentWin",0x4002,0x67,"Ctrl + 小键盘 7"),
        new(0x5758,"resetBuild",0x4002,0x68,"Ctrl + 小键盘 8"),
        new(0x5759,"unlimitedAdrenaline",0x4002,0x69,"Ctrl + 小键盘 9"),
        new(0x5750,"enemySpeed",0x4002,0x60,"Ctrl + 小键盘 0"),
        new(0x5761,"catVision",0x4000,0x6F,"小键盘 /"),
        new(0x5762,"moveSpeed",0x4000,0x6A,"小键盘 *"),
        new(0x5763,"jumpHeight",0x4000,0x6D,"小键盘 -"),
        new(0x5764,"swimSpeed",0x4000,0x6B,"小键盘 +"),
        new(0x5765,"autoLoot",0x4002,0x6B,"Ctrl + 小键盘 +")
    ];
    private readonly Dictionary<string,Control> shortcutControls = [];
    private readonly HashSet<int> registeredHotkeys = [];
    private bool shortcutsReady;
    private nint hotkeyWindow;
    private readonly CheckBox hotkeysToggle=new TrainerSwitch {Text="快捷键",AutoSize=true,Anchor=AnchorStyles.Right};
    private readonly System.Windows.Forms.Timer hotkeyFocusTimer=new() {Interval=100};
    private nint lastHotkeyForeground=-1;

    private void RefreshHotkeyAvailability()
    {
        if(offlinePreview || !shortcutsReady) return;
        nint foreground=GetForegroundWindow();
        if(foreground==lastHotkeyForeground) return;
        lastHotkeyForeground=foreground;
        GetWindowThreadProcessId(foreground,out uint id);
        bool gameForeground=false;
        try {using var process=System.Diagnostics.Process.GetProcessById((int)id);gameForeground=process.ProcessName=="witcher3";}
        catch(ArgumentException) { }
        catch(System.ComponentModel.Win32Exception) { }
        catch(InvalidOperationException) { }
        ApplyHotkeyAvailability(gameForeground);
    }

    private void ApplyHotkeyAvailability(bool gameForeground)
    {
        if(offlinePreview) return;
        if(hotkeysToggle.Checked && gameForeground) RegisterFeatureHotkeys();
        else
        {
            foreach(int id in registeredHotkeys) UnregisterHotKey(hotkeyWindow,id);
            registeredHotkeys.Clear();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint processId);

    private Control WithFeatureShortcut(Control control,string feature)
    {
        shortcutControls.Add(feature,control);
        return WithShortcut(control,Shortcuts.Single(shortcut=>shortcut.Feature==feature).Label);
    }

    private void RegisterFeatureHotkeys()
    {
        if(offlinePreview || !shortcutsReady) return;
        hotkeyWindow=Handle;
        var failed=new List<string>();
        foreach(var shortcut in Shortcuts)
        {
            if(registeredHotkeys.Contains(shortcut.Id) || !shortcutControls.ContainsKey(shortcut.Feature)) continue;
            if(RegisterHotKey(Handle,shortcut.Id,shortcut.Modifiers,shortcut.Key)) registeredHotkeys.Add(shortcut.Id);
            else failed.Add(shortcut.Label);
        }
        if(failed.Count!=0)
        {
            status.Text=$"快捷键注册失败，可能已被其他程序占用：{string.Join("、",failed)}";
            ErrorLog.Write("快捷键注册失败",null,new {Shortcuts=failed});
        }
        ErrorLog.Write("快捷键注册状态",null,new {Registered=registeredHotkeys.Count,Expected=Shortcuts.Length,Window=hotkeyWindow.ToInt64(),Failed=failed});
    }

    private bool ActivateFeatureShortcut(int id)
    {
        if(offlinePreview || !registeredHotkeys.Contains(id)) return false;
        var shortcut=Shortcuts.SingleOrDefault(shortcut=>shortcut.Id==id);
        if(shortcut is null || !shortcutControls.TryGetValue(shortcut.Feature,out var control) || !control.Enabled) return false;
        if(control is CheckBox box) box.Checked=!box.Checked;
        else if(shortcut.Feature=="teleport") _=TeleportToMapPin();
        else if(shortcut.Feature=="repairEquipment") RepairEquipment();
        else if(shortcut.Feature=="gwentWin") _=WinGwent();
        else if(shortcut.Feature=="resetBuild") _=ResetBuild();
        else return false;
        return true;
    }
}
