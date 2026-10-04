namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TrainerSwitch enemySpeedToggle=new() {Text="敌人速度倍率",AutoSize=true};
    private readonly NumericUpDown enemySpeedValue=new() {Minimum=.1m,Maximum=10,Value=.5m,DecimalPlaces=2,Increment=.1m,Width=85};
    private readonly System.Windows.Forms.Timer enemySpeedTimer=new() {Interval=2000};
    private bool enemySpeedBusy,enemySpeedManual,enemySpeedClosing;

    private Control BuildEnemySpeed()
    {
        enemySpeedValue.Value=Math.Clamp(draft.EnemySpeedMultiplier,.1m,10);
        enemySpeedToggle.Checked=draft.SaveSwitchesAndMultipliers && draft.Toggles.GetValueOrDefault("enemySpeed");
        enemySpeedToggle.CheckedChanged+=async (_,_)=>
        {
            if(offlinePreview) return;
            enemySpeedManual=true;enemySpeedTimer.Start();SaveRuntimeSettings();await RefreshEnemySpeed();
        };
        enemySpeedValue.ValueChanged+=async (_,_)=>
        {
            SaveRuntimeSettings();
            if(!offlinePreview && enemySpeedToggle.Checked) {enemySpeedManual=true;enemySpeedTimer.Start();await RefreshEnemySpeed();}
        };
        enemySpeedTimer.Tick+=async (_,_)=>await RefreshEnemySpeed();
        Shown+=(_,_)=> {if(!offlinePreview) enemySpeedTimer.Start();};
        FormClosed+=(_,_)=>enemySpeedTimer.Dispose();
        FormClosing+=async (_,eventArgs)=>
        {
            if(closingWithoutGame || offlinePreview || (!enemySpeedBusy && !GameItemScheduler.EnemySpeedHasEffects)) return;
            eventArgs.Cancel=true;
            enemySpeedClosing=true;enemySpeedManual=false;
            enemySpeedTimer.Start();
            status.Text="正在恢复敌人速度，请返回游戏等待完成后关闭";
            await RefreshEnemySpeed();
        };
        var row=(FlowLayoutPanel)WithFeatureShortcut(enemySpeedToggle,"enemySpeed");
        row.Controls.Add(enemySpeedValue);
        row.Controls.SetChildIndex(enemySpeedValue,1);
        return row;
    }

    private async Task RefreshEnemySpeed()
    {
        if(offlinePreview || enemySpeedBusy || (!enemySpeedToggle.Checked && !GameItemScheduler.EnemySpeedHasEffects && !enemySpeedClosing)) return;
        bool enabled=!enemySpeedClosing && enemySpeedToggle.Checked;
        decimal value=enemySpeedValue.Value;
        enemySpeedBusy=true;
        try
        {
            var result=await Task.Run(()=>GameItemScheduler.UpdateEnemySpeed(enabled,(float)value));
            if(IsDisposed) return;
            if((!enemySpeedClosing && enemySpeedToggle.Checked!=enabled) || enemySpeedValue.Value!=value) return;
            if(enemySpeedManual || enemySpeedClosing)
            {
                status.Text=result.Applied ? enabled?$"敌人速度 {value:0.##} 倍已应用，附近目标 {result.Count} 个":"敌人速度倍率已关闭"
                    :"敌人速度请求等待返回实际游玩场景";
                if(result.Applied && enemySpeedManual) {ToggleSounds.Play(enabled);enemySpeedManual=false;}
            }
            if(result.Applied && enemySpeedClosing && !GameItemScheduler.EnemySpeedHasEffects)
            {enemySpeedClosing=false;BeginInvoke(Close);}
        }
        catch(Exception ex)
        {
            if(!IsDisposed)
            {
                LogFailure("敌人速度倍率",ex,new {Enabled=enabled,Multiplier=value});
                status.Text="敌人速度操作未确认："+ex.Message;
                enemySpeedManual=false;
                enemySpeedTimer.Stop();
            }
        }
        finally {enemySpeedBusy=false;}
    }
}
