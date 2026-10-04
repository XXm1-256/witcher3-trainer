namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private static int lootQueryProcess;
    private static long lootQueryOriginal,lootQueryPrivate;
    private static byte[] lootQuerySource=[];
    private static (int Process,long Started,long Game,long Player,long Type,byte[] Array)? lootCleanup;

    internal static bool IsAutoLootWait(Exception error) => error is TimeoutException ||
        error is InvalidOperationException && error.Message is
            "未找到运行中的游戏。" or
            "游戏场景正在切换，请稍后重试。" or
            "请在杰洛特的实际游玩场景使用此功能。" or
            "未能确认游戏场景对象结构，本次操作未执行；请先载入存档。" or
            "未能确认玩家对象结构，本次操作未执行；请先载入存档。" or
            "未能确认背包对象结构，本次操作未执行；请先载入存档。" or
            "自动拾取期间场景已变化。" or
            "角色脚本调用未执行，当前场景或对象可能已变化。" or
            "装备请求尚未完成。";

    private static void CompleteLootCleanup()
    {
        if(lootCleanup is not {} pending) return;
        ReleaseActorArray(pending.Game,pending.Player,pending.Type,pending.Array);
        lootCleanup=null;
    }

    internal static byte[] BuildLootQueryCode(byte[] source)
    {
        byte[] result=(byte[])source.Clone();
        int match=-1;
        for(int i=0;i+28<=source.Length;i++)
            if(source[i]==0x63 && source[i+9]==4 && BitConverter.ToInt32(source,i+10)==0x40 &&
                source[i+14]==0x63 && source.AsSpan(i+1,8).SequenceEqual(source.AsSpan(i+15,8)) &&
                source[i+23]==4 && BitConverter.ToInt32(source,i+24)==0x100)
            {
                if(match>=0) throw new InvalidOperationException("附近物品查询脚本不匹配。");
                match=i;
            }
        if(match<0) throw new InvalidOperationException("附近物品查询脚本不匹配。");
        Array.Clear(result,match+10,4);Array.Clear(result,match+24,4);
        return result;
    }

    private static long LootQueryFunction(nint handle,long original)
    {
        int size=GameMoney.ReadInt32(handle,original+0x90);
        if(size is <28 or >2048) throw new InvalidOperationException("附近物品查询脚本长度异常。");
        byte[] source=GameMoney.ReadBytes(handle,GameMoney.ReadInt64(handle,original+0x88),size);
        using var process=System.Diagnostics.Process.GetProcessesByName("witcher3").Single();
        if(lootQueryProcess==process.Id && lootQueryOriginal==original && source.SequenceEqual(lootQuerySource)) return lootQueryPrivate;
        byte[] code=BuildLootQueryCode(source),header=GameMoney.ReadBytes(handle,original,0xA0);
        long allocation=VirtualAllocEx(handle,0,0x1000,0x3000,0x04).ToInt64();
        if(allocation==0) throw new InvalidOperationException("无法准备附近物品查询。");
        try
        {
            BitConverter.GetBytes(allocation+0x100).CopyTo(header,0x88);
            Write(handle,allocation,header);Write(handle,allocation+0x100,code);
            if(!GameMoney.ReadBytes(handle,allocation,header.Length).SequenceEqual(header) ||
                !GameMoney.ReadBytes(handle,allocation+0x100,code.Length).SequenceEqual(code))
                throw new InvalidOperationException("附近物品查询准备失败。");
        }
        catch {VirtualFreeEx(handle,(nint)allocation,0,0x8000);throw;}
        // Keep the small private function alive until process exit, including delayed game-thread requests.
        lootQueryProcess=process.Id;lootQueryOriginal=original;lootQuerySource=source;lootQueryPrivate=allocation;
        return allocation;
    }
    private static long LootGlobalFunction(nint handle,long module,string name,int parameters)
    {
        long system=GameMoney.ReadInt64(handle,module+GameVersion.Rva(0x5CC5148));
        int buckets=GameMoney.ReadInt32(handle,system+0x40),count=GameMoney.ReadInt32(handle,system+0x44);
        if(buckets is <1 or >1000000 || count is <1 or >1000000)
            throw new InvalidOperationException("附近物品查询函数表尚未确认。");
        int id=ResolveName(handle,module,name);
        long node=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,system+0x60)+(uint)id%(uint)buckets*8);
        for(int i=0;node!=0 && i<count;i++,node=GameMoney.ReadInt64(handle,node+0x18))
        {
            if(GameMoney.ReadInt32(handle,node)!=id || GameMoney.ReadInt32(handle,node+0x10)!=id) continue;
            long fn=GameMoney.ReadInt64(handle,node+8);
            if(fn==0 || GameMoney.ReadInt64(handle,fn)!=module+GameVersion.Rva(0x3A82D58) ||
                GameMoney.ReadInt32(handle,fn+0x10)!=id || GameMoney.ReadInt32(handle,fn+0x30)!=parameters)
                throw new InvalidOperationException("附近物品查询函数元数据未通过检查。");
            return fn;
        }
        throw new InvalidOperationException("附近物品查询入口未找到。");
    }

    internal static object ReadAutoLootMetadata() => GameMoney.WithInventory(false,(handle,_,_,module)=>
    {
        long fn=LootGlobalFunction(handle,module,"GetNonFriendlyGameplayEntitiesInRange",5);
        int codeSize=GameMoney.ReadInt32(handle,fn+0x90);
        if(codeSize is <1 or >4096) throw new InvalidOperationException("查询脚本长度异常。");
        return (object)new {Function=$"0x{fn:X}",ReturnProperty=$"0x{GameMoney.ReadInt64(handle,fn+0x20):X}",
            FunctionBytes=Convert.ToHexString(GameMoney.ReadBytes(handle,fn,0xA0)),
            Code=Convert.ToHexString(GameMoney.ReadBytes(handle,GameMoney.ReadInt64(handle,fn+0x88),codeSize)),
            Parameters=Enumerable.Range(0,5).Select(i=>
            {
                long p=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,fn+0x28)+i*8);
                long type=GameMoney.ReadInt64(handle,p+8);
                return new {Offset=GameMoney.ReadInt32(handle,p+0x20),Flags=GameMoney.ReadInt32(handle,p+0x24),
                    Name=GameMoney.ReadInt32(handle,p+0x10),Type=$"0x{type:X}",
                    TypeBytes=Convert.ToHexString(GameMoney.ReadBytes(handle,type,64)),
                    TypeCopy=$"0x{GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,type)+0x58)-module:X}"};
            }).ToArray()};
    });

    internal sealed record AutoLootResult(bool Ready,int Containers,int Transferred,int Skipped,int Entities=0,int Harvested=0);

    private static (long Game,long Player,long Function,long Entry,long Reference) LootQueryTarget() =>
        GameMoney.WithInventory(true,(handle,game,_,module)=>
        {
            long player=FunTarget(handle,game,module).Player;
            long fn=LootGlobalFunction(handle,module,"GetNonFriendlyGameplayEntitiesInRange",5);
            string[] names=["center","range","attitudeReferenceActor","maxResults","tag"];
            int[] offsets=[0,8,16,24,28];
            for(int i=0;i<names.Length;i++)
            {
                long p=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,fn+0x28)+i*8);
                if(GameMoney.ReadInt32(handle,p+0x10)!=ResolveName(handle,module,names[i]) ||
                    GameMoney.ReadInt32(handle,p+0x20)!=offsets[i] || (GameMoney.ReadInt32(handle,p+0x24)&0x80)==0)
                    throw new InvalidOperationException("附近物品查询参数未通过检查。");
            }
            return (game,player,LootQueryFunction(handle,fn),RespecCallEntry(handle,module),GameMoney.ReadInt64(handle,player+8));
        });

    private static long LootMethod(nint handle,long module,long obj,string name,int parameters)
    {
        int id=ResolveName(handle,module,name);
        long cls=GameMoney.ReadInt64(handle,obj+0x18);
        for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
        {
            int count=GameMoney.ReadInt32(handle,cls+0x50);
            if(count is <0 or >4096) throw new InvalidOperationException("拾取对象函数表异常。");
            long list=GameMoney.ReadInt64(handle,cls+0x48);
            for(int i=0;i<count;i++)
            {
                long fn=GameMoney.ReadInt64(handle,list+i*8);
                if(GameMoney.ReadInt32(handle,fn+0x10)!=id) continue;
                if(GameMoney.ReadInt64(handle,fn)!=module+GameVersion.Rva(0x3A82D58) ||
                    GameMoney.ReadInt64(handle,fn+8)!=cls || GameMoney.ReadInt32(handle,fn+0x30)!=parameters)
                    throw new InvalidOperationException("拾取对象函数元数据异常。");
                return fn;
            }
        }
        throw new InvalidOperationException($"拾取对象入口未找到：{name}。");
    }

    private static byte[] LootCall(long obj,long reference,string name) 
    {
        var t=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            if(GameMoney.ReadInt64(handle,reference+8)!=obj || GameMoney.ReadInt64(handle,obj+8)!=reference)
                throw new InvalidOperationException("拾取对象已卸载。");
            return (Game:game,Player:FunTarget(handle,game,module).Player,Fn:LootMethod(handle,module,obj,name,0),Entry:RespecCallEntry(handle,module));
        });
        return InvokeActorScript(t.Game,t.Player,t.Fn,t.Entry,obj,reference,[]);
    }

    private static bool LootFlag(nint handle,long module,long obj,string name) =>
        GameMoney.ReadBytes(handle,RespecProperty(handle,module,obj,name).Address,1)[0]!=0;

    private static (long Inventory,int Quantity) LootInventory(nint handle,long module,long obj)
    {
        long reference=GameMoney.ReadInt64(handle,RespecProperty(handle,module,obj,"inv").Address);
        long inventory=reference==0?0:GameMoney.ReadInt64(handle,reference+8);
        if(inventory==0) return (0,0);
        if(GameMoney.ReadInt64(handle,inventory)!=module+GameVersion.Rva(0x384F4F0))
            throw new InvalidOperationException("容器库存类型未通过检查。");
        int count=GameMoney.ReadInt32(handle,inventory+0x148);
        if(count is <0 or >10000) throw new InvalidOperationException("容器物品数量异常。");
        long items=GameMoney.ReadInt64(handle,inventory+0x140);
        int total=0;
        for(int i=0;i<count;i++) total=checked(total+Math.Max(0,GameMoney.ReadInt32(handle,items+i*0x98+0x54)));
        return (inventory,total);
    }

    internal static AutoLootResult RunAutoLoot(bool take,Func<bool>? continueLoot=null) => RunSerialized(()=>
    {
        using var process=System.Diagnostics.Process.GetProcessesByName("witcher3").SingleOrDefault();
        if(process is null) return new AutoLootResult(false,0,0,0);
        long started=process.StartTime.ToUniversalTime().Ticks;
        if(lootCleanup is {} pending && (pending.Process!=process.Id || pending.Started!=started)) lootCleanup=null;
        bool paused=GameMoney.WithInventory(false,(handle,game,_,_)=>GameMoney.ReadInt32(handle,game+0x160)!=0);
        if(paused) return new AutoLootResult(false,0,0,0);
        // Finish ownership cleanup before issuing another query after a pause or scene transition.
        CompleteLootCleanup();
        var t=LootQueryTarget();
        long type=GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            long result=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,t.Function+0x20)+8);
            if(GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,result)+0x70)!=module+GameVersion.Rva(0x26FC970))
                throw new InvalidOperationException("附近物品查询数组的释放类型未确认。");
            return result;
        });
        // The private wrapper has zero query flags, selecting the game's general entity storage.
        byte[] array=InvokeActorScript(t.Game,t.Player,t.Function,t.Entry,t.Player,t.Reference,
            [..BitConverter.GetBytes(t.Reference),..BitConverter.GetBytes(5f),..new byte[4],
             ..BitConverter.GetBytes(0L),..BitConverter.GetBytes(256),..BitConverter.GetBytes(0)]);
        int containers=0,transferred=0,skipped=0,harvested=0;
        try
        {
            int count=BitConverter.ToInt32(array,8);long data=BitConverter.ToInt64(array);
            if(count is <0 or >256 || (count>0 && data==0)) throw new InvalidOperationException("附近物品查询结果异常。");
            for(int i=0;i<count;i++)
            {
                if(take && continueLoot is not null && !continueLoot()) break;
                if(GameMoney.WithInventory(false,(handle,game,_,_)=>game!=t.Game || GameMoney.ReadInt32(handle,game+0x160)!=0)) break;
                var c=GameMoney.WithInventory(false,(handle,game,_,module)=>
                {
                    if(game!=t.Game || FunTarget(handle,game,module).Player!=t.Player)
                        throw new InvalidOperationException("自动拾取期间场景已变化。");
                    long reference=GameMoney.ReadInt64(handle,data+i*8);
                    long obj=reference==0?0:GameMoney.ReadInt64(handle,reference+8);
                    if(obj==0 || !FunClass(handle,module,obj,"W3Container")) return (Obj:0L,Reference:0L,Quantity:0,Allowed:false,Herb:false);
                    bool allowed=!LootFlag(handle,module,obj,"disableLooting") && !LootFlag(handle,module,obj,"lockedByKey") &&
                        !LootFlag(handle,module,obj,"isInteractionBlocked") && LootFlag(handle,module,obj,"disableStealing");
                    long fact=RespecProperty(handle,module,obj,"factOnContainerOpened").Address;
                    allowed&=GameMoney.ReadInt32(handle,fact+8)<=1;
                    return (Obj:obj,Reference:reference,Quantity:LootInventory(handle,module,obj).Quantity,Allowed:allowed,Herb:FunClass(handle,module,obj,"W3Herb"));
                });
                if(c.Obj==0 || c.Quantity==0) continue;
                containers++;
                if(!c.Allowed || !take){skipped++;continue;}
                if(LootCall(c.Obj,c.Reference,"IsEmpty")[0]!=0 || LootCall(c.Obj,c.Reference,"HasQuestItem")[0]!=0){skipped++;continue;}
                if(!GameMoney.WithInventory(false,(handle,game,_,module)=>game==t.Game &&
                    GameMoney.ReadInt32(handle,game+0x160)==0 && LootFlag(handle,module,c.Obj,"disableStealing"))) {skipped++;continue;}
                LootCall(c.Obj,c.Reference,"TakeAllItems");
                int after=GameMoney.WithInventory(false,(handle,_,_,module)=>LootInventory(handle,module,c.Obj).Quantity);
                if(after<c.Quantity)
                {
                    int moved=c.Quantity-after;
                    transferred+=moved;
                    if(c.Herb) harvested+=moved;
                    ErrorLog.Write("自动拾取容器",null,new {Object=$"0x{c.Obj:X}",Before=c.Quantity,After=after,Transferred=moved,Herb=c.Herb});
                    LootCall(c.Obj,c.Reference,"OnContainerClosed");
                }
            }
            return new AutoLootResult(true,containers,transferred,skipped,count,harvested);
        }
        finally
        {
            lootCleanup=(process.Id,started,t.Game,t.Player,type,array);
            CompleteLootCleanup();
        }
    });
}
