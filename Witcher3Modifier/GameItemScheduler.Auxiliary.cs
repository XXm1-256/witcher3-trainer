namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    internal static readonly string[] AuxiliaryFeatures=["unlimitedHealth","unlimitedBreath","infiniteDurability","noToxicity","horseFear","oneHitKill","freeCrafting","unlimitedAdrenaline"];
    private static readonly (string Feature,string Function,string Owner,int Parameters,byte Mode)[] AuxiliaryPolicies=
    [
        ("unlimitedHealth","IsInvulnerable","CActor",0,2),
        ("unlimitedHealth","DrainVitality","CActor",1,0),
        ("unlimitedBreath","DrainAir","CActor",2,0),
        ("infiniteDurability","ReduceItemDurability","CInventoryComponent",2,1),
        ("noToxicity","GainStat","W3PlayerAbilityManager",2,4),
        ("noToxicity","GetStat","W3PlayerAbilityManager",2,3),
        ("noToxicity","AddToxicityOffset","W3PlayerAbilityManager",1,0),
        ("noToxicity","SetToxicityOffset","W3PlayerAbilityManager",1,0),
        ("horseFear","GetPanicPercent","W3HorseComponent",0,5),
        ("unlimitedAdrenaline","DrainFocus","W3AbilityManager",1,0)
    ];

    internal static bool ReadAuxiliary(string feature) => GameMoney.WithInventory(false,(handle,game,inventory,module)=>
    {
        if(feature=="freeCrafting") return ReadFreeCrafting();
        if(!AuxiliaryFeatures.Contains(feature)) throw new ArgumentException("未知辅助功能。",nameof(feature));
        long page=FindPage(handle,module);
        if(page==0 || GameMoney.ReadInt64(handle,module+EntryRva+6)!=page+EquipmentLevelOffset ||
            !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,EquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module))) return false;
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        long context=AuxiliaryContext(handle,module,feature,player,inventory);
        if(feature=="oneHitKill") return GameMoney.ReadBytes(handle,page+0x980,1)[0]==1 && GameMoney.ReadInt64(handle,page+0x990)==player;
        for(int index=0;index<AuxiliaryPolicies.Length;index++)
        {
            if(AuxiliaryPolicies[index].Feature!=feature) continue;
            long row=AuxiliaryRow(page,index);
            byte flag=GameMoney.ReadBytes(handle,row+0x10,1)[0];
            if(flag>1) throw new InvalidOperationException("辅助功能开关数据异常。");
            if(flag==0 || GameMoney.ReadInt64(handle,row+8)!=context) return false;
        }
        return true;
    });

    internal static bool SetAuxiliary(string feature,bool enabled) => WithItemRequest((handle,game,inventory,module)=>
    {
        if(feature=="freeCrafting") return SetFreeCrafting(enabled);
        if(!AuxiliaryFeatures.Contains(feature)) throw new ArgumentException("未知辅助功能。",nameof(feature));
        long page=FindPage(handle,module);
        if(!enabled)
        {
            if(page!=0)
            {
                if(feature=="oneHitKill") Write(handle,page+0x980,[0]);
                for(int index=0;index<AuxiliaryPolicies.Length;index++)
                    if(AuxiliaryPolicies[index].Feature==feature) Write(handle,AuxiliaryRow(page,index)+0x10,[0]);
            }
            return false;
        }
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        if(GameMoney.ReadInt64(handle,player)!=module + GameVersion.Rva(0x383A4D0)) throw new InvalidOperationException("玩家对象尚不可用。");
        long context=AuxiliaryContext(handle,module,feature,player,inventory);
        long damageFunction=feature=="oneHitKill"?FindDamageFunction(handle,module,player):0;
        var targets=new List<(int Index,long Function)>();
        for(int index=0;index<AuxiliaryPolicies.Length;index++)
        {
            var policy=AuxiliaryPolicies[index];
            if(policy.Feature!=feature) continue;
            long metadataContext=policy.Feature=="horseFear"?FindHorseClass(handle,module,player):context;
            targets.Add((index,FindAuxiliaryFunction(handle,module,metadataContext,policy.Function,policy.Owner,policy.Parameters,policy.Feature=="horseFear")));
        }
        if(page==0) page=Install(handle,module,game,FindTick(handle,game,module));
        InstallEquipmentLevelPolicy(handle,page,module);
        if(feature=="oneHitKill")
        {
            Write(handle,page+0x980,[0]);
            Write(handle,page+0x988,BitConverter.GetBytes(damageFunction));
            Write(handle,page+0x990,BitConverter.GetBytes(player));
            Write(handle,page+0x980,[1]);
        }
        if(feature=="noToxicity")
        {
            long cls=GameMoney.ReadInt64(handle,context+0x18);
            int propertyName=ResolveName(handle,module,"toxicityOffset");
            int propertyCount=GameMoney.ReadInt32(handle,cls+0x38);
            if(propertyCount is <1 or >4096) throw new InvalidOperationException("煎药毒性属性表异常。");
            long properties=GameMoney.ReadInt64(handle,cls+0x30),offset=-1;
            for(int index=0;index<propertyCount;index++)
            {
                long property=GameMoney.ReadInt64(handle,properties+index*8);
                if(GameMoney.ReadInt32(handle,property+0x10)==propertyName) offset=GameMoney.ReadInt32(handle,property+0x20);
            }
            if(offset!=0x20) throw new InvalidOperationException("煎药毒性属性位置未确认。");
            ExecuteEquipmentNative(0x1D781C0,[4,..BitConverter.GetBytes(3),6,..BitConverter.GetBytes(0f),0x20],contextOverride:context);
            byte[] actual=ExecuteEquipmentNative(0x1D78540,[4,..BitConverter.GetBytes(3),9,1,0x20],contextOverride:context);
            if(BitConverter.ToSingle(actual)!=0f) throw new InvalidOperationException("药水毒性清除回读未确认。");
            long script=GameMoney.ReadInt64(handle,context+0x20);
            Write(handle,script+0x20,BitConverter.GetBytes(0f));
            if(GameMoney.ReadSingle(handle,script+0x20)!=0f) throw new InvalidOperationException("煎药毒性清除回读未确认。");
        }
        foreach(var (index,function) in targets)
        {
            long row=AuxiliaryRow(page,index);
            Write(handle,row+0x10,[0]);
            Write(handle,row,BitConverter.GetBytes(function));
            Write(handle,row+8,BitConverter.GetBytes(context));
            Write(handle,row+0x11,[AuxiliaryPolicies[index].Mode]);
            if(feature=="horseFear") Write(handle,row+0x12,BitConverter.GetBytes(0x40));
            Write(handle,row+0x10,[1]);
        }
        if(feature=="unlimitedAdrenaline")
        {
            try { FillAdrenaline(handle,module,context); }
            catch { Write(handle,AuxiliaryRow(page,9)+0x10,[0]); throw; }
        }
        if(!ReadAuxiliary(feature)) throw new InvalidOperationException("辅助功能开启后回读未确认。");
        return true;
    });

    private static long AuxiliaryRow(long page,int index) => index<9?page+0xE0+index*0x20:page+0xA00;

    private static long AuxiliaryContext(nint handle,long module,string feature,long player,long inventory)
    {
        if(feature=="infiniteDurability") return inventory;
        if(feature is not ("noToxicity" or "unlimitedAdrenaline")) return player;
        long ability=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,player+0x20)+0x158)+8);
        if(GameMoney.ReadInt32(handle,GameMoney.ReadInt64(handle,ability+0x18)+0x2C)!=ResolveName(handle,module,"W3PlayerAbilityManager"))
            throw new InvalidOperationException("玩家能力对象尚不可用。");
        return ability;
    }

    private static long FindAuxiliaryFunction(nint handle,long module,long context,string name,string owner,int parameters,bool classContext=false)
    {
        int functionName=ResolveName(handle,module,name),ownerName=ResolveName(handle,module,owner);
        long cls=classContext?context:GameMoney.ReadInt64(handle,context+0x18);
        for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
        {
            if(GameMoney.ReadInt32(handle,cls+0x2C)!=ownerName) continue;
            int count=GameMoney.ReadInt32(handle,cls+0x50);
            if(count is <1 or >4096) throw new InvalidOperationException("辅助功能函数表结构异常。");
            long array=GameMoney.ReadInt64(handle,cls+0x48),found=0;
            for(int index=0;index<count;index++)
            {
                long candidate=GameMoney.ReadInt64(handle,array+index*8);
                if(GameMoney.ReadInt32(handle,candidate+0x10)!=functionName) continue;
                if(found!=0) throw new InvalidOperationException("辅助功能函数重复。");
                found=candidate;
            }
            if(found!=0 && GameMoney.ReadInt64(handle,found)==module + GameVersion.Rva(0x3A82D58) &&
                GameMoney.ReadInt64(handle,found+8)==cls && GameMoney.ReadInt32(handle,found+0x30)==parameters &&
                GameMoney.ReadInt32(handle,found+0x90) is >0 and <65536) return found;
            break;
        }
        throw new InvalidOperationException($"尚未确认游戏函数：{name}。");
    }

    private static long FindHorseClass(nint handle,long module,long player)
    {
        long getter=FindAuxiliaryFunction(handle,module,player,"GetUsedHorseComponent","CActor",0);
        long cls=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,getter+0x20)+8)+8);
        if(GameMoney.ReadInt32(handle,cls+0x2C)!=ResolveName(handle,module,"W3HorseComponent"))
            throw new InvalidOperationException("马匹组件类型尚未确认。");
        int count=GameMoney.ReadInt32(handle,cls+0x38);
        if(count is <1 or >4096) throw new InvalidOperationException("马匹组件属性表异常。");
        long array=GameMoney.ReadInt64(handle,cls+0x30);
        int name=ResolveName(handle,module,"lastRider");
        for(int index=0;index<count;index++)
        {
            long prop=GameMoney.ReadInt64(handle,array+index*8);
            if(GameMoney.ReadInt32(handle,prop+0x10)==name && GameMoney.ReadInt32(handle,prop+0x20)==0x40) return cls;
        }
        throw new InvalidOperationException("马匹骑乘者属性位置尚未确认。");
    }

    private static long FindDamageFunction(nint handle,long module,long player)
    {
        long fn=FindAuxiliaryFunction(handle,module,player,"ReduceDamage","CActor",1);
        long parameter=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,fn+0x28));
        if(GameMoney.ReadInt32(handle,parameter+0x20)!=0 || (GameMoney.ReadInt32(handle,parameter+0x24)&0x200)==0)
            throw new InvalidOperationException("伤害参数布局尚未确认。");
        long cls=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,parameter+8)+8);
        if(GameMoney.ReadInt32(handle,cls+0x2C)!=ResolveName(handle,module,"W3DamageAction"))
            throw new InvalidOperationException("伤害对象类型尚未确认。");
        long processed=0;
        bool attacker=false;
        int attackerName=ResolveName(handle,module,"attacker"),processedName=ResolveName(handle,module,"processedDmg");
        for(int depth=0;cls!=0 && depth<16;depth++,cls=GameMoney.ReadInt64(handle,cls+0x10))
        {
            int count=GameMoney.ReadInt32(handle,cls+0x38);
            if(count is <0 or >4096) throw new InvalidOperationException("伤害属性表异常。");
            long array=GameMoney.ReadInt64(handle,cls+0x30);
            for(int index=0;index<count;index++)
            {
                long prop=GameMoney.ReadInt64(handle,array+index*8);
                int name=GameMoney.ReadInt32(handle,prop+0x10),offset=GameMoney.ReadInt32(handle,prop+0x20);
                if((GameMoney.ReadInt32(handle,prop+0x24)&0x20)!=0) continue;
                if(name==attackerName && offset==0x88) attacker=true;
                if(name==processedName && offset==0xA0) processed=GameMoney.ReadInt64(handle,prop+8);
            }
        }
        if(!attacker || processed==0) throw new InvalidOperationException("伤害来源及处理属性尚未确认。");
        int fields=GameMoney.ReadInt32(handle,processed+0x38),verified=0;
        if(fields is <1 or >128) throw new InvalidOperationException("伤害数值字段异常。");
        long props=GameMoney.ReadInt64(handle,processed+0x30);
        int vitality=ResolveName(handle,module,"vitalityDamage"),essence=ResolveName(handle,module,"essenceDamage");
        for(int index=0;index<fields;index++)
        {
            long prop=GameMoney.ReadInt64(handle,props+index*8);
            int name=GameMoney.ReadInt32(handle,prop+0x10),offset=GameMoney.ReadInt32(handle,prop+0x20);
            if(name==vitality && offset==0 || name==essence && offset==4) verified++;
        }
        if(verified!=2) throw new InvalidOperationException("生命及精华伤害字段尚未确认。");
        return fn;
    }

    internal static void RebindAuxiliary()
    {
        var stale=GameMoney.WithInventory(false,(handle,game,inventory,module)=>
        {
            var result=new HashSet<string>();
            long page=FindPage(handle,module);
            if(page==0 || GameMoney.ReadInt64(handle,module+EntryRva+6)!=page+EquipmentLevelOffset ||
                !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,EquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module))) return result;
            long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
            if(GameMoney.ReadBytes(handle,page+0x980,1)[0]==1 && GameMoney.ReadInt64(handle,page+0x990)!=player) result.Add("oneHitKill");
            for(int index=0;index<AuxiliaryPolicies.Length;index++)
            {
                var policy=AuxiliaryPolicies[index];
                long row=AuxiliaryRow(page,index);
                if(GameMoney.ReadBytes(handle,row+0x10,1)[0]==1 &&
                    GameMoney.ReadInt64(handle,row+8)!=AuxiliaryContext(handle,module,policy.Feature,player,inventory)) result.Add(policy.Feature);
            }
            return result;
        });
        foreach(string feature in stale) SetAuxiliary(feature,true);
    }

}
