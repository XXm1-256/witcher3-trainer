namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly Dictionary<string,(CheckBox Use,NumericUpDown Value)> numericFields = [];
    private const string EquipmentLevelHint="修改基础伤害或护甲可能改变需要等级。需要等级无法单独设置；穿戴高等级装备可开启首页“无视装备穿戴等级”。";

    private Control BuildNumericFields(Dictionary<string,(CheckBox Use,NumericUpDown Value)>? fields = null, Action? changed = null)
    {
        fields ??= numericFields;
        changed ??= RefreshGearPreview;
        var panel = Grid(2);
        panel.Padding=Padding.Empty;
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        panel.RowCount=7;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        panel.Controls.Add(new Label {Text="目标属性数值",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        panel.SetColumnSpan(panel.Controls[0],2);
        foreach(var field in GameEquipmentNumbers.Fields.Take(6))
        {
            var use = new TrainerSwitch {Text=field.Label,AutoSize=true,Anchor=AnchorStyles.Left};
            var value = new NumericUpDown {Minimum=0,Maximum=field.Unit==.01m?100:5000,Value=field.Unit==.01m?25:100,Dock=DockStyle.Fill,Enabled=false};
            int row=fields.Count+1;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            panel.Controls.Add(use,0,row); panel.Controls.Add(value,1,row);
            fields[field.Id]=(use,value);
            value.AccessibleName=field.Label;
            use.CheckedChanged+=(_,_)=> {value.Enabled=use.Enabled && use.Checked;changed();};
            use.Click+=(_,_)=> {if(!offlinePreview) ToggleSounds.Play(use.Checked);};
            value.ValueChanged+=(_,_)=>changed();
            usageTips.SetToolTip(use,"勾选后输入目标数值。可用“使用可达目标”填写最接近的可用值，最低值依装备而定。基础伤害与游戏显示的伤害区间不同。");
            if(field.Id is "damage" or "armor")
            {
                usageTips.SetToolTip(use,usageTips.GetToolTip(use)+EquipmentLevelHint);
                usageTips.SetToolTip(value,EquipmentLevelHint);
            }
        }
        return panel;
    }

    private Dictionary<string,decimal> ReadNumericTargets() => ReadNumericTargets(numericFields);

    private Control BuildAffixNumericEditor(CheckedListBox list, Dictionary<string,(CheckBox Use,NumericUpDown Value)> fields, Action changed)
    {
        var panel=Grid(1);
        panel.Padding=Padding.Empty;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        var title=new Label {Text="选中词条后设置目标数值",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        var row=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
        var use=new TrainerSwitch {Name="affixTargetToggle",Text="设置目标值",AutoSize=true,Enabled=false};
        var value=new NumericUpDown {Name="affixTargetValue",Width=125,Minimum=0,Maximum=5000,DecimalPlaces=3,Enabled=false};
        var nearest=new MetalButton {Text="使用可达值",Name="affixNearest",AutoSize=true,Enabled=false};
        row.Controls.Add(use); row.Controls.Add(value);
        row.Controls.Add(nearest);
        panel.Controls.Add(title,0,0); panel.Controls.Add(row,0,1);
        var storage=new Panel {Visible=false};
        panel.Controls.Add(storage,0,2);
        bool syncing=false;
        foreach(var field in GameEquipmentNumbers.Fields.Skip(6))
        {
            var targetUse=new CheckBox {Enabled=false};
            var targetValue=new NumericUpDown {Minimum=0,Maximum=field.Unit==.01m?100:5000,DecimalPlaces=3,Value=field.Unit==.01m?25:100,Enabled=false};
            fields[field.Id]=(targetUse,targetValue);
            storage.Controls.Add(targetUse); storage.Controls.Add(targetValue);
            targetUse.CheckedChanged+=(_,_)=> {targetValue.Enabled=targetUse.Enabled && targetUse.Checked;changed();};
            targetValue.ValueChanged+=(_,_)=>changed();
        }
        GameEquipmentNumbers.Field? SelectedField() => list.SelectedItem is EquipmentEffect effect ? GameEquipmentNumbers.ForAffix(effect.Id) : null;
        void RefreshEditor()
        {
            syncing=true;
            try
            {
                var field=SelectedField();
                if(field is null)
                {
                    title.Text=list.SelectedItem is EquipmentEffect effect ? $"{effect.DisplayName} · {effect.TypeHint} · 开关效果，无数值" : "选中词条后设置目标数值";
                    use.Checked=false; use.Enabled=value.Enabled=false;
                    nearest.Enabled=false;
                }
                else
                {
                    var target=fields[field.Id];
                    title.Text=$"{field.Label} · {(list.SelectedItem as EquipmentEffect)?.TypeHint}";
                    use.Enabled=list.Enabled && target.Use.Enabled;
                    use.Checked=target.Use.Checked;
                    value.Minimum=target.Value.Minimum; value.Maximum=target.Value.Maximum;
                    value.Value=target.Value.Value;
                    value.Enabled=use.Enabled && use.Checked;
                    nearest.Enabled=field.SeedUnit && list==editAffixes && loadedEquipment is not null && value.Enabled;
                    usageTips.SetToolTip(value,field.SeedUnit ? "部分目标数值受装备随机属性限制。背包页可点击“使用可达值”填写最接近的可用数值。" : "输入该属性修改后的总数值，勾选后生效。最低值受基础装备限制。");
                }
            }
            finally {syncing=false;}
        }
        use.CheckedChanged+=(_,_)=> {if(!syncing && SelectedField() is {} field) fields[field.Id].Use.Checked=use.Checked;};
        use.Click+=(_,_)=> {if(!offlinePreview) ToggleSounds.Play(use.Checked);};
        value.ValueChanged+=(_,_)=> {if(!syncing && SelectedField() is {} field) fields[field.Id].Value.Value=value.Value;};
        nearest.Click+=async (_,_)=>
        {
            if(offlinePreview || loadedEquipment is null || SelectedField() is not {} field) return;
            var item=loadedEquipment;
            decimal requested=value.Value;
            SetInventoryBusy(true);
            try
            {
                decimal reachable=await Task.Run(()=>GameItemScheduler.RunForItem(item,()=>GameEquipmentNumbers.NearestTarget(item.UniqueId,item.Category,field.Id,requested)));
                fields[field.Id].Value.Value=reachable;
                status.Text=$"{field.Label}的可达目标已填入 {reachable:0.###}；点击应用装备修改后生效。";
                PlaySuccess();
            }
            catch(Exception ex) {LogFailure("计算词条可达值",ex,new {item.UniqueId,field.Id,Requested=requested});status.Text=ex.Message;}
            finally {SetInventoryBusy(false);}
        };
        usageTips.SetToolTip(nearest,"先读取装备并勾选目标数值。本按钮填入最接近的可用值；点击“应用装备修改”后生效。");
        list.SelectedIndexChanged+=(_,_)=>RefreshEditor();
        list.EnabledChanged+=(_,_)=>RefreshEditor();
        foreach(var target in fields.Values)
        {
            target.Use.EnabledChanged+=(_,_)=>RefreshEditor();
            target.Use.CheckedChanged+=(_,_)=>RefreshEditor();
            target.Value.ValueChanged+=(_,_)=>RefreshEditor();
        }
        usageTips.SetToolTip(title,"设置该属性修改后的总数值，包含装备原有值和附加词条。最低值受基础装备限制；搜索或切换词条会保留已填数值。");
        usageTips.SetToolTip(list,"名称后标明适用装备：抗性、生命值和活力恢复用于护甲；触发几率和护甲穿透用于钢剑、银剑。选中词条后可设置总数值。");
        RefreshEditor();
        return panel;
    }

    private static Dictionary<string,decimal> ReadNumericTargets(Dictionary<string,(CheckBox Use,NumericUpDown Value)> fields) => fields
        .Where(pair=>pair.Value.Use.Enabled && pair.Value.Use.Checked)
        .ToDictionary(pair=>pair.Key,pair=>pair.Value.Value.Value);

    private void UpdateNumericTypes(string category)
    {
        foreach(var field in GameEquipmentNumbers.Fields)
        {
            var controls=numericFields[field.Id];
            controls.Use.Enabled=field.Categories.Split(',').Contains(category);
            controls.Value.Enabled=controls.Use.Enabled && controls.Use.Checked;
        }
    }

    private void LoadEditNumericValues(IReadOnlyDictionary<string,decimal> values,IReadOnlyDictionary<string,decimal>? minimums=null)
    {
        loadedNumericValues.Clear();
        foreach(var pair in values) loadedNumericValues[pair.Key]=pair.Value;
        foreach(var field in GameEquipmentNumbers.Fields)
        {
            var controls=editNumericFields[field.Id];
            controls.Use.Checked=false;
            controls.Use.Enabled=false;
            controls.Value.Enabled=false;
            controls.Value.DecimalPlaces=3;
            decimal current=values.GetValueOrDefault(field.Id);
            controls.Value.Minimum=Math.Min(0,current);
            if(minimums is not null && minimums.TryGetValue(field.Id,out decimal minimum)) controls.Value.Minimum=Math.Min(current,minimum);
            controls.Value.Maximum=Math.Max(field.Unit==.01m?100:5000,current);
            controls.Value.Value=current;
            usageTips.SetToolTip(controls.Value,values.ContainsKey(field.Id)
                ? $"读取值：{current:0.###}。最低目标：{controls.Value.Minimum:0.###}。勾选左侧属性后设置目标，修改完成后保存游戏。"+(field.Id is "damage" or "armor" ? EquipmentLevelHint : "")
                : "此装备类型不适用该属性。");
        }
    }

    private void SetEditNumericEnabled(bool enabled)
    {
        foreach(var field in GameEquipmentNumbers.Fields)
        {
            var controls=editNumericFields[field.Id];
            controls.Use.Enabled=enabled && loadedEquipment is not null && field.Categories.Split(',').Contains(loadedEquipment.Category);
            controls.Value.Enabled=controls.Use.Enabled && controls.Use.Checked;
        }
    }
}
