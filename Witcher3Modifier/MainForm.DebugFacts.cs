namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private static IEnumerable<string> AuxiliaryFeatureKeys => GameDebugFacts.Names.Keys.Concat(GameItemScheduler.AuxiliaryFeatures);
    private static bool ReadAuxiliaryFeature(string feature) => GameDebugFacts.Names.ContainsKey(feature)?GameDebugFacts.Read(feature):GameItemScheduler.ReadAuxiliary(feature);
    private static bool SetAuxiliaryFeature(string feature,bool enabled) => GameDebugFacts.Names.ContainsKey(feature)?GameDebugFacts.Set(feature,enabled):GameItemScheduler.SetAuxiliary(feature,enabled);
    private readonly Dictionary<string,CheckBox> debugFactToggles=[];
    private bool refreshingDebugFacts;

    private void AttachDebugFact(CheckBox box,string feature)
    {
        debugFactToggles.Add(feature,box);
        box.Checked=draft.Toggles.GetValueOrDefault(feature);
        box.CheckedChanged+=async (_,_)=>await ApplyDebugFact(feature,box);
    }

    private async Task ApplyDebugFact(string feature,CheckBox box)
    {
        if(refreshingDebugFacts) return;
        bool requested=box.Checked;
        box.Enabled=false;
        try
        {
            bool actual=offlinePreview?requested:await Task.Run(()=>SetAuxiliaryFeature(feature,requested));
            SaveRuntimeSettings();
            status.Text=$"{box.Text}已{(actual?"开启":"关闭")}";
            if(!offlinePreview) ToggleSounds.Play(actual);
        }
        catch(Exception ex)
        {
            LogFailure(box.Text,ex,new {Enabled=requested});
            refreshingDebugFacts=true;
            try {box.Checked=ReadAuxiliaryFeature(feature);}
            catch {box.Checked=!requested;}
            finally {refreshingDebugFacts=false;}
            status.Text=$"{box.Text}未完成：{ex.Message}";
        }
        finally {box.Enabled=true;}
    }

    private void RefreshDebugFacts()
    {
        refreshingDebugFacts=true;
        try
        {
            foreach(var (feature,box) in debugFactToggles)
                if(box.Enabled) box.Checked=ReadAuxiliaryFeature(feature);
        }
        catch(Exception ex) {status.Text=ex.Message;}
        finally {refreshingDebugFacts=false;}
    }
}
