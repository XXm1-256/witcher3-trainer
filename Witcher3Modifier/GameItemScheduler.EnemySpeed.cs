using System.Diagnostics;

namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private sealed record EnemyArray(long Game,long Player,long Type,byte[] Header,Dictionary<long,long> Actors);
    private static EnemyArray? enemyArray;
    private static readonly Dictionary<long,int> enemyCausers=[];
    private static int enemyProcessId;
    internal static bool EnemySpeedHasEffects => enemyArray is not null || enemyCausers.Count!=0;
    internal sealed record EnemySpeedResult(bool Applied,int Count);

    internal static EnemySpeedResult UpdateEnemySpeed(bool enabled,float multiplier) => RunSerialized(()=>
    {
        if(!float.IsFinite(multiplier) || multiplier is <.1f or >10f) throw new ArgumentOutOfRangeException(nameof(multiplier));
        using var process=Process.GetProcessesByName("witcher3").SingleOrDefault();
        if(process is null || process.Id!=enemyProcessId)
        {
            enemyArray=null;enemyCausers.Clear();enemyProcessId=process?.Id??0;
            if(process is null) return new EnemySpeedResult(!enabled,0);
        }
        var scene=GameMoney.WithInventory(false,(handle,game,_,module)=>
            (Target:FunTarget(handle,game,module),Paused:GameMoney.ReadInt32(handle,game+0x160)!=0));
        if(scene.Paused) return new EnemySpeedResult(!enabled && enemyArray is null,enemyCausers.Count);
        if(enemyArray is not null && (enemyArray.Game!=scene.Target.Game || enemyArray.Player!=scene.Target.Player))
        {
            ReleaseActorArray(enemyArray.Game,enemyArray.Player,enemyArray.Type,enemyArray.Header);
            enemyArray=null;enemyCausers.Clear();
        }
        if(!enabled)
        {
            if(enemyArray is not null)
            {
                foreach(var (actor,reference) in enemyArray.Actors) RemoveEnemyCauser(actor,reference);
                ReleaseActorArray(enemyArray.Game,enemyArray.Player,enemyArray.Type,enemyArray.Header);
                enemyArray=null;enemyCausers.Clear();
            }
            return new EnemySpeedResult(true,0);
        }
        EnemyArray next=QueryEnemyArray();
        try
        {
            if(enemyArray is not null)
                foreach(var (actor,reference) in enemyArray.Actors.Where(pair=>!next.Actors.ContainsKey(pair.Key))) RemoveEnemyCauser(actor,reference);
            foreach(var (actor,reference) in next.Actors)
            {
                int existing=enemyCausers.GetValueOrDefault(actor,-1);
                if(existing>=0 && ReadEnemyCauser(actor,existing) is {} current && Math.Abs(current-multiplier)<.0001f) continue;
                var target=ActorScriptTarget("SetAnimationSpeedMultiplier",2,actor);
                if(target.Reference!=reference || target.Player!=next.Player) throw new InvalidOperationException("敌人查询期间对象已变化。");
                int id=BitConverter.ToInt32(InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,actor,reference,
                    [..BitConverter.GetBytes(multiplier),..BitConverter.GetBytes(existing)]));
                if(id>=0) enemyCausers[actor]=id;
                if(id<0 || ReadEnemyCauser(actor,id) is not {} actual || Math.Abs(actual-multiplier)>.0001f)
                    throw new InvalidOperationException("敌人速度来源未回读确认。");
            }
            if(enemyArray is not null) ReleaseActorArray(enemyArray.Game,enemyArray.Player,enemyArray.Type,enemyArray.Header);
            enemyArray=next;
            return new EnemySpeedResult(true,next.Actors.Count);
        }
        catch
        {
            // Keep references to any newly affected actors so a later disable can undo them.
            if(enemyArray is null) enemyArray=next;
            else
            {
                foreach(var (actor,reference) in next.Actors.Where(pair=>!enemyArray.Actors.ContainsKey(pair.Key))) RemoveEnemyCauser(actor,reference);
                ReleaseActorArray(next.Game,next.Player,next.Type,next.Header);
            }
            throw;
        }
    });

    private static EnemyArray QueryEnemyArray(int queryFlags=0x105)
    {
        var target=ActorScriptTarget("GetNPCsAndPlayersInRange",4);
        GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            long destructor=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,target.ReturnType)+0x70);
            if(destructor!=module+GameVersion.Rva(0x26FC970)) throw new InvalidOperationException("敌人查询结果类型未确认。");
            return true;
        });
        // Verified native enum: exclude player 1, alive actors 4, hostile attitude 0x100. None name is 0.
        byte[] array=InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,target.Player,target.Reference,
            [..BitConverter.GetBytes(80f),..BitConverter.GetBytes(64),..BitConverter.GetBytes(0),..BitConverter.GetBytes(queryFlags)]);
        try
        {
            var actors=GameMoney.WithInventory(false,(handle,game,_,module)=>
            {
                if(game!=target.Game || FunTarget(handle,game,module).Player!=target.Player) throw new InvalidOperationException("敌人查询期间场景已变化。");
                // REDengine's array header is a pointer plus a 32-bit count (12 bytes).
                int count=BitConverter.ToInt32(array,8);
                long data=BitConverter.ToInt64(array);
                if(count is <0 or >64 || (count>0 && data==0))
                    throw new InvalidOperationException("敌人查询数组布局未确认。");
                var result=new Dictionary<long,long>();
                for(int i=0;i<count;i++)
                {
                    long reference=GameMoney.ReadInt64(handle,data+i*8);
                    if(reference==0) continue;
                    long actor=GameMoney.ReadInt64(handle,reference+8);
                    if(actor==0 || actor==target.Player) continue;
                    if(!FunClass(handle,module,actor,"CActor") || GameMoney.ReadInt64(handle,actor+8)!=reference)
                        throw new InvalidOperationException("敌人查询对象归属未确认。");
                    result[actor]=reference;
                }
                return result;
            });
            return new EnemyArray(target.Game,target.Player,target.ReturnType,array,actors);
        }
        catch {ReleaseActorArray(target.Game,target.Player,target.ReturnType,array);throw;}
    }

    private static float? ReadEnemyCauser(long actor,int id) => GameMoney.WithInventory(false,(handle,_,_,module)=>
    {
        var property=RespecProperty(handle,module,actor,"animationMultiplierCausers");
        long address=property.Address,type=GameMoney.ReadInt64(handle,property.Type+8);
        if(GameMoney.ReadInt32(handle,type+0x2C)!=ResolveName(handle,module,"SAnimMultiplyCauser") || GameMoney.ReadInt32(handle,type+0x60)!=8)
            throw new InvalidOperationException("敌人速度来源的元素类型未确认。");
        int idOffset=RespecField(handle,module,type,"id"),mulOffset=RespecField(handle,module,type,"mul");
        int count=GameMoney.ReadInt32(handle,address+8);
        if(count is <0 or >128) throw new InvalidOperationException("敌人速度来源数组异常。");
        long data=GameMoney.ReadInt64(handle,address);
        for(int i=0;i<count;i++)
            if(GameMoney.ReadInt32(handle,data+i*8+idOffset)==id) return (float?)BitConverter.ToSingle(GameMoney.ReadBytes(handle,data+i*8+mulOffset,4));
        return null;
    });

    private static void RemoveEnemyCauser(long actor,long reference)
    {
        if(!enemyCausers.TryGetValue(actor,out int id)) return;
        if(ReadEnemyCauser(actor,id) is not null)
        {
            var target=ActorScriptTarget("ResetAnimationSpeedMultiplier",1,actor);
            if(target.Reference!=reference) throw new InvalidOperationException("待恢复敌人的对象已变化。");
            InvokeActorScript(target.Game,target.Player,target.Function,target.Entry,actor,reference,BitConverter.GetBytes(id));
            if(ReadEnemyCauser(actor,id) is not null) throw new InvalidOperationException("敌人速度恢复未回读确认。");
        }
        enemyCausers.Remove(actor);
    }
}
