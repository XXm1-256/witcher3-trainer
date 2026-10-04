namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TrainerSwitch autoLootToggle=new() {Text="五米内拾取与采集",AutoSize=true};
    private readonly System.Windows.Forms.Timer autoLootTimer=new() {Interval=1000};
    private bool autoLootBusy,autoLootManual,autoLootClosing,refreshingAutoLoot;
    private int autoLootRequested;
    private string? autoLootWait;

    private Control BuildAutoLoot()
    {
        autoLootToggle.Checked=draft.SaveSwitchesAndMultipliers && draft.Toggles.GetValueOrDefault("autoLoot");
        autoLootRequested=autoLootToggle.Checked?1:0;
        autoLootToggle.CheckedChanged+=async (_,_)=>
        {
            if(refreshingAutoLoot) return;
            Volatile.Write(ref autoLootRequested,autoLootToggle.Checked?1:0);
            SaveRuntimeSettings();
            if(offlinePreview) return;
            if(!autoLootToggle.Checked)
            {
                autoLootManual=false;autoLootTimer.Stop();
                status.Text=autoLootBusy?"自动拾取已停止接收新容器，当前容器完成后停止":"五米内自动拾取已关闭";
                ToggleSounds.Play(false);
            }
            else {autoLootManual=true;autoLootTimer.Start();await RefreshAutoLoot();}
        };
        autoLootTimer.Tick+=async (_,_)=>await RefreshAutoLoot();
        Shown+=async (_,_)=> {if(!offlinePreview && autoLootToggle.Checked){autoLootTimer.Start();await RefreshAutoLoot();}};
        FormClosing+=(_,args)=>
        {
            Volatile.Write(ref autoLootRequested,0);autoLootTimer.Stop();
            if(!closingWithoutGame && autoLootBusy){args.Cancel=true;autoLootClosing=true;status.Text="正在完成当前容器拾取并释放查询结果";}
        };
        FormClosed+=(_,_)=>autoLootTimer.Dispose();
        return WithFeatureShortcut(autoLootToggle,"autoLoot");
    }

    private async Task RefreshAutoLoot()
    {
        if(offlinePreview || autoLootBusy || !autoLootToggle.Checked || autoLootClosing) return;
        autoLootBusy=true;
        try
        {
            var result=await Task.Run(()=>GameItemScheduler.RunAutoLoot(true,()=>Volatile.Read(ref autoLootRequested)!=0));
            if(IsDisposed || !autoLootToggle.Checked || autoLootClosing) return;
            if(result.Ready && autoLootWait is not null)
            {
                autoLootWait=null;status.Text="五米内自动拾取已恢复";
            }
            if(autoLootManual)
            {
                status.Text=result.Ready?"五米内自动拾取已开启":"自动拾取等待返回实际游玩场景";
                if(result.Ready){ErrorLog.Write("自动拾取首次查询",null,result);ToggleSounds.Play(true);autoLootManual=false;}
            }
            if(result.Transferred>0) status.Text=$"拾取完成：{result.Transferred}件，其中草药{result.Harvested}件";
        }
        catch(Exception ex) when(GameItemScheduler.IsAutoLootWait(ex))
        {
            if(!IsDisposed && autoLootToggle.Checked && !autoLootClosing)
            {
                if(autoLootWait!=ex.Message) ErrorLog.Write("自动拾取等待",ex);
                autoLootWait=ex.Message;
                status.Text="自动拾取等待返回游玩场景，将自动继续";
            }
        }
        catch(Exception ex)
        {
            if(!IsDisposed)
            {
                LogFailure("五米内自动拾取",ex);
                autoLootTimer.Stop();Volatile.Write(ref autoLootRequested,0);autoLootManual=false;
                refreshingAutoLoot=true;autoLootToggle.Checked=false;refreshingAutoLoot=false;SaveRuntimeSettings();
                status.Text="自动拾取已停止："+ex.Message;
            }
        }
        finally {autoLootBusy=false;if(autoLootClosing && !IsDisposed) BeginInvoke(Close);}
    }
}
