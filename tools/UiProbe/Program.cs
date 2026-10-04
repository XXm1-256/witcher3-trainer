using System.Reflection;
using System.Text.Json;
using Witcher3Modifier;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "draft.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Draft
        {
            SaveSwitchesAndMultipliers = false, RuntimeSettingsSaved = true,
            GoldMultiplier = 2, XpMultiplier = 1.5m, GoldMultiplierEnabled = true, XpMultiplierEnabled = true, KeepItems = true
        }));
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Console.WriteLine($"WorkingArea: {Screen.PrimaryScreen!.WorkingArea}");
        using var form = new MainForm(offlinePreview: true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        T Field<T>(string name) => (T)typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
        void Guard(string name) => typeof(MainForm).GetField(name, flags)!.SetValue(form, true);
        if(Environment.GetCommandLineArgs().Contains("--item-batch"))
        {
            var catalog=Field<ListBox>("itemCatalog");
            var pending=Field<ListBox>("pendingItems");
            var quantity=Field<NumericUpDown>("itemQuantity");
            var edit=Field<NumericUpDown>("pendingItemQuantity");
            void Queue()=>typeof(MainForm).GetMethod("QueueSelectedItem",flags)!.Invoke(form,null);
            void Pump(Task task)
            {
                var deadline=DateTime.UtcNow.AddSeconds(10);
                while(!task.IsCompleted && DateTime.UtcNow<deadline) { Application.DoEvents();Thread.Sleep(5); }
                if(!task.IsCompleted) throw new Exception("Offline batch did not complete");
                task.GetAwaiter().GetResult();
            }
            Task Submit(Func<string,int,Action,(int Added,int ReturnedIds)> give)=>
                (Task)typeof(MainForm).GetMethod("AddItemBatch",flags)!.Invoke(form,[give])!;
            catalog.SelectedIndex=0;quantity.Value=2;Queue();Queue();
            if(pending.Items.Count!=1 || !pending.Items[0]!.ToString()!.Contains("×4")) throw new Exception("Repeated selection did not merge");
            Field<TextBox>("itemSearch").Text="Linen";
            if(pending.Items.Count!=1) throw new Exception("Search erased the batch");
            catalog.SelectedIndex=0;quantity.Value=3;Queue();
            if(pending.Items.Count!=2) throw new Exception("Second item was not queued");
            edit.Value=5;
            if(!pending.Items[1]!.ToString()!.Contains("×5")) throw new Exception("Per-item quantity edit failed");
            int calls=0;
            Pump(Submit((item,count,wait)=> { if(++calls==2) throw new InvalidOperationException("Offline injected failure");return(count,1); }));
            if(calls!=2 || !pending.Items[0]!.ToString()!.Contains("已添加 4") || !pending.Items[1]!.ToString()!.Contains("结果未确认"))
                throw new Exception("Partial result was not preserved");
            Pump(Submit((item,count,wait)=> { calls++;return(count,1); }));
            if(calls!=2) throw new Exception("Confirmed or uncertain item was submitted twice");
            pending.Items.Clear();typeof(MainForm).GetMethod("RefreshPendingQuantity",flags)!.Invoke(form,null);
            Field<TextBox>("itemSearch").Clear();
            catalog.SelectedIndex=0;quantity.Value=1;Queue();catalog.SelectedIndex=1;Queue();
            calls=0;
            Pump(Submit((item,count,wait)=>
            {
                calls++;
                typeof(MainForm).GetField("stopItemBatchRequested",flags)!.SetValue(form,true);
                return(count,1);
            }));
            if(calls!=1 || !pending.Items[1]!.ToString()!.Contains("待添加")) throw new Exception("Stop did not preserve unsent items");
            Pump(Submit((item,count,wait)=> {calls++;return(count,1); }));
            if(calls!=2 || Field<Button>("giveItem").Enabled) throw new Exception("Continue repeated completed items");
            form.Size=new Size(1715,1131);form.CreateControl();form.PerformLayout();
            var batchTabs=form.Controls.OfType<TrainerTabs>().Single();batchTabs.SelectedIndex=1;batchTabs.CreateControl();batchTabs.PerformLayout();
            var page=batchTabs.TabPages[1];Size size=page.Size;page.Parent=null;page.Dock=DockStyle.None;page.Size=size;page.CreateControl();page.PerformLayout();
            using var bitmap=new Bitmap(page.Width,page.Height);page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/item-batch-offline.png")));
            Console.WriteLine("Batch merge/search retention/per-item quantity/partial failure/no duplicate/stop and continue passed; hidden render, no game access");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--help-copy"))
        {
            var helpTips=Field<ToolTip>("usageTips");
            IEnumerable<Control> Walk(Control parent)
            {
                foreach(Control child in parent.Controls){yield return child;foreach(var nested in Walk(child)) yield return nested;}
            }
            var helpTexts=Walk(form).Select(control=>new {Control=control.GetType().Name,Text=helpTips.GetToolTip(control)})
                .Where(entry=>!string.IsNullOrEmpty(entry.Text)).ToArray();
            foreach(var entry in helpTexts)
                if(entry.Text!.Length>200 || new[]{"原生","来源","后台","种子","开发中"}.Any(entry.Text.Contains))
                    throw new Exception("Player help contains implementation wording or excessive length: "+entry.Text);
            if(!helpTexts.Any(entry=>entry.Text!.Contains("偷窃") && entry.Text.Contains("任务")) ||
                !helpTexts.Any(entry=>entry.Text!.Contains("空中") && entry.Text.Contains("楼层") && entry.Text.Contains("无落地伤害")))
                throw new Exception("Player safety instructions were lost");
            File.WriteAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/PLAYER_HELP_TEXTS_20261002.json")),JsonSerializer.Serialize(helpTexts,new JsonSerializerOptions {WriteIndented=true}));
            Console.WriteLine($"Player help copy checks passed: {helpTexts.Length} controls; no game access");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--tooltip-wrap"))
        {
            var tip=Field<ToolTip>("usageTips");
            var measure=typeof(MainForm).GetMethod("MeasureUsageTip",BindingFlags.Static|BindingFlags.NonPublic)!;
            var position=typeof(MainForm).GetMethod("UsageTipPosition",BindingFlags.Static|BindingFlags.NonPublic)!;
            var screenArea=new Rectangle(0,0,1920,1080);
            var normalPoint=(Point)position.Invoke(null,[new Rectangle(100,100,300,40),new Size(520,180),screenArea])!;
            if(normalPoint!=new Point(100,146)) throw new Exception("Help did not anchor below its function");
            var edgePoint=(Point)position.Invoke(null,[new Rectangle(1800,1000,100,40),new Size(520,180),screenArea])!;
            if(!screenArea.Contains(new Rectangle(edgePoint,new Size(520,180)))) throw new Exception("Help escaped screen edge");
            string longTip="传送到当前区域的地图标记。关闭地图和暂停菜单后生效；多层建筑无法指定楼层。落点可能在空中，建议开启“无落地伤害”。若停在空中或角色不显示，换一个标记再传送即可。";
            foreach(int width in new[]{320,520,780})
            {
                var measured=(Size)measure.Invoke(null,[longTip,form.Font,width])!;
                if(measured.Width>width || measured.Height<=form.Font.Height*2)
                    throw new Exception("Long tooltip did not wrap within its width");
                var shortSize=(Size)measure.Invoke(null,["关闭后停止新拾取",form.Font,width])!;
                if(shortSize.Height>=measured.Height) throw new Exception("Tooltip height did not follow content");
            }
            using var target=new Label();tip.SetToolTip(target,longTip);
            var popup=new PopupEventArgs(target,target,false,Size.Empty);
            typeof(ToolTip).GetMethod("OnPopup",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(tip,[popup]);
            using var bitmap=new Bitmap(popup.ToolTipSize.Width,popup.ToolTipSize.Height);
            using var graphics=Graphics.FromImage(bitmap);
            var draw=new DrawToolTipEventArgs(graphics,target,target,new Rectangle(Point.Empty,bitmap.Size),longTip,tip.BackColor,tip.ForeColor,form.Font);
            typeof(ToolTip).GetMethod("OnDraw",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(tip,[draw]);
            bitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/tooltip-wrap-offline.png")));
            Console.WriteLine($"Tooltip wrapping and popup/draw pipeline passed: {bitmap.Size}; no game access");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--autoloot-layout"))
        {
            form.CreateControl();form.PerformLayout();
            var lootTabs=form.Controls.OfType<TrainerTabs>().Single();
            lootTabs.SelectedIndex=0;lootTabs.CreateControl();lootTabs.PerformLayout();
            var lootPage=lootTabs.TabPages[0];Size lootSize=lootPage.Size;
            lootPage.Parent=null;lootPage.Dock=DockStyle.None;lootPage.Size=lootSize;
            lootPage.CreateControl();lootPage.PerformLayout();
            using var lootBitmap=new Bitmap(lootPage.Width,lootPage.Height);
            lootPage.DrawToBitmap(lootBitmap,new Rectangle(Point.Empty,lootPage.Size));
            lootBitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/layout-autoloot-offline.png")));
            Console.WriteLine("Auto loot home rendered offline; no game access");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--autoloot-settings"))
        {
            Field<CheckBox>("saveSettingsToggle").Checked=true;
            Field<CheckBox>("autoLootToggle").Checked=true;
            using(var saved=new MainForm(offlinePreview:true))
                if(!((CheckBox)typeof(MainForm).GetField("autoLootToggle",flags)!.GetValue(saved)!).Checked)
                    throw new Exception("Auto loot saved switch did not restore");
            Field<CheckBox>("saveSettingsToggle").Checked=false;
            Field<CheckBox>("autoLootToggle").Checked=false;
            if(!JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!.Toggles["autoLoot"])
                throw new Exception("Disabled saving overwrote auto loot preference");
            using(var autoLootDisabled=new MainForm(offlinePreview:true))
                if(((CheckBox)typeof(MainForm).GetField("autoLootToggle",flags)!.GetValue(autoLootDisabled)!).Checked)
                    throw new Exception("Auto loot restored while preference saving was disabled");
            Console.WriteLine("Auto loot switch save/reopen/disabled-save passed; no game access");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--motion-settings"))
        {
            Field<CheckBox>("saveSettingsToggle").Checked=true;
            Field<NumericUpDown>("moveSpeedValue").Value=9.25m;
            Field<NumericUpDown>("jumpHeightValue").Value=8.75m;
            Field<NumericUpDown>("swimSpeedValue").Value=7.5m;
            Field<CheckBox>("moveSpeedToggle").Checked=true;
            Field<CheckBox>("jumpHeightToggle").Checked=true;
            Field<CheckBox>("swimSpeedToggle").Checked=true;
            typeof(MainForm).GetMethod("SaveRuntimeSettings",flags)!.Invoke(form,null);
            using(var saved=JsonDocument.Parse(File.ReadAllText(path)))
                if(saved.RootElement.GetProperty("MoveSpeedMultiplier").GetDecimal()!=9.25m || saved.RootElement.GetProperty("JumpHeightMultiplier").GetDecimal()!=8.75m || saved.RootElement.GetProperty("SwimSpeedMultiplier").GetDecimal()!=7.5m ||
                    !saved.RootElement.GetProperty("Toggles").GetProperty("moveSpeed").GetBoolean() || !saved.RootElement.GetProperty("Toggles").GetProperty("jumpHeight").GetBoolean() || !saved.RootElement.GetProperty("Toggles").GetProperty("swimSpeed").GetBoolean())
                    throw new Exception("Player motion settings were not saved");
            using(var motionReopened=new MainForm(offlinePreview:true))
                if(((NumericUpDown)typeof(MainForm).GetField("moveSpeedValue",flags)!.GetValue(motionReopened)!).Value!=9.25m || !((CheckBox)typeof(MainForm).GetField("jumpHeightToggle",flags)!.GetValue(motionReopened)!).Checked || ((NumericUpDown)typeof(MainForm).GetField("swimSpeedValue",flags)!.GetValue(motionReopened)!).Value!=7.5m || !((CheckBox)typeof(MainForm).GetField("swimSpeedToggle",flags)!.GetValue(motionReopened)!).Checked)
                    throw new Exception("Player motion settings did not restore on reopen");
            Field<CheckBox>("saveSettingsToggle").Checked=false;
            Field<NumericUpDown>("moveSpeedValue").Value=2.75m;
            Field<NumericUpDown>("swimSpeedValue").Value=10m;
            typeof(MainForm).GetMethod("SaveRuntimeSettings",flags)!.Invoke(form,null);
            using(var motionDisabled=JsonDocument.Parse(File.ReadAllText(path)))
                if(motionDisabled.RootElement.GetProperty("MoveSpeedMultiplier").GetDecimal()!=9.25m || motionDisabled.RootElement.GetProperty("SwimSpeedMultiplier").GetDecimal()!=7.5m) throw new Exception("Disabled save overwrote motion multiplier");
            Console.WriteLine("Player motion multipliers/toggles save, reopen restore and motionDisabled-save preservation passed; no game calls");
            return;
        }
        if(Environment.GetCommandLineArgs().Contains("--hotkey-lifecycle"))
        {
            // No window is shown or message loop pumped. Only temporary OS
            // registrations are exercised; feature callbacks are never invoked.
            _=form.Handle;
            var preview=typeof(MainForm).GetField("offlinePreview",flags)!;
            var register=typeof(MainForm).GetMethod("RegisterFeatureHotkeys",flags)!;
            var registered=Field<HashSet<int>>("registeredHotkeys");
            var controls=Field<Dictionary<string,Control>>("shortcutControls");
            var recreate=typeof(Control).GetMethod("RecreateHandle",BindingFlags.Instance|BindingFlags.NonPublic)!;
            controls["noFallDamage"].Enabled=false;
            preview.SetValue(form,false);
            try
            {
                register.Invoke(form,null);
                if(registered.Count!=25) throw new Exception("Not all system hotkeys registered");
                register.Invoke(form,null);
                if(registered.Count!=25) throw new Exception("Repeated registration lost hotkeys");
                recreate.Invoke(form,null);
                register.Invoke(form,null);
                if(registered.Count!=25 || Field<nint>("hotkeyWindow")!=form.Handle)
                    throw new Exception("Handle recreation lost system hotkeys");
                var applyAvailability=typeof(MainForm).GetMethod("ApplyHotkeyAvailability",flags)!;
                applyAvailability.Invoke(form,[false]);
                if(registered.Count!=0) throw new Exception("Typing outside the game kept hotkeys registered");
                applyAvailability.Invoke(form,[true]);
                if(registered.Count!=25) throw new Exception("Game focus did not restore hotkeys");
                Field<CheckBox>("hotkeysToggle").Checked=false;
                applyAvailability.Invoke(form,[true]);
                if(registered.Count!=0) throw new Exception("Hotkeys off did not release OS registrations");
                preview.SetValue(form,true);
                form.Dispose();
                if(registered.Count!=0 || Field<nint>("hotkeyWindow")!=0)
                    throw new Exception("Disposed handle kept system hotkeys");
            }
            finally {preview.SetValue(form,true);}
            Console.WriteLine("25 real Windows hotkeys: startup registration, disabled control, repeated registration, handle recreation and disposal passed; no shown window, message loop or game calls");
            return;
        }
        var tabs = form.Controls.OfType<TrainerTabs>().Single();
        if (form.FormBorderStyle != FormBorderStyle.None) throw new Exception("Unexpected native frame");
        if (tabs.TabPages[0].Text != "常用功能" || tabs.TabPages.Count != 5 || tabs.TabPages[4].Text != "趣味实验室" || tabs.TabPages.Cast<Panel>().Any(page => page.Text == "总览"))
            throw new Exception("Home structure failed");
        var save = Field<CheckBox>("saveSettingsToggle");
        if (save.Text != "保存开关与倍率" || save.Parent?.Parent != form) throw new Exception("Save toggle must remain outside the tab pages");
        Guard("refreshingInventoryHooks"); Guard("refreshingExperience"); Guard("refreshingWeight");
        var list = Field<CheckedListBox>("affixes");
        int EffectIndex(CheckedListBox target,string id)=>target.Items.Cast<object>().Select((effect,index)=>(effect,index)).Single(pair=>pair.effect.GetType().GetProperty("Id")!.GetValue(pair.effect) as string==id).index;
        int armorOnly=EffectIndex(list,"MA_SlashingResistance");
        list.SetItemChecked(armorOnly,true);
        if(list.GetItemChecked(armorOnly)) throw new Exception("Sword accepted armor-only affix");
        if(string.IsNullOrEmpty(Field<ToolTip>("usageTips").GetToolTip(list))) throw new Exception("Affix compatibility hint missing");
        int attackIndex=list.Items.Cast<object>().Select((effect,index)=>(effect,index)).Single(pair=>pair.effect.GetType().GetProperty("Id")!.GetValue(pair.effect) as string=="MA_AttackPowerMult").index;
        list.SetItemChecked(attackIndex, true);
        object selected = list.Items[attackIndex];
        Field<TextBox>("affixSearch").Text = "不匹配的搜索";
        Field<TextBox>("affixSearch").Text = "";
        if (!list.CheckedItems.Contains(selected)) throw new Exception("Search lost checked effect");
        if(Environment.GetCommandLineArgs().Contains("--quiet"))
        {
            var definitions=(Array)typeof(MainForm).GetField("Shortcuts",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            string[] expectedFeatures=["unlimitedHealth","unlimitedStamina","unlimitedBreath","noToxicity","horseStamina","horseFear","freeCrafting","infiniteDurability","keepItems","oneHitKill","teleport","ignoreEquipmentLevel","ignoreWeight","keepAmmo","repairEquipment","noFallDamage","gwentWin","resetBuild","unlimitedAdrenaline","enemySpeed","catVision","moveSpeed","jumpHeight","swimSpeed","autoLoot"];
            uint[] expectedKeys=[0x61,0x62,0x63,0x64,0x65,0x66,0x67,0x68,0x69,0x60,0x61,0x62,0x63,0x64,0x65,0x66,0x67,0x68,0x69,0x60,0x6F,0x6A,0x6D,0x6B,0x6B];
            var mistletoe=((System.Collections.IEnumerable)typeof(MainForm).GetField("availableItems",flags)!.GetValue(form)!).Cast<object>()
                .Single(item=>(string)item.GetType().GetProperty("Name")!.GetValue(item)! =="Mistletoe");
            bool MistletoeMatches(string query,bool fuzzy)=>(bool)mistletoe.GetType().GetMethod("Matches",flags)!.Invoke(mistletoe,[query,fuzzy])!;
            if(!MistletoeMatches("榭寄生",true) || MistletoeMatches("榭寄生",false) || MistletoeMatches("错误生",true))
                throw new Exception("Chinese one-character typo search failed");
            if(Field<CheckBox>("catVisionToggle").Parent!.Parent!.Parent!.Text=="趣味实验室") throw new Exception("Cat vision was not moved home");
            var mapped=Field<Dictionary<string,Control>>("shortcutControls");
            var ids=new HashSet<int>();
            for(int index=0;index<definitions.Length;index++)
            {
                object definition=definitions.GetValue(index)!;
                object Property(string name)=>definition.GetType().GetProperty(name)!.GetValue(definition)!;
                string feature=(string)Property("Feature");
                if(feature!=expectedFeatures[index] || (uint)Property("Key")!=expectedKeys[index] ||
                    (uint)Property("Modifiers")!=(index<10?0x4000u:index<20 || index==24?0x4002u:0x4000u) || !ids.Add((int)Property("Id")))
                    throw new Exception("Numpad shortcut mapping is invalid");
                Control control=mapped[feature];
                var wrapper=control.Parent!;
                var rows=(TableLayoutPanel)wrapper.Parent!;
                int row=rows.GetRow(wrapper);
                if(row!=3+index/2 || rows.GetColumn(wrapper)!=index%2 || !wrapper.Controls.Cast<Control>().Any(child=>child.Text==(string)Property("Label")))
                    throw new Exception("Shortcut labels or home order disagree with the mapping");
                var help=wrapper.Controls.Cast<Control>().Single(child=>child.GetType().Name=="HelpIcon");
                if(rows.ColumnCount!=2 || string.IsNullOrEmpty(Field<ToolTip>("usageTips").GetToolTip(help)) ||
                    wrapper.Controls.GetChildIndex(help)!=(index is 19 or 21 or 22 or 23?2:1))
                    throw new Exception("Inline help icon or tooltip is missing");
            }
            var homeRows=(TableLayoutPanel)mapped["unlimitedHealth"].Parent!.Parent!;
            if(homeRows.Controls.Cast<Control>().Where(control=>homeRows.GetRow(control)<3).Any(control=>homeRows.GetColumnSpan(control)!=2))
                throw new Exception("Money and multipliers must span both home columns");
            form.Size=new Size(700,800);
            form.PerformLayout();
            if(homeRows.ColumnCount!=1 || definitions.Cast<object>().Select((_,index)=>mapped[expectedFeatures[index]].Parent!).Where((control,index)=>homeRows.GetRow(control)!=3+index).Any())
                throw new Exception("Narrow home must preserve order in one column");
            form.Size=new Size(1715,1131);
            form.PerformLayout();
            if(homeRows.ColumnCount!=2) throw new Exception("Wide home did not restore two columns");
            foreach(var size in new[]{new Size(1715,1131),Screen.PrimaryScreen!.WorkingArea.Size})
            {
                form.WindowState=size.Width>1715?FormWindowState.Maximized:FormWindowState.Normal;
                form.Size=size;
                typeof(MainForm).GetMethod("UpdateContentScale",flags)!.Invoke(form,null);
                form.PerformLayout();
                var enemyRow=mapped["enemySpeed"].Parent!;
                enemyRow.PerformLayout();
                foreach(Control child in enemyRow.Controls)
                    if(child.Bottom>enemyRow.ClientSize.Height || child.Right>enemyRow.ClientSize.Width || child.Top<0 || child.Left<0)
                        throw new Exception("Enemy speed row clips a control at "+size+": "+child.Text);
                if(mapped["enemySpeed"].Height<mapped["enemySpeed"].GetPreferredSize(Size.Empty).Height)
                    throw new Exception("Enemy speed switch height is cropped");
            }
            form.WindowState=FormWindowState.Normal;
            form.Size=new Size(1715,1131);
            typeof(MainForm).GetMethod("UpdateContentScale",flags)!.Invoke(form,null);
            form.PerformLayout();
            var enemyRender=mapped["enemySpeed"].Parent!;
            var enemyPosition=homeRows.GetPositionFromControl(enemyRender);
            homeRows.Controls.Remove(enemyRender);
            enemyRender.CreateControl();enemyRender.PerformLayout();
            using(var bitmap=new Bitmap(enemyRender.Width,enemyRender.Height))
            {
                enemyRender.DrawToBitmap(bitmap,new Rectangle(Point.Empty,enemyRender.Size));
                bitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/layout-enemy-row.png")));
            }
            homeRows.Controls.Add(enemyRender,enemyPosition.Column,enemyPosition.Row);
            foreach(string feature in new[]{"catVision","moveSpeed","jumpHeight","swimSpeed"})
            {
                var render=mapped[feature].Parent!;
                var position=homeRows.GetPositionFromControl(render);
                homeRows.Controls.Remove(render);
                render.CreateControl();render.PerformLayout();
                if(render.Controls.Cast<Control>().Any(child=>child.Right>render.ClientSize.Width+2 || child.Bottom>render.ClientSize.Height+2))
                    throw new Exception("Player motion controls are clipped");
                using(var bitmap=new Bitmap(render.Width,render.Height))
                {
                    render.DrawToBitmap(bitmap,new Rectangle(Point.Empty,render.Size));
                    bitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/layout-"+feature+"-20261002.png")));
                }
                homeRows.Controls.Add(render,position.Column,position.Row);
            }
            Console.WriteLine("Enemy speed control bounds fit normal and screen-sized windows");
            if(Field<HashSet<int>>("registeredHotkeys").Count!=0 || (bool)typeof(MainForm).GetMethod("ActivateFeatureShortcut",flags)!.Invoke(form,[0x5749])!)
                throw new Exception("Offline preview must not register or dispatch game hotkeys");
            var teleportType=typeof(MainForm).Assembly.GetType("Witcher3Modifier.GameTeleport")!;
            var legacyTemplate=(byte[])teleportType.GetField("LegacyTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            var newTemplate=(byte[])teleportType.GetField("Template",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            var buildTeleport=teleportType.GetMethod("BuildCode",BindingFlags.Static|BindingFlags.NonPublic,null,[typeof(long),typeof(byte[])],null)!;
            var legacyCode=(byte[])buildTeleport.Invoke(null,[0x140000000L,legacyTemplate])!;
            var installedLegacy=new byte[newTemplate.Length];
            legacyCode.CopyTo(installedLegacy,0);
            var acceptsLegacy=teleportType.GetMethod("IsLegacyCode",BindingFlags.Static|BindingFlags.NonPublic)!;
            bool Accepted(byte[] code)=>(bool)acceptsLegacy.Invoke(null,[code,0x140000000L])!;
            if(!Accepted(installedLegacy)) throw new Exception("Known teleport job upgrade was rejected");
            var previousTemplate=(byte[])teleportType.GetField("PreviousTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            var previousCode=(byte[])buildTeleport.Invoke(null,[0x140000000L,previousTemplate])!;
            var previousInstalled=new byte[newTemplate.Length]; previousCode.CopyTo(previousInstalled,0);
            if(!Accepted(previousInstalled)) throw new Exception("Previous nearby teleport job upgrade was rejected");
            previousInstalled[previousCode.Length]=1;
            if(Accepted(previousInstalled) || newTemplate.Length>0x600) throw new Exception("Teleport upgrade/size guards failed");
            installedLegacy[^1]=1;
            if(Accepted(installedLegacy)) throw new Exception("Unexpected code after the legacy job was accepted");
            installedLegacy[^1]=0; installedLegacy[3]^=1;
            if(Accepted(installedLegacy) || Accepted(new byte[4])) throw new Exception("Unknown teleport code was accepted");
            var knownTeleport=teleportType.GetMethod("IsKnownCode",BindingFlags.Static|BindingFlags.NonPublic)!;
            var stageTemplate=(byte[])teleportType.GetField("StageTemplate",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            foreach(var template in new[]{newTemplate,stageTemplate,previousTemplate})
            {
                var slot=new byte[0x600];
                ((byte[])buildTeleport.Invoke(null,[0x140000000L,template])!).CopyTo(slot,0);
                if(!(bool)knownTeleport.Invoke(null,[slot,0x140000000L])!) throw new Exception("Known teleport slot rejected");
                slot[^1]=1;
                if((bool)knownTeleport.Invoke(null,[slot,0x140000000L])!) throw new Exception("Unknown teleport slot tail accepted");
            }
            var equipmentSwitch=Field<CheckBox>("equipmentLevelToggle");
            equipmentSwitch.Checked=true;
            var factSwitches=Field<Dictionary<string,CheckBox>>("debugFactToggles");
            foreach(var box in factSwitches.Values) box.Checked=true;
            save.Checked=true;
            var saved=JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
            if(!saved.Toggles.GetValueOrDefault("ignoreEquipmentLevel")) throw new Exception("Equipment level preference was not saved");
            if(factSwitches.Count!=12 || factSwitches.Keys.Any(key=>!saved.Toggles.GetValueOrDefault(key)))
                throw new Exception("Auxiliary preferences were not saved");
            var quietNumbers=Field<Dictionary<string,(CheckBox Use,NumericUpDown Value)>>("numericFields");
            var tips=Field<ToolTip>("usageTips");
            foreach(var group in new[]{quietNumbers,Field<Dictionary<string,(CheckBox Use,NumericUpDown Value)>>("editNumericFields")})
                foreach(string id in new[]{"damage","armor"})
                    if(tips.GetToolTip(group[id].Use)?.Contains("需要等级")!=true)
                        throw new Exception("Equipment level limitation hint missing");
            if(tips.GetToolTip(quietNumbers["damage"].Value)?.Contains("需要等级")!=true || tips.GetToolTip(quietNumbers["armor"].Value)?.Contains("需要等级")!=true)
                throw new Exception("Generated equipment numeric hint missing");
            foreach(string name in new[]{"Burning Rose Sword","Bear Armor 4","DLC1 Temerian Armor"})
            {
                Field<TextBox>("itemSearch").Text=name;
                var items=Field<ListBox>("itemCatalog").Items.Cast<object>();
                var found=items.Single(item=>item.GetType().GetProperty("Name")!.GetValue(item) as string==name);
                string chinese=(string)found.GetType().GetProperty("DisplayName")!.GetValue(found)!;
                Field<TextBox>("itemSearch").Text=chinese;
                if(!Field<ListBox>("itemCatalog").Items.Contains(found)) throw new Exception("DLC Chinese search missing");
            }
            Field<TextBox>("itemSearch").Text="";
            Field<ComboBox>("itemLevel").SelectedIndex=12;
            Field<TextBox>("itemSearch").Text="白虎";
            if(Field<ListBox>("itemCatalog").Items.Count!=8) throw new Exception("Level and Chinese item search did not combine");
            Field<ComboBox>("itemLevel").SelectedIndex=13;
            if(Field<ListBox>("itemCatalog").Items.Count!=0) throw new Exception("Item level filter accepted another level");
            Field<ComboBox>("itemLevel").SelectedIndex=0;
            Field<TextBox>("itemSearch").Text="";
            Field<ComboBox>("gearLevel").SelectedIndex=12;
            Field<TextBox>("gearSearch").Text="Steel Vixen";
            if(Field<ComboBox>("gearBase").Items.Count<1) throw new Exception("Base equipment level and name search did not combine");
            quietNumbers["damage"].Use.Checked=true;
            quietNumbers["damage"].Value.Value=129;
            if(!Field<TextBox>("gearPreview").Text.Contains("11 级 → 修改后预估 12 级")) throw new Exception("Generated level estimate missing or incorrect");
            quietNumbers["damage"].Use.Checked=false;
            Field<ComboBox>("gearLevel").SelectedIndex=0;
            Field<TextBox>("gearSearch").Text="";
            Console.WriteLine("Catalog/base level-name filters and numeric level estimate passed; no game access");
            if(quietNumbers.Count!=31 || Field<Dictionary<string,(CheckBox Use,NumericUpDown Value)>>("editNumericFields").Count!=31)
                throw new Exception("Both pages must include all numeric affix fields");
            var effectList=Field<CheckedListBox>("affixes");
            effectList.SelectedIndex=effectList.Items.Cast<object>().Select((effect,index)=>(effect,index)).Single(pair=>pair.effect.GetType().GetProperty("Id")!.GetValue(pair.effect) as string=="MA_AardIntensity").index;
            var targetToggle=(CheckBox)effectList.Parent!.Controls.Find("affixTargetToggle",true).Single();
            var targetValue=(NumericUpDown)effectList.Parent!.Controls.Find("affixTargetValue",true).Single();
            targetToggle.Checked=true;
            targetValue.Value=35;
            if(!quietNumbers["aard"].Use.Checked || quietNumbers["aard"].Value.Value!=35 || !targetValue.Enabled)
                throw new Exception("Affix editor did not update its numeric target");
            Field<TextBox>("affixSearch").Text="不匹配";
            Field<TextBox>("affixSearch").Text="";
            if(!quietNumbers["aard"].Use.Checked || quietNumbers["aard"].Value.Value!=35)
                throw new Exception("Affix search lost its numeric target");
            quietNumbers["poison"].Use.Checked=true;
            quietNumbers["poison"].Value.Value=35;
            typeof(MainForm).GetMethod("AddGear",flags)!.Invoke(form,null);
            saved=JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
            if(saved.Gear.Last().NumericTargets.GetValueOrDefault("poison")!=35 || saved.Gear.Last().NumericTargets.GetValueOrDefault("aard")!=35) throw new Exception("Numeric target was not saved");
            var editNumbers=Field<Dictionary<string,(CheckBox Use,NumericUpDown Value)>>("editNumericFields");
            var itemType=typeof(MainForm).Assembly.GetType("Witcher3Modifier.InventoryItem")!;
            var templateType=typeof(MainForm).Assembly.GetType("Witcher3Modifier.GameItem")!;
            var inventory=Field<ListBox>("inventoryList");
            if(inventory.SelectionMode!=SelectionMode.MultiExtended) throw new Exception("Inventory multi-selection missing");
            object fakeTemplate=Activator.CreateInstance(templateType)!;
            templateType.GetProperty("Category")!.SetValue(fakeTemplate,"steelsword");
            var snapshot=(System.Collections.IList)typeof(MainForm).GetField("inventorySnapshot",flags)!.GetValue(form)!;
            snapshot.Add(Activator.CreateInstance(itemType,[0,0L,901,0,3,"offline-first",fakeTemplate])!);
            snapshot.Add(Activator.CreateInstance(itemType,[0,0L,902,0,7,"offline-second",fakeTemplate])!);
            typeof(MainForm).GetMethod("FilterInventory",flags)!.Invoke(form,null);
            inventory.SetSelected(0,true); inventory.SetSelected(1,true);
            if(inventory.SelectedItems.Count!=2 || Field<Button>("inspectEquipment").Enabled || !Field<Button>("deleteItem").Enabled || Field<NumericUpDown>("deleteQuantity").Maximum!=7)
                throw new Exception("Multi-selection deletion or single equipment guard failed");
            typeof(MainForm).GetMethod("FilterInventory",flags)!.Invoke(form,null);
            if(inventory.SelectedItems.Count!=2) throw new Exception("Filtering lost visible selected items");
            Field<TextBox>("inventorySearch").Text="offline-first";
            if(inventory.SelectedItems.Count!=1 || !Field<Button>("inspectEquipment").Enabled || Field<NumericUpDown>("deleteQuantity").Maximum!=3)
                throw new Exception("Filtered single equipment state failed");
            Field<TextBox>("inventorySearch").Text="";
            var levelType=typeof(MainForm).Assembly.GetType("Witcher3Modifier.GameEquipmentLevels")!;
            var profileType=levelType.GetNestedType("Profile",BindingFlags.NonPublic)!;
            var liveType=levelType.GetNestedType("Live",BindingFlags.NonPublic)!;
            var cache=(System.Collections.IDictionary)typeof(MainForm).GetField("inventoryLevels",flags)!.GetValue(form)!;
            cache.Add(901,Activator.CreateInstance(liveType,[11,Activator.CreateInstance(profileType)!])!);
            cache.Add(902,Activator.CreateInstance(liveType,[12,Activator.CreateInstance(profileType)!])!);
            Field<ComboBox>("inventoryLevel").SelectedIndex=12;
            if(inventory.Items.Count!=1 || itemType.GetProperty("UniqueId")!.GetValue(inventory.Items[0]) as int?!=901) throw new Exception("Backpack actual level filter failed");
            Field<TextBox>("inventorySearch").Text="offline-second";
            if(inventory.Items.Count!=0) throw new Exception("Backpack level/name intersection failed");
            Field<ComboBox>("inventoryLevel").SelectedIndex=13;
            if(inventory.Items.Count!=1) throw new Exception("Backpack level switch failed");
            Field<ComboBox>("inventoryLevel").SelectedIndex=0;
            var typeFilter=Field<ComboBox>("inventoryType");
            int TypeIndex(string label)=>Enumerable.Range(0,typeFilter.Items.Count).Single(i=>typeFilter.Items[i]!.ToString()==label);
            object armorTemplate=Activator.CreateInstance(templateType)!;templateType.GetProperty("Category")!.SetValue(armorTemplate,"armor");
            object materialTemplate=Activator.CreateInstance(templateType)!;templateType.GetProperty("Category")!.SetValue(materialTemplate,"crafting_ingredient");
            snapshot.Add(Activator.CreateInstance(itemType,[0,0L,903,0,2,"offline-armor",armorTemplate])!);
            snapshot.Add(Activator.CreateInstance(itemType,[0,0L,904,0,8,"offline-material",materialTemplate])!);
            Field<TextBox>("inventorySearch").Text="";
            typeFilter.SelectedIndex=TypeIndex("全部武器");
            if(inventory.Items.Count!=2) throw new Exception("Aggregate weapon filter failed");
            inventory.SetSelected(0,true);inventory.SetSelected(1,true);
            typeFilter.SelectedIndex=TypeIndex("制作材料");
            if(inventory.Items.Count!=1 || inventory.SelectedItems.Count!=0 || Field<Button>("deleteItem").Enabled) throw new Exception("Hidden weapon selection leaked into filtered deletion");
            typeFilter.SelectedIndex=TypeIndex("全部护甲");
            if(inventory.Items.Count!=1 || itemType.GetProperty("UniqueId")!.GetValue(inventory.Items[0]) as int?!=903) throw new Exception("Armor type filter failed");
            typeFilter.SelectedIndex=TypeIndex("钢剑");
            Field<TextBox>("inventorySearch").Text="offline-first";
            Field<ComboBox>("inventoryLevel").SelectedIndex=12;
            if(inventory.Items.Count!=1) throw new Exception("Type/name/level intersection failed");
            typeFilter.SelectedIndex=TypeIndex("胸甲");
            if(inventory.Items.Count!=0) throw new Exception("Type filter ignored active name and level");
            Field<ComboBox>("inventoryLevel").SelectedIndex=0;
            typeFilter.SelectedIndex=0;
            typeof(MainForm).GetMethod("SetInventoryBusy",flags)!.Invoke(form,[true]);
            if(typeFilter.Enabled) throw new Exception("Type filter remained editable during inventory operation");
            typeof(MainForm).GetMethod("SetInventoryBusy",flags)!.Invoke(form,[false]);
            cache.Clear();
            snapshot.Clear(); Field<TextBox>("inventorySearch").Text="";
            Console.WriteLine("Inventory multi-select, visible selection preservation, quantity bounds and single equipment guard passed; no game calls");
            void LoadFakeEquipment(string category,Dictionary<string,decimal> values)
            {
                object template=Activator.CreateInstance(templateType)!;
                templateType.GetProperty("Category")!.SetValue(template,category);
                object item=Activator.CreateInstance(itemType,[0,0L,406,0,1,"offline-equipment",template])!;
                typeof(MainForm).GetField("loadedEquipment",flags)!.SetValue(form,item);
                typeof(MainForm).GetMethod("LoadEditNumericValues",flags)!.Invoke(form,[values,null]);
                typeof(MainForm).GetMethod("SetInventoryBusy",flags)!.Invoke(form,[false]);
            }
            LoadFakeEquipment("steelsword",new(){{"damage",63},{"poison",25}});
            if(tips.GetToolTip(editNumbers["damage"].Value)?.Contains("需要等级")!=true) throw new Exception("Loading equipment erased level hint");
            var editList=Field<CheckedListBox>("editAffixes");
            int editArmorOnly=EffectIndex(editList,"MA_SlashingResistance");
            editList.SetItemChecked(editArmorOnly,true);
            if(editList.GetItemChecked(editArmorOnly) || editNumbers["slashingResistance"].Use.Enabled)
                throw new Exception("Backpack sword accepted armor-only affix");
            editList.SelectedIndex=EffectIndex(editList,"MA_CriticalChance");
            var editTargetToggle=(CheckBox)editList.Parent!.Controls.Find("affixTargetToggle",true).Single();
            var nearest=(Button)editList.Parent.Controls.Find("affixNearest",true).Single();
            editTargetToggle.Checked=true;
            if(!nearest.Enabled || string.IsNullOrEmpty(Field<ToolTip>("usageTips").GetToolTip(nearest)))
                throw new Exception("Seed affix reachable target control missing");
            editTargetToggle.Checked=false;
            if(editNumbers["poison"].Value.Value!=25 || editNumbers["poison"].Use.Checked || !editNumbers["poison"].Use.Enabled || editNumbers["armor"].Use.Enabled)
                throw new Exception("Sword readback or field applicability failed");
            editNumbers["poison"].Use.Checked=true;
            editNumbers["poison"].Value.Value=29;
            editNumbers["aard"].Use.Checked=true;
            editNumbers["aard"].Value.Value=20;
            string preview=Field<TextBox>("editPreview").Text;
            if(!preview.Contains("25 → 目标 29") || !preview.Contains("配置已更新") || !Field<Button>("checkEquipment").Enabled)
                throw new Exception("Inventory before/target preview or check action missing");
            var equipmentType=typeof(MainForm).Assembly.GetType("Witcher3Modifier.GameEquipment")!;
            var validate=equipmentType.GetMethod("ValidateConfiguration",BindingFlags.Static|BindingFlags.NonPublic)!;
            var catalogMethod=templateType.GetMethod("LoadCatalog",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!;
            int dlcEquipment=0;
            foreach(object template in (System.Collections.IEnumerable)catalogMethod.Invoke(null,null)!)
            {
                string source=(string)templateType.GetProperty("Source")!.GetValue(template)!;
                string category=(string)templateType.GetProperty("Category")!.GetValue(template)!;
                if(source is not ("ep1" or "bob" or "dlc0") || category is not ("steelsword" or "silversword" or "armor" or "boots" or "pants" or "gloves" or "crossbow")) continue;
                validate.Invoke(null,[category,Array.Empty<string>(),"",0,new Dictionary<string,decimal>()]);
                dlcEquipment++;
            }
            if(dlcEquipment<100) throw new Exception("DLC equipment type coverage unexpectedly small");
            foreach(var invalid in new[]{("steelsword",new[]{"MA_SlashingResistance"},""),("armor",new[]{"MA_PoisonChance"},""),("crossbow",new[]{"MA_CriticalChance"},"")})
            {
                bool rejected=false;
                try {validate.Invoke(null,[invalid.Item1,invalid.Item2,invalid.Item3,0,new Dictionary<string,decimal>()]);}
                catch(TargetInvocationException ex) when(ex.InnerException is InvalidOperationException) {rejected=true;}
                if(!rejected) throw new Exception("Invalid equipment affix configuration accepted");
            }
            Console.WriteLine($"DLC equipment configuration checks: {dlcEquipment}; preview and incompatible affixes passed.");
            var readTargets=typeof(MainForm).GetMethod("ReadNumericTargets",BindingFlags.Static|BindingFlags.NonPublic)!;
            var editTargets=(Dictionary<string,decimal>)readTargets.Invoke(null,[editNumbers])!;
            if(editTargets.Count!=2 || editTargets["poison"]!=29 || editTargets["aard"]!=20 || quietNumbers["poison"].Value.Value!=35 || quietNumbers["aard"].Value.Value!=35)
                throw new Exception("Edit targets or independence from generation failed");
            typeof(MainForm).GetMethod("SetInventoryBusy",flags)!.Invoke(form,[true]);
            if(editNumbers.Values.Any(pair=>pair.Use.Enabled || pair.Value.Enabled)) throw new Exception("Busy edit fields are enabled");
            if(Field<Button>("checkEquipment").Enabled) throw new Exception("Busy check action is enabled");
            typeof(MainForm).GetMethod("SetInventoryBusy",flags)!.Invoke(form,[false]);
            if(!editNumbers["poison"].Value.Enabled || !editNumbers["poison"].Use.Checked) throw new Exception("Busy state lost edit selection");
            foreach(string category in new[]{"armor","boots","pants","gloves"})
            {
                LoadFakeEquipment(category,new(){{"armor",42.5m}});
                int swordOnly=EffectIndex(editList,"MA_PoisonChance");
                editList.SetItemChecked(swordOnly,true);
                if(editList.GetItemChecked(swordOnly) || !editNumbers["slashingResistance"].Use.Enabled || editNumbers["stagger"].Use.Enabled)
                    throw new Exception("Backpack armor affix applicability failed");
                if(!editNumbers["armor"].Use.Enabled || editNumbers["armor"].Value.Value!=42.5m || editNumbers["poison"].Use.Enabled || editNumbers.Values.Any(pair=>pair.Use.Checked))
                    throw new Exception($"Armor readback or cleared selection failed: {category}, enabled={editNumbers["armor"].Use.Enabled}, value={editNumbers["armor"].Value.Value}, poison={editNumbers["poison"].Use.Enabled}, checked={string.Join(',',editNumbers.Where(pair=>pair.Value.Use.Checked).Select(pair=>pair.Key))}");
            }
            LoadFakeEquipment("crossbow",new());
            if(editNumbers.Values.Any(pair=>pair.Use.Enabled)) throw new Exception("Unsupported equipment has enabled numeric fields");
            typeof(MainForm).GetMethod("InventorySelectionChanged",flags)!.Invoke(form,null);
            if(editNumbers.Values.Any(pair=>pair.Use.Enabled || pair.Use.Checked || pair.Value.Enabled)) throw new Exception("Selection change retains numeric targets");
            LoadFakeEquipment("steelsword",new(){{"damage",63},{"poison",25}});
            editNumbers["poison"].Use.Checked=true;
            editNumbers["poison"].Value.Value=29;
            // Render the offline form without showing or activating a window.
            form.Size=new Size(1715,1131);
            tabs.SelectedIndex=2;
            form.PerformLayout();
            var gearPage=tabs.TabPages[2];
            Size gearSize=gearPage.Size;
            gearPage.Parent=null; gearPage.Dock=DockStyle.None; gearPage.Size=gearSize;
            gearPage.CreateControl(); gearPage.PerformLayout();
            using(var gearBitmap=new Bitmap(gearPage.Width,gearPage.Height))
            {
                gearPage.DrawToBitmap(gearBitmap,new Rectangle(Point.Empty,gearPage.Size));
                gearBitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/layout-affix-numbers-offline.png")));
            }
            gearPage.Dock=DockStyle.Fill; tabs.Controls.Add(gearPage); tabs.Controls.SetChildIndex(gearPage,2);
            tabs.SelectedIndex=0;
            form.PerformLayout();
            var home=tabs.TabPages[0];
            Size homeSize=home.Size;
            home.Parent=null;
            home.Dock=DockStyle.None;
            home.Size=homeSize;
            home.CreateControl();
            home.PerformLayout();
            using(var homeBitmap=new Bitmap(home.Width,home.Height))
            {
                home.DrawToBitmap(homeBitmap,new Rectangle(Point.Empty,home.Size));
                homeBitmap.Save(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research/layout-shortcuts-offline.png")));
            }
            home.Dock=DockStyle.Fill;
            tabs.Controls.Add(home);
            tabs.Controls.SetChildIndex(home,0);
            tabs.SelectedIndex=3;
            form.PerformLayout();
            string quietOutput=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Research"));
            var quietPage=tabs.TabPages[3];
            Size quietSize=quietPage.Size;
            quietPage.Parent=null;
            quietPage.Dock=DockStyle.None;
            quietPage.Size=quietSize;
            quietPage.CreateControl();
            quietPage.PerformLayout();
            using(var quietBitmap=new Bitmap(quietPage.Width,quietPage.Height))
            {
                quietPage.DrawToBitmap(quietBitmap,new Rectangle(Point.Empty,quietPage.Size));
                quietBitmap.Save(Path.Combine(quietOutput,"layout-inventory-numbers-offline.png"));
            }
            quietPage.Dock=DockStyle.Fill;
            tabs.Controls.Add(quietPage);
            tabs.SelectedIndex=4;
            form.PerformLayout();
            var funPage=tabs.TabPages[4];
            Size funSize=funPage.Size;
            funPage.Parent=null;funPage.Dock=DockStyle.None;funPage.Size=funSize;
            funPage.CreateControl();funPage.PerformLayout();
            using(var funBitmap=new Bitmap(funPage.Width,funPage.Height))
            {
                funPage.DrawToBitmap(funBitmap,new Rectangle(Point.Empty,funPage.Size));
                funBitmap.Save(Path.Combine(quietOutput,"layout-fun-offline.png"));
            }
            funPage.Dock=DockStyle.Fill;tabs.Controls.Add(funPage);
            if(form.Visible || Field<CheckedListBox>("editAffixes").Height<100)
                throw new Exception("Quiet render was shown or affix list is too small");
            if(Field<TextBox>("editPreview").Height<160 || Field<ListBox>("inventoryList").Height<250)
                throw new Exception("Preview has insufficient space or crowds inventory list");
            foreach(var controls in editNumbers.Values)
                if(controls.Value.Width<100 || controls.Value.Height>controls.Value.Parent!.Height)
                    throw new Exception("Inventory numeric input layout failed");
            typeof(MainForm).GetMethod("LoadEditNumericValues",flags)!.Invoke(form,[new Dictionary<string,decimal>{{"damage",142.5677m}},new Dictionary<string,decimal>{{"damage",142.568m}}]);
            if(editNumbers["damage"].Value.Minimum<142.567m || tips.GetToolTip(editNumbers["damage"].Value)?.Contains("最低目标")!=true)
                throw new Exception("Native damage floor is not shown or enforced");
            var sounds=typeof(MainForm).Assembly.GetType("Witcher3Modifier.ToggleSounds")!;
            var wave=sounds.GetMethod("Wave",BindingFlags.Static|BindingFlags.NonPublic)!;
            var loudness=new List<double>();
            foreach(int frequency in new[]{1568,392})
            {
                byte[] bytes=(byte[])wave.Invoke(null,[frequency,90])!;
                int samples=44100*90/1000;
                if(BitConverter.ToInt32(bytes,24)!=44100 || bytes.Length!=44+samples*2)
                    throw new Exception("Unexpected single sound format");
                int[] pcm=Enumerable.Range(0,samples).Select(i=>(int)BitConverter.ToInt16(bytes,44+i*2)).ToArray();
                if(pcm.Max(Math.Abs)>6600 || pcm[0]!=0 || pcm[^1]!=0 || pcm.Skip(44).Take(samples-264).Chunk(44).Any(chunk=>chunk.All(v=>v==0)))
                    throw new Exception("Single pulse envelope invalid");
                loudness.Add(Math.Sqrt(pcm.Average(v=>(double)v*v)));
            }
            if(Math.Abs(loudness[0]/loudness[1]-1)>.01) throw new Exception("On/off signal volume differs");
            Console.WriteLine("Single high/low tones have equal duration and RMS within 1 percent");
            if(Field<CheckBox>("hotkeysToggle").Parent?.Parent!=form || Field<CheckBox>("catVisionToggle").Text!="猫药水夜视")
                throw new Exception("Global hotkey control or cat switch missing");
            typeof(MainForm).GetMethod("PlaySuccess",flags)!.Invoke(form,[true]);
            typeof(MainForm).GetMethod("PlaySuccess",flags)!.Invoke(form,[false]);
            Field<NumericUpDown>("enemySpeedValue").Value=.7m;
            Field<CheckBox>("enemySpeedToggle").Checked=true;
            save.Checked=true;
            typeof(MainForm).GetMethod("SaveRuntimeSettings",flags)!.Invoke(form,null);
            var enemySaved=JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
            if(enemySaved.EnemySpeedMultiplier!=.7m || !enemySaved.Toggles.GetValueOrDefault("enemySpeed"))
                throw new Exception("Enemy speed preferences were not saved");
            save.Checked=false;
            Field<NumericUpDown>("enemySpeedValue").Value=.9m;
            Field<CheckBox>("enemySpeedToggle").Checked=false;
            enemySaved=JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
            if(enemySaved.EnemySpeedMultiplier!=.7m || !enemySaved.Toggles.GetValueOrDefault("enemySpeed"))
                throw new Exception("Disabled saving changed enemy speed preferences");
            using(var restoredEnemyForm=new MainForm(offlinePreview:true))
            {
                if(((NumericUpDown)typeof(MainForm).GetField("enemySpeedValue",flags)!.GetValue(restoredEnemyForm)!).Value!=.7m ||
                    ((CheckBox)typeof(MainForm).GetField("enemySpeedToggle",flags)!.GetValue(restoredEnemyForm)!).Checked)
                    throw new Exception("Enemy speed reload did not honor the global save toggle");
            }
            Console.WriteLine("Enemy speed preference save/disabled-save/reload passed; no game access");
            File.Delete(path);
            Console.WriteLine("Quiet checks passed: numpad-only mapping/labels/home order, silent offline hotkeys, known teleport job upgrade/unknown code rejection, equipment-level setting, numeric target persistence, inventory readback/selection/busy/type states, hidden render, single high/low equal-level sound envelopes; no shown window or game access");
            return;
        }
        form.Size = new Size(1715, 1131);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Show();
        Field<System.Windows.Forms.Timer>("inventoryBinding").Stop();
        form.PerformLayout();
        var header = form.Controls.Cast<Control>().Single(control => control.GetType().Name == "SwordHeader");
        using (var emptyPaintBitmap = new Bitmap(1,1))
        using (var emptyPaintGraphics = Graphics.FromImage(emptyPaintBitmap))
        {
            Size previousSize = header.Size;
            header.Size = new Size(0,112);
            header.GetType().GetMethod("OnPaintBackground",flags)!.Invoke(header,[new PaintEventArgs(emptyPaintGraphics,new Rectangle(0,0,0,112))]);
            header.Size = previousSize;
        }
        var windowButtons = Field<FlowLayoutPanel>("windowActions");
        if (windowButtons.Controls.Count != 3) throw new Exception("Window actions missing");
        ((Button)windowButtons.Controls[0]).PerformClick();
        if (form.WindowState != FormWindowState.Minimized) throw new Exception("Minimize action failed");
        form.WindowState = FormWindowState.Normal;
        Point resizePoint = form.PointToScreen(new Point(2,form.Height/2));
        var resizeMessage = Message.Create(form.Handle,0x84,0,unchecked((nint)((resizePoint.Y & 0xffff)<<16 | (resizePoint.X & 0xffff))));
        object[] resizeArgs = [resizeMessage];
        bool handled = (bool)typeof(MainForm).GetMethod("HandleWindowResize",flags)!.Invoke(form,resizeArgs)!;
        if (!handled || ((Message)resizeArgs[0]).Result != 10) throw new Exception("Left resize hit test failed");
        for (int cycle=0; cycle<20; cycle++)
        {
            tabs.SelectedIndex=cycle%4;
            form.Size=new Size(1715+(cycle%2)*40,1131+(cycle%2)*30);
            Application.DoEvents();
        }
        save.Checked=false;
        form.Size=new Size(1715,1131);
        string output = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../Research"));
        float normalFont = form.Font.Size;
        int normalHeaderHeight = header.Height;
        for (int cycle=0; cycle<5; cycle++)
        {
            ((Button)windowButtons.Controls[1]).PerformClick();
            Application.DoEvents();
            if (form.WindowState != FormWindowState.Maximized || form.Font.Size <= normalFont || header.Height <= normalHeaderHeight)
                throw new Exception("Maximize must enlarge fonts and controls");
            if(cycle==0)
            {
                tabs.SelectedIndex=0;
                using var maximizedBitmap=new Bitmap(form.Width,form.Height);
                form.DrawToBitmap(maximizedBitmap,new Rectangle(Point.Empty,form.Size));
                maximizedBitmap.Save(System.IO.Path.Combine(output,"layout-maximized.png"));
            }
            ((Button)windowButtons.Controls[1]).PerformClick();
            Application.DoEvents();
            if(Math.Abs(form.Font.Size-normalFont)>.01f || Math.Abs(header.Height-normalHeaderHeight)>1)
                throw new Exception($"Restore sizing drift: {header.Height} versus {normalHeaderHeight}");
        }
        foreach (int index in new[] { 0, 1, 2, 3 })
        {
            tabs.SelectedIndex = index;
            form.PerformLayout();
            if (index == 3)
            {
                var inspect = Field<Button>("inspectEquipment");
                if (inspect.Height > inspect.Parent!.Height) throw new Exception("Inspect button exceeds its row");
            }
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(System.IO.Path.Combine(output, $"layout-page-{index}.png"));
            if (index == 2 && list.Height < 90) throw new Exception($"Gear effects height too small: {list.Height}");
            if (index == 3 && Field<CheckedListBox>("editAffixes").Height < 65) throw new Exception("Inventory effects height too small");
        }
        IEnumerable<Control> Descendants(Control root)
        {
            foreach(Control child in root.Controls)
            {
                yield return child;
                foreach(var nested in Descendants(child)) yield return nested;
            }
        }
        tabs.SelectedIndex=0;
        if(Descendants(tabs.TabPages[0]).Any(control=>control.Text.Contains("更多辅助功能")))
            throw new Exception("Home must show auxiliary functions directly");
        var toxicity=Descendants(tabs.TabPages[0]).Single(control=>control.Text=="无毒性");
        if(!toxicity.Visible) throw new Exception("Auxiliary function is hidden");
        tabs.SelectedIndex=2;
        var numbers=Field<Dictionary<string,(CheckBox Use,NumericUpDown Value)>>("numericFields");
        numbers["poison"].Use.Checked=true;
        numbers["poison"].Value.Value=35;
        typeof(MainForm).GetMethod("AddGear",flags)!.Invoke(form,null);
        var numericDraft=JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
        if(numericDraft.Gear.Last().NumericTargets.GetValueOrDefault("poison")!=35)
            throw new Exception("Saved equipment lost its numeric target");
        Field<ComboBox>("gearType").SelectedIndex=2;
        var armorTargets=(Dictionary<string,decimal>)typeof(MainForm).GetMethod("ReadNumericTargets",flags)!.Invoke(form,null)!;
        if(numbers["poison"].Use.Enabled || !numbers["armor"].Use.Enabled || armorTargets.ContainsKey("poison"))
            throw new Exception("Armor must exclude sword-only chance targets");
        typeof(MainForm).GetMethod("SaveWindowBounds", flags)!.Invoke(form, null);
        Field<NumericUpDown>("goldMultiplier").Value = 3;
        Field<NumericUpDown>("xp").Value = 1000m;
        Field<CheckBox>("keepItemsToggle").Checked = false;
        typeof(MainForm).GetMethod("SaveRuntimeSettings",flags)!.Invoke(form,null);
        var disabled = JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
        if (disabled.GoldMultiplier != 2 || disabled.XpMultiplier != 1.5m || !disabled.KeepItems)
            throw new Exception("Disabled saving changed the stored runtime preferences");
        save.Checked = true;
        var enabled = JsonSerializer.Deserialize<Draft>(File.ReadAllText(path))!;
        if (!enabled.SaveSwitchesAndMultipliers || enabled.GoldMultiplier != 3 || enabled.XpMultiplier != 1000m || enabled.KeepItems)
            throw new Exception("Enabled saving failed to capture current choices");
        save.Checked = false;
        using var reopened = new MainForm(offlinePreview: true);
        var reopenedSave = (CheckBox)typeof(MainForm).GetField("saveSettingsToggle",flags)!.GetValue(reopened)!;
        if (reopenedSave.Checked) throw new Exception("Saving preference did not survive reopening");
        File.Delete(path);
        if (reopened.Width != form.Width || reopened.Height != form.Height) throw new Exception("Window dimensions were not restored");
        Console.WriteLine("UI offline checks passed: home layout, persistent selection through search, window size, global save toggle and persistence; no game writes");
    }
}
