namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private static long ReturnTableRva => GameVersion.Rva(0x5A92BC8);
    private const int CraftingReturnOffset=0x1500;
    private static readonly byte[] CraftingReturnTemplate=Convert.FromHexString(
        "5356574883EC304889D64C89C748BB8877665544332211488B42308A008844242048B89988776655443322FFD05080BB00090000017541807C24280A753A488B4618483B83080900007410483B83100900007524BA3C000000EB05BA1C000000488B46284885C0740FC7041000000000F048FF8320090000584883C4305F5E5BC3");

    private static byte[] BuildCraftingReturn(long page,long module)
    {
        byte[] code=(byte[])CraftingReturnTemplate.Clone();
        Replace(code,0x1122334455667788,page,1);
        Replace(code,0x2233445566778899,module + GameVersion.Rva(0x2776C50),1);
        return code;
    }

    internal static bool ReadFreeCrafting() => GameMoney.WithInventory(false,(handle,_,_,module)=>
    {
        long target=GameMoney.ReadInt64(handle,module+ReturnTableRva);
        if(target==module + GameVersion.Rva(0x2776C50)) return false;
        long page=FindPage(handle,module);
        if(page==0 || target!=page+CraftingReturnOffset ||
            !GameMoney.ReadBytes(handle,target,CraftingReturnTemplate.Length).SequenceEqual(BuildCraftingReturn(page,module)))
            throw new InvalidOperationException("制作材料返回入口校验失败。");
        byte flag=GameMoney.ReadBytes(handle,page+0x900,1)[0];
        if(flag>1) throw new InvalidOperationException("制作材料开关数据异常。");
        return flag==1;
    });

    internal static bool SetFreeCrafting(bool enabled) => WithItemRequest((handle,game,_,module)=>
    {
        ReadFreeCrafting();
        long page=FindPage(handle,module);
        if(!enabled)
        {
            if(page!=0) Write(handle,page+0x900,[0]);
            return false;
        }
        long crafting=FindCraftingClass(handle,module);
        long alchemy=FindAlchemyClass(handle,module,game);
        long craftFn=ValidateRecipeGetter(handle,module,crafting,"W3CraftingManager","GetSchematic","SCraftingSchematic","ingredients",16);
        long alchemyFn=ValidateRecipeGetter(handle,module,alchemy,"W3AlchemyManager","GetRecipe","SAlchemyRecipe","requiredIngredients",48);
        if(page==0) page=Install(handle,module,game,FindTick(handle,game,module));
        byte[] code=BuildCraftingReturn(page,module);
        long address=page+CraftingReturnOffset;
        byte[] previousCode=GameMoney.ReadBytes(handle,address,code.Length);
        if(previousCode.Any(value=>value!=0) && !previousCode.SequenceEqual(code))
            throw new InvalidOperationException("制作材料返回空间已被占用。");
        Write(handle,page+0x900,[0]);
        Write(handle,page+0x908,BitConverter.GetBytes(craftFn));
        Write(handle,page+0x910,BitConverter.GetBytes(alchemyFn));
        long previousTarget=GameMoney.ReadInt64(handle,module+ReturnTableRva);
        if(previousTarget!=address)
        {
            GameProcessPause.Run(handle,()=>
            {
                if(GameMoney.ReadInt64(handle,module+ReturnTableRva)!=previousTarget) throw new InvalidOperationException("制作材料返回入口在安装前变化。");
                try
                {
                    Patch(handle,address,code);
                    Write(handle,module+ReturnTableRva,BitConverter.GetBytes(address));
                    if(GameMoney.ReadInt64(handle,module+ReturnTableRva)!=address) throw new InvalidOperationException("制作材料返回入口回读失败。");
                }
                catch {Write(handle,module+ReturnTableRva,BitConverter.GetBytes(previousTarget));Patch(handle,address,previousCode);throw;}
            },(address,code.Length));
        }
        Write(handle,page+0x900,[1]);
        if(!ReadFreeCrafting()) throw new InvalidOperationException("制作材料开关回读未确认。");
        return true;
    });

    private static long FindCraftingClass(nint handle,long module)
    {
        long context=GameMoney.ReadInt64(handle,module + GameVersion.Rva(0x5CC5148));
        int buckets=GameMoney.ReadInt32(handle,context+0x40),count=GameMoney.ReadInt32(handle,context+0x44);
        if(buckets is <1 or >65535 || count is <1 or >65535) throw new InvalidOperationException("全局函数索引异常。");
        long table=GameMoney.ReadInt64(handle,context+0x60);
        int name=ResolveName(handle,module,"craft");
        for(int bucket=0;bucket<buckets;bucket++)
        {
            long node=GameMoney.ReadInt64(handle,table+bucket*8);
            for(int index=0;node!=0 && index<count;index++,node=GameMoney.ReadInt64(handle,node+0x18))
                if(GameMoney.ReadInt32(handle,node)==name)
                    return ClassFromLocal(handle,module,GameMoney.ReadInt64(handle,node+8),"cftman","W3CraftingManager");
        }
        throw new InvalidOperationException("制作管理器类型尚不可用。");
    }

    private static long FindAlchemyClass(nint handle,long module,long game)
    {
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        long ability=AuxiliaryContext(handle,module,"noToxicity",player,0);
        long cls=GameMoney.ReadInt64(handle,ability+0x18);
        int count=GameMoney.ReadInt32(handle,cls+0x50);
        if(count is <1 or >4096) throw new InvalidOperationException("玩家能力函数表异常。");
        long array=GameMoney.ReadInt64(handle,cls+0x48);
        for(int index=0;index<count;index++)
        {
            long found=ClassFromLocal(handle,module,GameMoney.ReadInt64(handle,array+index*8),"m_alchemyManager","W3AlchemyManager",true);
            if(found!=0) return found;
        }
        throw new InvalidOperationException("炼金管理器类型尚不可用。");
    }

    private static long ClassFromLocal(nint handle,long module,long fn,string local,string className,bool optional=false)
    {
        if(GameMoney.ReadInt64(handle,fn)!=module + GameVersion.Rva(0x3A82D58)) throw new InvalidOperationException("函数元数据类型异常。");
        int count=GameMoney.ReadInt32(handle,fn+0x54);
        if(count is <0 or >128) throw new InvalidOperationException("函数局部变量表异常。");
        long array=GameMoney.ReadInt64(handle,fn+0x4C);
        int name=ResolveName(handle,module,local),expected=ResolveName(handle,module,className);
        for(int index=0;index<count;index++)
        {
            long prop=GameMoney.ReadInt64(handle,array+index*8);
            if(GameMoney.ReadInt32(handle,prop+0x10)!=name) continue;
            long cls=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,prop+8)+8);
            if(GameMoney.ReadInt32(handle,cls+0x2C)==expected) return cls;
            throw new InvalidOperationException("管理器局部变量类型不匹配。");
        }
        if(optional) return 0;
        throw new InvalidOperationException("管理器局部变量未找到。");
    }

    private static long ValidateRecipeGetter(nint handle,long module,long cls,string owner,string method,string structName,string ingredients,int expectedOffset)
    {
        long fn=FindAuxiliaryFunction(handle,module,cls,method,owner,2,true);
        long array=GameMoney.ReadInt64(handle,fn+0x28),output=GameMoney.ReadInt64(handle,array+8);
        if(GameMoney.ReadInt32(handle,output+0x20)!=4 || GameMoney.ReadInt32(handle,output+0x24)!=0x280)
            throw new InvalidOperationException("配方输出参数布局不匹配。");
        long structure=GameMoney.ReadInt64(handle,output+8);
        if(GameMoney.ReadInt32(handle,structure+0x2C)!=ResolveName(handle,module,structName)) throw new InvalidOperationException("配方输出结构不匹配。");
        int count=GameMoney.ReadInt32(handle,structure+0x38);
        if(count is <1 or >128) throw new InvalidOperationException("配方结构字段异常。");
        long props=GameMoney.ReadInt64(handle,structure+0x30);
        bool found=false;
        int name=ResolveName(handle,module,ingredients);
        for(int index=0;index<count;index++)
        {
            long prop=GameMoney.ReadInt64(handle,props+index*8);
            if(GameMoney.ReadInt32(handle,prop+0x10)==name && GameMoney.ReadInt32(handle,prop+0x20)==expectedOffset && GameMoney.ReadInt32(handle,prop+0x24)==0) found=true;
        }
        byte[] bytecode=GameMoney.ReadBytes(handle,GameMoney.ReadInt64(handle,fn+0x88),GameMoney.ReadInt32(handle,fn+0x90));
        if(!found || !bytecode.AsSpan().EndsWith(new byte[]{0x21,0x0B,0}) || bytecode.AsSpan().IndexOf(new byte[]{0x21,0x0A,0x1C,0x15})<0)
            throw new InvalidOperationException("配方材料字段及返回契约未确认。");
        return fn;
    }
}
