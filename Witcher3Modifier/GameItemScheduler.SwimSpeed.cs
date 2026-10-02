using System.Diagnostics;
using System.Text.Json;

namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private sealed record SwimCauser(long Started,long Player,long Reference,int Id);
    private static readonly string SwimPath=Path.Combine(AppContext.BaseDirectory,"swim-speed-runtime.json");
    private static SwimCauser? swimCauser;
    private static bool swimLoaded;
    internal static bool SwimSpeedHasEffect => swimCauser is not null;
    internal sealed record SwimSpeedResult(bool Applied,bool Swimming);

    internal static SwimSpeedResult UpdateSwimSpeed(bool enabled,float multiplier) => RunSerialized<SwimSpeedResult>(()=>
    {
        if(!float.IsFinite(multiplier) || multiplier is <.5f or >10f) throw new ArgumentOutOfRangeException(nameof(multiplier));
        if(!swimLoaded)
        {
            swimCauser=File.Exists(SwimPath)?JsonSerializer.Deserialize<SwimCauser>(File.ReadAllText(SwimPath)):null;
            swimLoaded=true;
        }
        using var process=Process.GetProcessesByName("witcher3").SingleOrDefault();
        if(process is null) {SaveSwimCauser(null);return new(!enabled,false);}
        long started=process.StartTime.ToUniversalTime().Ticks;
        var scene=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=FunTarget(handle,game,module).Player;
            long swimming=RespecProperty(handle,module,player,"isSwimming").Address;
            byte value=GameMoney.ReadBytes(handle,swimming,1)[0];
            if(value>1) throw new InvalidOperationException("游泳状态未通过检查。");
            return (Player:player,Reference:GameMoney.ReadInt64(handle,player+8),Swimming:value!=0,Paused:GameMoney.ReadInt32(handle,game+0x160)!=0);
        });
        if(swimCauser is not null && (swimCauser.Started!=started || swimCauser.Player!=scene.Player || swimCauser.Reference!=scene.Reference)) SaveSwimCauser(null);
        if(!enabled || !scene.Swimming)
        {
            if(swimCauser is null) return new(true,scene.Swimming);
            if(scene.Paused) return new(false,scene.Swimming);
            if(ReadEnemyCauser(scene.Player,swimCauser.Id) is not null)
            {
                var target=ActorScriptTarget("ResetAnimationSpeedMultiplier",1);
                if(target.Player!=scene.Player || target.Reference!=scene.Reference) throw new InvalidOperationException("游泳角色已变化。");
                InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,target.Player,target.Reference,BitConverter.GetBytes(swimCauser.Id));
                if(ReadEnemyCauser(scene.Player,swimCauser.Id) is not null) throw new InvalidOperationException("游泳倍率撤销未回读确认。");
            }
            SaveSwimCauser(null);
            return new(true,scene.Swimming);
        }
        if(swimCauser is not null && ReadEnemyCauser(scene.Player,swimCauser.Id) is {} actual && Math.Abs(actual-multiplier)<.0001f) return new(true,true);
        if(scene.Paused) return new(false,true);
        var apply=ActorScriptTarget("SetAnimationSpeedMultiplier",2);
        if(apply.Player!=scene.Player || apply.Reference!=scene.Reference) throw new InvalidOperationException("游泳角色已变化。");
        int id=BitConverter.ToInt32(InvokeActorScript(apply.Game,apply.Player,apply.Function,apply.Entry,apply.Player,apply.Reference,
            [..BitConverter.GetBytes(multiplier),..BitConverter.GetBytes(swimCauser?.Id??-1)]));
        if(id<0) throw new InvalidOperationException("游戏未接受游泳倍率。");
        SaveSwimCauser(new(started,scene.Player,scene.Reference,id));
        if(ReadEnemyCauser(scene.Player,id) is not {} confirmed || Math.Abs(confirmed-multiplier)>.0001f) throw new InvalidOperationException("游泳倍率未回读确认。");
        return new(true,true);
    });

    private static void SaveSwimCauser(SwimCauser? value)
    {
        if(swimLoaded && swimCauser==value) return;
        File.WriteAllText(SwimPath+".tmp",JsonSerializer.Serialize(value));
        File.Move(SwimPath+".tmp",SwimPath,true);
        swimCauser=value;
    }
}
