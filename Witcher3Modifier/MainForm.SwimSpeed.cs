namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TrainerSwitch swimSpeedToggle=new() {Text="游泳速度倍率",AutoSize=true};
    private readonly NumericUpDown swimSpeedValue=new() {Minimum=.5m,Maximum=10,DecimalPlaces=2,Increment=.1m,Value=1.5m,Width=85};
    private readonly System.Windows.Forms.Timer swimSpeedTimer=new() {Interval=500};
    private bool swimSpeedBusy,swimSpeedManual,swimSpeedClosing;

    private Control BuildSwimSpeed()
    {
        swimSpeedValue.Value=Math.Clamp(draft.SwimSpeedMultiplier,.5m,10);
        swimSpeedToggle.Checked=draft.SaveSwitchesAndMultipliers && draft.Toggles.GetValueOrDefault("swimSpeed");
        swimSpeedToggle.CheckedChanged+=async (_,_)=> {if(offlinePreview)return;swimSpeedManual=true;swimSpeedTimer.Start();SaveRuntimeSettings();await RefreshSwimSpeed();};
        swimSpeedValue.ValueChanged+=async (_,_)=> {SaveRuntimeSettings();if(!offlinePreview && swimSpeedToggle.Checked){swimSpeedManual=true;swimSpeedTimer.Start();await RefreshSwimSpeed();}};
        swimSpeedTimer.Tick+=async (_,_)=>await RefreshSwimSpeed();
        Shown+=async (_,_)=> {if(!offlinePreview){swimSpeedTimer.Start();await RefreshSwimSpeed(true);}};
        FormClosed+=(_,_)=>swimSpeedTimer.Dispose();
        FormClosing+=async (_,args)=>
        {
            if(offlinePreview || (!swimSpeedBusy && !GameItemScheduler.SwimSpeedHasEffect)) return;
            args.Cancel=true;swimSpeedClosing=true;swimSpeedManual=false;swimSpeedTimer.Start();
            status.Text="正在恢复游泳速度，请返回游戏等待完成后关闭";
            await RefreshSwimSpeed();
        };
        var row=(FlowLayoutPanel)WithFeatureShortcut(swimSpeedToggle,"swimSpeed");
        row.Controls.Add(swimSpeedValue);row.Controls.SetChildIndex(swimSpeedValue,1);
        return row;
    }

    private async Task RefreshSwimSpeed(bool restore=false)
    {
        if(offlinePreview || swimSpeedBusy || (!restore && !swimSpeedToggle.Checked && !GameItemScheduler.SwimSpeedHasEffect && !swimSpeedManual && !swimSpeedClosing)) return;
        bool enabled=!swimSpeedClosing && swimSpeedToggle.Checked;
        decimal value=swimSpeedValue.Value;
        swimSpeedBusy=true;
        try
        {
            var result=await Task.Run(()=>GameItemScheduler.UpdateSwimSpeed(enabled,(float)value));
            if(IsDisposed || (!swimSpeedClosing && swimSpeedToggle.Checked!=enabled) || swimSpeedValue.Value!=value) return;
            if(swimSpeedManual)
            {
                status.Text=result.Applied?enabled?result.Swimming?$"游泳速度 {value:0.##} 倍已应用":$"游泳速度 {value:0.##} 倍已启用，入水游泳后自动应用":"游泳速度倍率已关闭":"游泳速度请求等待返回实际游玩场景";
                if(result.Applied){ToggleSounds.Play(enabled);swimSpeedManual=false;}
            }
            if(result.Applied && swimSpeedClosing && !GameItemScheduler.SwimSpeedHasEffect){swimSpeedClosing=false;BeginInvoke(Close);}
        }
        catch(Exception ex)
        {
            if(!IsDisposed){LogFailure("游泳速度倍率",ex,new {Enabled=enabled,Multiplier=value});status.Text="游泳速度未确认："+ex.Message;swimSpeedManual=false;swimSpeedTimer.Stop();}
        }
        finally {swimSpeedBusy=false;}
    }
}
