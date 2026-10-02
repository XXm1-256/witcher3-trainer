using System.Diagnostics;
using System.Text.Json;

namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private sealed record MotionField(string Name,long Owner,long Storage,long Address);
    internal sealed record MotionBaseline(long Started,long Player,long Owner,long Storage,long Address,float Original,float Applied);
    private static readonly string MotionPath=Path.Combine(AppContext.BaseDirectory,"player-motion-runtime.json");
    private static List<MotionBaseline>? motionBaselines;

    private static MotionField[] MotionFields(nint handle,long game,long module,string feature)
    {
        long player=FunTarget(handle,game,module).Player;
        if(feature=="moveSpeed")
        {
            long controller=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,RespecProperty(handle,module,player,"defaultLocomotionController").Address)+8);
            if(!FunClass(handle,module,controller,"CR4LocomotionPlayerControllerScript")) throw new InvalidOperationException("玩家移动控制器尚未确认。");
            return new[]{("speedSlowWalkingMax",128),("speedWalkingMax",132),("speedRunning",136),("speedSprinting",140),("speedSprintingWithPerk",144)}
                .Select(pair=>MotionProperty(handle,module,controller,pair.Item1,pair.Item2)).ToArray();
        }
        if(feature!="jumpHeight") throw new ArgumentException("未知玩家移动功能。");
        int count=GameMoney.ReadInt32(handle,player+0xF8);long array=GameMoney.ReadInt64(handle,player+0xF0),jump=0;
        if(count is <1 or >256) throw new InvalidOperationException("玩家组件列表尚未确认。");
        for(int i=0;i<count;i++)
        {
            long component=GameMoney.ReadInt64(handle,array+i*8);
            if(component!=0 && FunClass(handle,module,component,"CExplorationStateJump")) {jump=component;break;}
        }
        if(jump==0) throw new InvalidOperationException("当前玩家跳跃组件尚未找到。");
        var fields=new List<MotionField>();
        foreach(var (name,offset) in new[]{("m_JumpParmsIdleS",300),("m_JumpParmsIdleToWalkS",392),("m_JumpParmsWalkS",484),("m_JumpParmsWalkHighS",576),("m_JumpParmsRunS",668),("m_JumpParmsSprintS",760),("m_JumpParmsFallS",852)})
        {
            var root=RespecProperty(handle,module,jump,name);
            long storage=GameMoney.ReadInt64(handle,jump+0x20);
            if(root.Address!=storage+offset) throw new InvalidOperationException("跳跃参数布局尚未确认。");
            long vertical=RespecFindField(handle,module,root.Type,"m_VerticalMovementS");
            if(GameMoney.ReadInt32(handle,vertical+0x20)!=8) throw new InvalidOperationException("跳跃垂直参数位置尚未确认。");
            long gravity=RespecFindField(handle,module,GameMoney.ReadInt64(handle,vertical+8),"m_GravityUpF");
            if(GameMoney.ReadInt32(handle,gravity+0x20)!=8) throw new InvalidOperationException("跳跃向上重力位置尚未确认。");
            fields.Add(new MotionField(name,jump,storage,root.Address+16));
        }
        return fields.ToArray();
    }

    private static MotionField MotionProperty(nint handle,long module,long owner,string name,int offset)
    {
        long storage=GameMoney.ReadInt64(handle,owner+0x20);
        var field=RespecProperty(handle,module,owner,name);
        if(field.Address!=storage+offset) throw new InvalidOperationException("玩家移动速度字段位置尚未确认。");
        return new MotionField(name,owner,storage,field.Address);
    }

    internal static object ReadPlayerMotion(string feature) => GameMoney.WithInventory(false,(handle,game,_,module)=>
        MotionFields(handle,game,module,feature).Select(field=>new {field.Name,field.Address,Value=GameMoney.ReadSingle(handle,field.Address)}).ToArray());

    internal static void SetPlayerMotion(string feature,bool enabled,float multiplier) => RunSerialized(()=>
        GameMoney.WithInventory(true,(handle,game,_,module)=>
        {
            if(!float.IsFinite(multiplier) || multiplier<.5f || multiplier>10) throw new ArgumentOutOfRangeException(nameof(multiplier));
            long player=FunTarget(handle,game,module).Player;
            using var process=Process.GetProcessesByName("witcher3").Single();
            long started=process.StartTime.ToUniversalTime().Ticks;
            motionBaselines ??= File.Exists(MotionPath)?JsonSerializer.Deserialize<List<MotionBaseline>>(File.ReadAllText(MotionPath))??[]:[];
            motionBaselines.RemoveAll(entry=>entry.Started!=started || entry.Player!=player);
            var fields=MotionFields(handle,game,module,feature);
            var writes=new List<(MotionField Field,float Value)>();
            foreach(var field in fields)
            {
                float current=GameMoney.ReadSingle(handle,field.Address);
                var prior=motionBaselines.SingleOrDefault(entry=>entry.Owner==field.Owner && entry.Storage==field.Storage && entry.Address==field.Address);
                if(!enabled && prior is null) continue;
                float original=prior?.Original??current;
                if(!float.IsFinite(original) || (feature=="moveSpeed" && original is <.01f or >10f) || (feature=="jumpHeight" && original is <-500f or >-.01f))
                    throw new InvalidOperationException("玩家移动参数数值未通过检查。");
                if(prior is not null && Math.Abs(current-prior.Applied)>.0001f && Math.Abs(current-original)>.0001f)
                    throw new InvalidOperationException("移动参数已被其他游戏逻辑改动，请重新载入后再使用。");
                float value=enabled?(feature=="moveSpeed"?original*multiplier:original/multiplier):original;
                if(prior is not null) motionBaselines.Remove(prior);
                motionBaselines.Add(new(started,player,field.Owner,field.Storage,field.Address,original,value));
                writes.Add((field,value));
            }
            // Persist original values before writing, so a trainer restart can undo this process's changes.
            File.WriteAllText(MotionPath+".tmp",JsonSerializer.Serialize(motionBaselines));
            File.Move(MotionPath+".tmp",MotionPath,true);
            GameProcessPause.Run(handle,()=>
            {
                if(FunTarget(handle,game,module).Player!=player || fields.Any(field=>GameMoney.ReadInt64(handle,field.Owner+0x20)!=field.Storage))
                    throw new InvalidOperationException("玩家对象已变化，本次操作未执行。");
                foreach(var (field,value) in writes) Write(handle,field.Address,BitConverter.GetBytes(value));
                foreach(var (field,value) in writes)
                    if(GameMoney.ReadSingle(handle,field.Address)!=value) throw new InvalidOperationException("移动参数回读失败。");
            });
            ErrorLog.Write("玩家移动参数完成",null,new {Feature=feature,Enabled=enabled,Multiplier=multiplier,Fields=writes.Count});
            return true;
        },extraAccess:0x800));
}
