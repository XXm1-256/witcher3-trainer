namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private Panel? funActions;
    private readonly CheckBox catVisionToggle=new TrainerSwitch {Text="猫药水夜视",AutoSize=true};
    private bool refreshingCatVision;
    private readonly Label funResult=new() { Text="选择项目后执行；暂停时请返回游戏等待处理。",Dock=DockStyle.Fill,AutoEllipsis=true };

    private TabPage BuildFun()
    {
        var page=new TabPage("趣味实验室");
        var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};
        var grid=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(12),MinimumSize=new Size(800,0)};
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        funActions=grid;
        FormClosing+=(_,eventArgs)=>
        {
            if(!offlinePreview && !grid.Enabled)
            {eventArgs.Cancel=true;status.Text="趣味功能正在处理，完成后可关闭修改器";}
        };
        scroll.Controls.Add(grid); page.Controls.Add(scroll);
        var weather=Choice("晴朗");
        weather.Items.Clear();weather.Items.Add(new GameItemScheduler.FunWeather("晴朗","WT_Clear"));weather.SelectedIndex=0;
        var hour=new NumericUpDown {Minimum=0,Maximum=23,Value=12,Width=70};
        var minute=new NumericUpDown {Minimum=0,Maximum=59,Width=70};
        var hair=Choice(GameItemScheduler.FunHairStyles.Select(style=>style.Label).ToArray());
        var beard=Choice("刮干净","短胡茬","短胡须","中等胡须","浓密胡须");
        var slow=new NumericUpDown {Minimum=.1m,Maximum=1,Value=.5m,DecimalPlaces=2,Increment=.1m,Width=90};
        Add(0,0,"天气与时段",new FlowLayoutPanel {AutoSize=true,WrapContents=true,Controls={weather,
            Action("读取区域天气",()=> {var choices=GameItemScheduler.ReadFunWeather();Invoke(()=> {weather.Items.Clear();weather.Items.AddRange(choices);if(choices.Length>0) weather.SelectedIndex=0;});return $"已读取当前区域 {choices.Length} 种天气";}),
            Action("应用天气",()=> {string? name=null;Invoke(()=>name=(weather.SelectedItem as GameItemScheduler.FunWeather)?.Name);if(name is null) throw new InvalidOperationException("请先读取并选择区域天气。");GameItemScheduler.SetFunWeather(name);return "天气请求已被游戏接受，等待渐变完成";}),
            Hint("先读取可用天气，再选择并应用。天气会逐渐切换，剧情可能覆盖设置。")}},
            new FlowLayoutPanel {AutoSize=true,WrapContents=true,Controls={new Label {Text="时",AutoSize=true},hour,new Label {Text="分",AutoSize=true},minute,
            Action("设置时段",()=>"游戏时间已回读："+Clock(GameItemScheduler.SetFunTime(UiValue(()=>(int)hour.Value),UiValue(()=>(int)minute.Value)))),
            Action("读取时间",()=>"当前游戏时间："+Clock(GameItemScheduler.ReadFunTime())),
            Hint("设置当天时刻。会影响NPC日程及限时任务，请先保存进度。")}});
        Add(1,0,"发型与胡须",new FlowLayoutPanel {AutoSize=true,WrapContents=true,Controls={hair,
            Action("应用发型",()=> {GameItemScheduler.SetFunHair(UiValue(()=>hair.SelectedIndex));return "发型装配已回读确认，请查看杰洛特外观";}),
            Hint("选择发型，包含DLC理发样式。保存游戏可保留造型。")}},
            new FlowLayoutPanel {AutoSize=true,WrapContents=true,Controls={beard,
            Action("应用胡须",()=>"胡须阶段已回读确认："+GameItemScheduler.SetFunBeard(UiValue(()=>beard.SelectedIndex))),
            Hint("选择胡须长度，之后仍会正常生长。特殊剧情造型可能限制修改。")}});
        Add(0,1,"慢动作",new FlowLayoutPanel {AutoSize=true,WrapContents=true,Controls={new Label {Text="速度倍率",AutoSize=true},slow,
            Action("应用慢动作",()=>"慢动作请求已执行，当前总速度倍率："+GameItemScheduler.SetFunSlow(UiValue(()=>(float)slow.Value)).ToString("0.###")),
            Action("恢复正常速度",()=>"已撤销本项慢动作，当前总速度倍率："+GameItemScheduler.SetFunSlow(1).ToString("0.###")),
            Hint("让整个游戏以0.1～1倍速度运行，角色也会变慢。结束使用前点击“恢复”；此设置不随存档保存。")}});
        grid.RowCount=3;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        grid.Controls.Add(funResult,0,2);grid.SetColumnSpan(funResult,2);
        return page;

        void Add(int column,int row,string title,params Control[] controls)
        {
            var section=new TableLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1,Padding=new Padding(8),Margin=new Padding(4,4,12,16)};
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            section.Controls.Add(new Label {Text=title,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(3,6,3,14)});
            foreach(var control in controls)
            {
                if(control is FlowLayoutPanel flow) {flow.WrapContents=false;flow.AutoSize=false;flow.Height=60;}
                control.Dock=DockStyle.Top;control.Margin=new Padding(3,8,3,8);section.Controls.Add(control);
            }
            grid.Controls.Add(section,column,row);
        }
        ComboBox Choice(params string[] choices)
        {
            var combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=210};combo.Items.AddRange(choices);combo.SelectedIndex=0;return combo;
        }
        HelpIcon Hint(string text)
        {
            var hint=new HelpIcon {AccessibleName="功能说明",AccessibleDescription=text,Margin=new Padding(8,6,0,0)};
            usageTips.SetToolTip(hint,text);return hint;
        }
        MetalButton Action(string text,Func<string> execute,bool? switchSound=null)
        {
            var button=new MetalButton {Text=text,AutoSize=true,MinimumSize=new Size(140,44),Margin=new Padding(6,0,6,8)};
            button.Click+=async (_,_)=>await RunFunAction(text,execute,switchSound);return button;
        }
        static string Clock(int seconds)=>$"第 {seconds/86400} 天 {seconds%86400/3600:00}:{seconds%3600/60:00}";
        T UiValue<T>(Func<T> read)=>(T)Invoke(read);
    }

    private Control BuildCatVision()
    {
        catVisionToggle.Checked=draft.SaveSwitchesAndMultipliers && draft.Toggles.GetValueOrDefault("catVision");
        catVisionToggle.CheckedChanged+=async (_,_)=>
        {
            if(offlinePreview || refreshingCatVision) return;
            bool enabled=catVisionToggle.Checked;
            catVisionToggle.Enabled=false;
            bool applied=await RunFunAction("猫药水夜视",()=> {GameItemScheduler.SetFunCat(enabled);return enabled?"猫药水夜视开启请求已执行":"猫药水夜视关闭请求已执行";},enabled);
            catVisionToggle.Enabled=true;
            if(!applied) {refreshingCatVision=true;catVisionToggle.Checked=!enabled;refreshingCatVision=false;}
            else SaveRuntimeSettings();
        };
        return WithFeatureShortcut(catVisionToggle,"catVision");
    }

    private async Task RestoreCatVisionAsync()
    {
        if(offlinePreview) return;
        catVisionToggle.Enabled=false;
        try {await Task.Run(()=>GameItemScheduler.SetFunCat(draft.Toggles.GetValueOrDefault("catVision")));}
        catch(Exception ex) {if(!IsDisposed) {LogFailure("恢复猫眼",ex);status.Text="猫眼恢复未确认："+ex.Message;}}
        finally {if(!IsDisposed) catVisionToggle.Enabled=true;}
    }

    private async Task<bool> RunFunAction(string name,Func<string> execute,bool? switchSound)
    {
        if(offlinePreview || funActions is null || !funActions.Enabled) return false;
        funActions.Enabled=false;
        funResult.Text=status.Text=name+"处理中，请返回游戏等待执行";
        try
        {
            string result=await Task.Run(execute);
            if(IsDisposed) return false;
            funResult.Text=status.Text=result;
            if(switchSound.HasValue) ToggleSounds.Play(switchSound.Value);else PlaySuccess();
            ErrorLog.Write("趣味功能完成",null,new {Function=name,Result=result});
            return true;
        }
        catch(Exception ex) {if(!IsDisposed) {LogFailure(name,ex);funResult.Text=status.Text=name+"未确认："+ex.Message;} }
        finally {if(!IsDisposed) funActions.Enabled=true;}
        return false;
    }
}
