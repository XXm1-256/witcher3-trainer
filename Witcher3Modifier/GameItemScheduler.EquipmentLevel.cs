namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private const int EquipmentLevelOffset=0x1600;
    private static readonly byte[] LegacyEquipmentLevelTemplate=Convert.FromHexString(
        "9C50415249BA88776655443322114180BAD0000000017535493BB2C0000000752C4D3BAAC800000075234D85FF741E41C60701F049FF82D8000000415A589DFF2500000000DDCCBBAA99887766415A589DFF2500000000CCBBAA9988776655");

    private static readonly byte[] CachedEquipmentLevelTemplate=Convert.FromHexString("9C50415241535249BA88776655443322114180BA80090000017558493BB288090000754F4D3BAA900900007446498B174885D2743E488B52084885D27435488B82880000004885C074294D8B9A900900004C395808751CC782A000000020BCBE4CC782A400000020BCBE4CF049FF82980900004180BAD000000001752E493BB2C000000075254D3BAAC8000000751C488B85C80100004885C07410C60001F049FF82D8000000E9E40000004180BA100A000001751F493BB2000A000075164D3BAA080A0000750DF049FF82180A0000E9BB0000004D8D9AE000000041807B10010F8580000000493B33757B41807B110574154D3B6B08756E41807B1103722841833F037561EB20498B55204885D27456418B4312488B14024885D27449498B430848394208753F41807B1100743141807B1104742A488B85C80100004885C0741E41807B1103741141807B1105740A41807B11020F9400EB06C70000000000F049FF4318EB294983C320498D82000200004939C30F8261FFFFFF5A415B415A589DFF2500000000CCBBAA99887766555A415B415A589DFF2500000000DDCCBBAA99887766");

    // The live player guard also covers calls made while a newly loaded character is initializing.
    private static readonly byte[] EquipmentLevelTemplate=Convert.FromHexString("9C50415241535249BA88776655443322114180BA80090000017558493BB288090000754F4D3BAA900900007446498B174885D2743E488B52084885D27435488B82880000004885C074294D8B9A900900004C395808751CC782A000000020BCBE4CC782A400000020BCBE4CF049FF82980900004180BAD000000001752E493BB2C00000007525E8190100009090751C488B85C80100004885C07410C60001F049FF82D8000000E9E40000004180BA100A000001751F493BB2000A000075164D3BAA080A0000750DF049FF82180A0000E9BB0000004D8D9AE000000041807B10010F8580000000493B33757B41807B110574154D3B6B08756E41807B1103722841833F037561EB20498B55204885D27456418B4312488B14024885D27449498B430848394208753F41807B1100743141807B1104742A488B85C80100004885C0741E41807B1103741141807B1105740A41807B11020F9400EB06C70000000000F049FF4318EB294983C320498D82000200004939C30F8261FFFFFF5A415B415A589DFF2500000000CCBBAA99887766555A415B415A589DFF2500000000DDCCBBAA9988776650498B42084885C0741A488B8060FD00004885C0740E488B40084885C074054939C558C34883C8014885C058C3");

    private static readonly byte[] LegacyAuxiliaryTemplate=Convert.FromHexString("9C50415241535249BA88776655443322114180BAD000000001752E493BB2C000000075254D3BAAC8000000751C488B85C80100004885C07410C60001F049FF82D8000000E9BB0000004D8D9AE000000041807B10010F8580000000493B33757B41807B110574154D3B6B08756E41807B1103722841833F037561EB20498B55204885D27456418B4312488B14024885D27449498B430848394208753F41807B1100743141807B1104742A488B85C80100004885C0741E41807B1103741141807B1105740A41807B11020F9400EB06C70000000000F049FF4318EB294983C320498D82000200004939C30F8261FFFFFF5A415B415A589DFF2500000000CCBBAA99887766555A415B415A589DFF2500000000DDCCBBAA99887766");

    private static readonly byte[] PreviousEquipmentLevelTemplate=Convert.FromHexString("9C50415241535249BA88776655443322114180BA80090000017558493BB288090000754F4D3BAA900900007446498B174885D2743E488B52084885D27435488B82880000004885C074294D8B9A900900004C395808751CC782A000000020BCBE4CC782A400000020BCBE4CF049FF82980900004180BAD000000001752E493BB2C000000075254D3BAAC8000000751C488B85C80100004885C07410C60001F049FF82D8000000E9BB0000004D8D9AE000000041807B10010F8580000000493B33757B41807B110574154D3B6B08756E41807B1103722841833F037561EB20498B55204885D27456418B4312488B14024885D27449498B430848394208753F41807B1100743141807B1104742A488B85C80100004885C0741E41807B1103741141807B1105740A41807B11020F9400EB06C70000000000F049FF4318EB294983C320498D82000200004939C30F8261FFFFFF5A415B415A589DFF2500000000CCBBAA99887766555A415B415A589DFF2500000000DDCCBBAA99887766");

    private static byte[] BuildEquipmentLevelCode(long page,long module,bool legacy=false,bool legacyAuxiliary=false,bool previous=false,bool cached=false)
    {
        byte[] code=(byte[])(cached?CachedEquipmentLevelTemplate:previous?PreviousEquipmentLevelTemplate:legacy?LegacyEquipmentLevelTemplate:legacyAuxiliary?LegacyAuxiliaryTemplate:EquipmentLevelTemplate).Clone();
        Replace(code,0x1122334455667788,page,1);
        Replace(code,0x66778899AABBCCDD,module + GameVersion.Rva(0x26F94A5),1);
        Replace(code,0x5566778899AABBCC,page+PolicyOffset,1);
        return code;
    }


    internal static bool ReadEquipmentLevelBypass() => GameMoney.WithInventory(false,(handle,_,_,module)=>
    {
        long page=FindPage(handle,module);
        if(page==0 || GameMoney.ReadInt64(handle,module+EntryRva+6)!=page+EquipmentLevelOffset) return false;
        byte flag=GameMoney.ReadBytes(handle,page+0xD0,1)[0];
        if(flag>1) throw new InvalidOperationException("装备穿戴等级开关数据异常。");
        return flag==1;
    });

    internal static bool SetEquipmentLevelBypass(bool enabled) => WithItemRequest((handle,game,_,module)=>
    {
        long page=FindPage(handle,module);
        if(!enabled)
        {
            if(page!=0) Write(handle,page+0xD0,[0]);
            return false;
        }
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        if(GameMoney.ReadInt64(handle,player)!=module + GameVersion.Rva(0x383A4D0))
            throw new InvalidOperationException("装备等级检查的玩家对象不匹配。");
        if(page!=0 && GameMoney.ReadInt64(handle,module+EntryRva+6)==page+EquipmentLevelOffset &&
            GameMoney.ReadInt64(handle,page+0xC8)==player && GameMoney.ReadInt64(handle,page+0xC0)>0 &&
            GameMoney.ReadBytes(handle,page+0xD0,1)[0]==1 &&
            GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,EquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module))) return true;
        long function=FindEquipmentLevelFunction(handle,player,module);
        if(page==0) page=Install(handle,module,game,FindTick(handle,game,module));
        Write(handle,page+0xD0,[0]);
        Write(handle,page+0xC0,BitConverter.GetBytes(function));
        Write(handle,page+0xC8,BitConverter.GetBytes(player));
        InstallEquipmentLevelPolicy(handle,page,module);
        Write(handle,page+0xD0,[1]);
        if(!ReadEquipmentLevelBypass()) throw new InvalidOperationException("装备穿戴等级开关回读失败。");
        return true;
    });

    internal static void RebindEquipmentLevelBypass()
    {
        if(ReadEquipmentLevelBypass())
        {
            SetEquipmentLevelBypass(true);
            ObserveEquipmentSlots();
        }
    }

    private static (long Player,int[] Slots)? lastEquipmentSlots;
    internal static (long Player,int[] Slots) ReadEquipmentSlots() => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        long player=FunTarget(handle,game,module).Player;
        var property=RespecProperty(handle,module,player,"itemSlots");
        long element=GameMoney.ReadInt64(handle,property.Type+8);
        int count=GameMoney.ReadInt32(handle,property.Address+8);
        long data=GameMoney.ReadInt64(handle,property.Address);
        if(GameMoney.ReadInt32(handle,element+0x60)!=4 || count is <1 or >128 || data==0)
            throw new InvalidOperationException("装备槽诊断布局未确认。");
        byte[] bytes=GameMoney.ReadBytes(handle,data,count*4);
        if(GameMoney.ReadInt64(handle,property.Address)!=data || GameMoney.ReadInt32(handle,property.Address+8)!=count)
            throw new InvalidOperationException("装备槽正在变化，请稍后读取。");
        return (player,Enumerable.Range(0,count).Select(index=>BitConverter.ToInt32(bytes,index*4)).ToArray());
    });

    private static void ObserveEquipmentSlots()
    {
        var current=ReadEquipmentSlots();
        if(lastEquipmentSlots is {} previous && previous.Player==current.Player && previous.Slots.SequenceEqual(current.Slots)) return;
        var ids=current.Slots.Concat(lastEquipmentSlots?.Slots??[]).ToHashSet();
        var items=GameInventory.Read().Where(item=>ids.Contains(item.UniqueId)).Select(item=>new {item.UniqueId,item.Name,item.DisplayName}).ToArray();
        ErrorLog.Write("装备槽变化记录",null,new {Player=current.Player,Before=lastEquipmentSlots?.Slots,After=current.Slots,Items=items,LevelBypass=true});
        lastEquipmentSlots=current;
    }

    private static long FindEquipmentLevelFunction(nint handle,long player,long module)
    {
        long playerClass=GameMoney.ReadInt64(handle,player+0x18);
        int functionName=ResolveName(handle,module,"HasRequiredLevelToEquipItem");
        int ownerName=ResolveName(handle,module,"CR4Player");
        long found=0;
        for(int depth=0;playerClass!=0 && depth<16;depth++,playerClass=GameMoney.ReadInt64(handle,playerClass+0x10))
        {
            if(GameMoney.ReadInt32(handle,playerClass+0x2C)!=ownerName) continue;
            int count=GameMoney.ReadInt32(handle,playerClass+0x50);
            if(count is <1 or >4096) throw new InvalidOperationException("玩家函数表结构异常。");
            long array=GameMoney.ReadInt64(handle,playerClass+0x48);
            for(int index=0;index<count;index++)
            {
                long candidate=GameMoney.ReadInt64(handle,array+index*8);
                if(GameMoney.ReadInt32(handle,candidate+0x10)!=functionName) continue;
                if(found!=0) throw new InvalidOperationException("装备等级检查函数重复。");
                found=candidate;
            }
            break;
        }
        if(found==0 || GameMoney.ReadInt64(handle,found)!=module + GameVersion.Rva(0x3A82D58) ||
            GameMoney.ReadInt32(handle,GameMoney.ReadInt64(handle,found+8)+0x2C)!=ownerName ||
            GameMoney.ReadInt32(handle,found+0x30)!=1 || GameMoney.ReadInt32(handle,found+0x90) is <1 or >65536)
            throw new InvalidOperationException("装备等级检查函数尚未通过结构核对。");
        long parameter=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,found+0x28));
        if(GameMoney.ReadInt32(handle,parameter+0x10)!=ResolveName(handle,module,"item") ||
            GameMoney.ReadInt32(handle,parameter+0x20)!=0)
            throw new InvalidOperationException("装备等级检查参数布局不匹配。");
        return found;
    }

    private static void InstallEquipmentLevelPolicy(nint handle,long page,long module)
    {
        long entry=module+EntryRva;
        byte[] previous=GameMoney.ReadBytes(handle,entry,EntryOriginal.Length);
        byte[] code=BuildEquipmentLevelCode(page,module);
        byte[] existing=GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,code.Length);
        if(existing.SequenceEqual(code) && GameMoney.ReadInt64(handle,entry+6)==page+EquipmentLevelOffset) return;
        byte[] legacy=BuildEquipmentLevelCode(page,module,true);
        bool knownLegacy=existing.AsSpan(0,legacy.Length).SequenceEqual(legacy) && existing.AsSpan(legacy.Length).IndexOfAnyExcept((byte)0)<0;
        byte[] legacyAuxiliary=BuildEquipmentLevelCode(page,module,legacyAuxiliary:true);
        knownLegacy|=existing.AsSpan(0,legacyAuxiliary.Length).SequenceEqual(legacyAuxiliary) && existing.AsSpan(legacyAuxiliary.Length).IndexOfAnyExcept((byte)0)<0;
        byte[] cachedCode=BuildEquipmentLevelCode(page,module,cached:true);
        knownLegacy|=existing.AsSpan(0,cachedCode.Length).SequenceEqual(cachedCode) && existing.AsSpan(cachedCode.Length).IndexOfAnyExcept((byte)0)<0;
        byte[] previousCode=BuildEquipmentLevelCode(page,module,previous:true);
        knownLegacy|=existing.AsSpan(0,previousCode.Length).SequenceEqual(previousCode) && existing.AsSpan(previousCode.Length).IndexOfAnyExcept((byte)0)<0;
        if(existing.Any(value=>value!=0) && !existing.SequenceEqual(code) && !knownLegacy)
            throw new InvalidOperationException("装备等级入口空间已经被使用。");
        GameProcessPause.Run(handle,()=>
        {
            if(!GameMoney.ReadBytes(handle,entry,previous.Length).SequenceEqual(previous))
                throw new InvalidOperationException("装备等级入口在安装前已变化。");
            Patch(handle,page+EquipmentLevelOffset,code);
            byte[] jump=(byte[])previous.Clone();
            BitConverter.GetBytes(page+EquipmentLevelOffset).CopyTo(jump,6);
            try
            {
                Patch(handle,entry,jump);
                if(FindPage(handle,module)!=page) throw new InvalidOperationException("装备等级入口安装校验失败。");
            }
            catch { Patch(handle,page+EquipmentLevelOffset,existing); Patch(handle,entry,previous); throw; }
        },(entry,previous.Length),(page+EquipmentLevelOffset,code.Length));
    }
}
