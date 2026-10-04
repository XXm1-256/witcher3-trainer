namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TrainerSwitch moveSpeedToggle=new() {Text="移动速度倍率",AutoSize=true};
    private readonly TrainerSwitch jumpHeightToggle=new() {Text="跳跃高度倍率",AutoSize=true};
    private readonly NumericUpDown moveSpeedValue=new() {Minimum=.5m,Maximum=10,DecimalPlaces=2,Increment=.1m,Value=1.5m,Width=85};
    private readonly NumericUpDown jumpHeightValue=new() {Minimum=.5m,Maximum=10,DecimalPlaces=2,Increment=.1m,Value=1.5m,Width=85};
    private bool playerMotionBusy,playerMotionReady,playerMotionClosing,playerMotionClosed;

    private Control BuildPlayerMotion(string feature)
    {
        var toggle=feature=="moveSpeed"?moveSpeedToggle:jumpHeightToggle;
        var value=feature=="moveSpeed"?moveSpeedValue:jumpHeightValue;
        value.Value=Math.Clamp(feature=="moveSpeed"?draft.MoveSpeedMultiplier:draft.JumpHeightMultiplier,.5m,10);
        toggle.Checked=draft.SaveSwitchesAndMultipliers && draft.Toggles.GetValueOrDefault(feature);
        toggle.CheckedChanged+=async (_,_)=>await RefreshPlayerMotion(true,feature);
        value.ValueChanged+=async (_,_)=> {SaveRuntimeSettings();if(toggle.Checked) await RefreshPlayerMotion(true,feature);};
        var row=(FlowLayoutPanel)WithFeatureShortcut(toggle,feature);
        row.Controls.Add(value);row.Controls.SetChildIndex(value,1);
        if(feature=="jumpHeight")
        {
            Shown+=async (_,_)=> {playerMotionReady=true;await RefreshPlayerMotion(false);};
            FormClosing+=async (_,args)=>
            {
                if(closingWithoutGame || offlinePreview || playerMotionClosed || (!moveSpeedToggle.Checked && !jumpHeightToggle.Checked && !playerMotionBusy)) return;
                args.Cancel=true;
                if(playerMotionBusy) {status.Text="移动功能正在处理，完成后可关闭修改器";return;}
                playerMotionClosing=true;
                await RefreshPlayerMotion(false);
                if(playerMotionClosed) BeginInvoke(Close);
            };
        }
        return row;
    }

    private async Task RefreshPlayerMotion(bool manual,string? only=null)
    {
        if(offlinePreview || playerMotionClosed || playerMotionBusy || !playerMotionReady) return;
        var requests=new[]{("moveSpeed",moveSpeedToggle.Checked,moveSpeedValue.Value),("jumpHeight",jumpHeightToggle.Checked,jumpHeightValue.Value)}
            .Where(request=>only is null?request.Item2 || playerMotionClosing:request.Item1==only).ToArray();
        if(requests.Length==0) return;
        playerMotionBusy=true;
        moveSpeedToggle.Enabled=jumpHeightToggle.Enabled=moveSpeedValue.Enabled=jumpHeightValue.Enabled=false;
        try
        {
            foreach(var (feature,enabled,value) in requests)
            {
                await Task.Run(()=>GameItemScheduler.SetPlayerMotion(feature,!playerMotionClosing && enabled,(float)value));
                if(IsDisposed) return;
                if(manual) {ToggleSounds.Play(enabled);status.Text=$"{(feature=="moveSpeed"?"移动速度":"跳跃高度")}参数已{(enabled?$"应用 {value:0.##} 倍":"恢复")}";}
            }
            if(manual) SaveRuntimeSettings();
            if(playerMotionClosing) playerMotionClosed=true;
        }
        catch(Exception ex)
        {
            if(!IsDisposed) {LogFailure("玩家移动参数",ex);status.Text="移动功能未确认："+ex.Message;}
        }
        finally
        {
            playerMotionBusy=false;
            if(!IsDisposed) moveSpeedToggle.Enabled=jumpHeightToggle.Enabled=moveSpeedValue.Enabled=jumpHeightValue.Enabled=true;
        }
    }
}
