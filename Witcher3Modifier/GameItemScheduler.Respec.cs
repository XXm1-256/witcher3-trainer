namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private const int RespecJobOffset=0x1900;
    private static readonly byte[] RespecJobCode=Convert.FromHexString("534883EC204C89C34C8B5230488B8160FD00004885C07423488B5008493B12751A498B4A08498B4210498B52184531C04C8D4B08FFD00FB6C089034883C4205BC3");
    private static readonly byte[] RespecInvokePrefix=Convert.FromHexString("405541544155415641574881EC70010000488D6C24208B81900000004D8BF94C8BF248899D88010000498BD84889B5900100004889BD98010000488BF948038188000000");
    internal sealed record RespecState(long Player,long Ability,int Free,int Used,int Learned,int PathPoints);

    internal static RespecState ReadRespecState() => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        long level=RespecManager(handle,module,player,"levelManager");
        long ability=RespecManager(handle,module,player,"abilityManager");
        var points=RespecArray(handle,module,level,"points","SSpendablePoints",16);
        if(points.Count!=2 || points.Stride!=8) throw new InvalidOperationException("技能点数组布局未确认。");
        int free=GameMoney.ReadInt32(handle,points.Data+RespecField(handle,module,points.Type,"free"));
        int used=GameMoney.ReadInt32(handle,points.Data+RespecField(handle,module,points.Type,"used"));
        if(free is <0 or >1000000 || used is <0 or >1000000) throw new InvalidOperationException("技能点数据异常。");
        var skills=RespecArray(handle,module,ability,"skills","SSkill",512);
        int levelOffset=RespecField(handle,module,skills.Type,"level"),coreOffset=RespecField(handle,module,skills.Type,"isCoreSkill");
        int learned=0;
        for(int index=0;index<skills.Count;index++)
        {
            long item=skills.Data+index*skills.Stride;
            if(GameMoney.ReadBytes(handle,item+coreOffset,1)[0]==0 && GameMoney.ReadInt32(handle,item+levelOffset)>0) learned++;
        }
        long path=RespecProperty(handle,module,ability,"pathPointsSpent").Address;
        int count=GameMoney.ReadInt32(handle,path+8);
        if(count is <1 or >16) throw new InvalidOperationException("技能分支数组布局未确认。");
        long data=GameMoney.ReadInt64(handle,path);
        int spent=0;
        for(int index=0;index<count;index++) spent=checked(spent+GameMoney.ReadInt32(handle,data+index*4));
        return new RespecState(player,ability,free,used,learned,spent);
    });

    internal static int ReadRespecMutationPoints() => RunSerialized(()=>
    {
        var target=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
            long ability=RespecManager(handle,module,player,"abilityManager");
            long fn=FindAuxiliaryFunction(handle,module,ability,"GetMutationsUsedSkillPoints","W3PlayerAbilityManager",0);
            return (Game:game,Player:player,Context:ability,Function:fn,Entry:RespecCallEntry(handle,module));
        });
        byte[] result=InvokeRespecFunction(target.Game,target.Player,target.Function,target.Entry,target.Context);
        return BitConverter.ToInt32(result,8);
    });

    internal static int ResetBuild() => RunSerialized(()=>
    {
        var before=ReadRespecState();
        int mutations=ReadRespecMutationPoints();
        if(mutations<0 || mutations>before.Used) throw new InvalidOperationException("突变研究技能点尚未确认，本次洗点未执行。");
        var target=GameMoney.WithInventory(false,(handle,_,_,module)=>
            (Game:GameMoney.ReadInt64(handle,module+GameVersion.Rva(0x5A750E8)),Function:FindAuxiliaryFunction(handle,module,before.Player,"ResetCharacterDev","W3PlayerWitcher",0),Entry:RespecCallEntry(handle,module)));
        if(before.Learned!=0 || before.PathPoints!=0 || before.Used!=mutations)
            InvokeRespecFunction(target.Game,before.Player,target.Function,target.Entry,before.Player);
        var after=ReadRespecState();
        if(after.Player!=before.Player || after.Ability!=before.Ability || after.Learned!=0 || after.PathPoints!=0 ||
            after.Used!=mutations || after.Free!=before.Free+before.Used-mutations)
            throw new InvalidOperationException("洗点已提交，但技能与返点结果未完整确认；请检查角色页，勿重复操作。");
        int refunded=after.Free-before.Free;
        ErrorLog.Write("一键洗点完成",null,new {Before=before,After=after,MutationPoints=mutations,Refunded=refunded});
        return refunded;
    });

    private static byte[] InvokeRespecFunction(long game,long player,long function,long entry,long context)
    {
        byte[] args=[..BitConverter.GetBytes(player),..BitConverter.GetBytes(function),..BitConverter.GetBytes(entry),..BitConverter.GetBytes(context)];
        byte[] result=ExecuteEquipmentNative(0,args,contextOverride:game,respec:true);
        if(BitConverter.ToInt32(result)!=1) throw new InvalidOperationException("洗点调用未完成，游戏场景可能已变化。");
        return result;
    }
    private static long RespecCallEntry(nint handle,long module)
    {
        long entry=module+GameVersion.Rva(0x26F5580);
        if(!GameMoney.ReadBytes(handle,entry,RespecInvokePrefix.Length).SequenceEqual(RespecInvokePrefix))
            throw new InvalidOperationException("洗点的游戏完整调用入口未通过检查。");
        return entry;
    }
    private static long RespecManager(nint handle,long module,long player,string name) =>
        GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,RespecProperty(handle,module,player,name).Address)+8);
    private static (long Address,long Type) RespecProperty(nint handle,long module,long obj,string name)
    {
        long cls=GameMoney.ReadInt64(handle,obj+0x18);
        for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
        {
            long property=RespecFindField(handle,module,cls,name,true);
            if(property==0) continue;
            long storage=(GameMoney.ReadInt32(handle,property+0x24)&0x20)!=0?GameMoney.ReadInt64(handle,obj+0x20):obj;
            return (storage+GameMoney.ReadInt32(handle,property+0x20),GameMoney.ReadInt64(handle,property+8));
        }
        throw new InvalidOperationException($"洗点属性尚未确认：{name}。");
    }
    private static long RespecFindField(nint handle,long module,long type,string name,bool optional=false)
    {
        int id=ResolveName(handle,module,name),count=GameMoney.ReadInt32(handle,type+0x38);
        if(count is <0 or >4096) throw new InvalidOperationException("洗点属性表结构异常。");
        long table=GameMoney.ReadInt64(handle,type+0x30);
        for(int i=0;i<count;i++)
        {
            long field=GameMoney.ReadInt64(handle,table+i*8);
            if(GameMoney.ReadInt32(handle,field+0x10)==id) return field;
        }
        if(optional) return 0;
        throw new InvalidOperationException($"洗点字段尚未确认：{name}。");
    }
    private static int RespecField(nint handle,long module,long type,string name)
    {
        int offset=GameMoney.ReadInt32(handle,RespecFindField(handle,module,type,name)+0x20);
        if(offset<0 || offset>=GameMoney.ReadInt32(handle,type+0x60)) throw new InvalidOperationException("洗点字段位置异常。");
        return offset;
    }
    private static (long Data,int Count,long Type,int Stride) RespecArray(nint handle,long module,long obj,string name,string element,int limit)
    {
        var property=RespecProperty(handle,module,obj,name);
        long type=GameMoney.ReadInt64(handle,property.Type+8);
        int count=GameMoney.ReadInt32(handle,property.Address+8),stride=GameMoney.ReadInt32(handle,type+0x60);
        if(GameMoney.ReadInt32(handle,type+0x2C)!=ResolveName(handle,module,element) || count is <1 || count>limit || stride is <1 or >1024)
            throw new InvalidOperationException($"洗点数组尚未确认：{name}。");
        return (GameMoney.ReadInt64(handle,property.Address),count,type,stride);
    }
}

