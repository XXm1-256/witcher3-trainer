namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    internal static readonly (string Label,string Item)[] FunHairStyles =
    [
        ("束发马尾","Half With Tail Hairstyle"), ("剃鬓马尾","Shaved With Tail Hairstyle"),
        ("长发披肩","Long Loose Hairstyle"), ("短发披肩（DLC）","Short Loose Hairstyle"),
        ("莫霍克马尾（DLC）","Mohawk With Ponytail Hairstyle"), ("尼弗迦德式（DLC）","Nilfgaardian Hairstyle")
    ];
    private static readonly Dictionary<long,string> FunPrefixes = new()
    {
        [0x1F85150]="48895C2408565741564883EC308B05C564C7034C8D351669",
        [0x2393610]="48895C24084889742418574883EC308B05E3B58D03488D3D",
        [0x22715C0]="48FF42304D85C0740D488B81B80100008B4808418908C3CC",
        [0x2271510]="48895C24184889742420574883EC20488B4230488D3556A5",
        [0x2271860]="48895C24084889742418574883EC40488B4230488D3506A2",
        [0x22717F0]="48895C2408574883EC208B0508D49F034C8D0D79A27F0389",
        [0x22719B0]="48895C24084889742418574883EC20488BDA498BF832D248",
        [0x1CF7070]="40534883EC20488B42304C8D0DFF49D703C5FA10051F63DB",
        [0x1CF6F80]="40534883EC20488B42304C8D0DEF4AD703C5FA10050F64DB",
        [0x1CF6E30]="48895C2408574883EC30488B4230488D3D3B4CD703C5F810",
        [0x1CF6B90]="40534883EC20488B42304C8D0DDF4ED703C5FA1005FF67DB",
        [0x1CF6AA0]="40534883EC20488B42304C8D0DCF4FD703C5FA1005876EDB",
        [0x1CF67F0]="48895C2408574883EC30488B4230488D3D7B52D703C5F829",
        [0x20FB390]="48895C24084889742418574883EC30488B4230488D35D606",
        [0x20FB490]="4883EC2848FF42304C8BC9488B058E649503C78158010000"
    };

    private static bool FunClass(nint handle,long module,long obj,string name)
    {
        int id=ResolveName(handle,module,name);
        long cls=GameMoney.ReadInt64(handle,obj+0x18);
        for(int i=0;cls!=0 && i<20;i++,cls=GameMoney.ReadInt64(handle,cls+0x10))
            if(GameMoney.ReadInt32(handle,cls+0x2C)==id) return true;
        return false;
    }

    private static (long Game,long Player,long Head) FunTarget(nint handle,long game,long module,bool head=false)
    {
        long player=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8);
        if(!FunClass(handle,module,player,"W3PlayerWitcher"))
            throw new InvalidOperationException("请在杰洛特的实际游玩场景使用此功能。");
        long component=0;
        if(head)
        {
            int count=GameMoney.ReadInt32(handle,player+0xF8);
            if(count is <1 or >256) throw new InvalidOperationException("角色组件布局未确认。");
            long array=GameMoney.ReadInt64(handle,player+0xF0);
            for(int i=0;i<count;i++)
            {
                long obj=GameMoney.ReadInt64(handle,array+i*8);
                if(obj!=0 && FunClass(handle,module,obj,"CHeadManagerComponent")) { component=obj; break; }
            }
            if(component==0) throw new InvalidOperationException("当前角色的胡须管理组件未找到。");
        }
        return (game,player,component);
    }

    private static void FunCheck(nint handle,long module,long rva)
    {
        byte[] prefix=Convert.FromHexString(FunPrefixes[rva]);
        if(!GameMoney.ReadBytes(handle,module+GameVersion.Rva(rva),prefix.Length).SequenceEqual(prefix))
            throw new InvalidOperationException("该趣味功能的游戏入口未通过检查，本次操作未执行。");
    }

    internal static int ReadFunTime() => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        FunTarget(handle,game,module);
        FunCheck(handle,module,0x22715C0);
        int seconds=GameMoney.ReadInt32(handle,GameMoney.ReadInt64(handle,game+0x1B8)+8);
        if(seconds<0) throw new InvalidOperationException("游戏时间数据异常。");
        return seconds;
    });

    internal static object FunDiagnostics() => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        var target=FunTarget(handle,game,module,true);
        foreach(long rva in FunPrefixes.Keys) FunCheck(handle,module,rva);
        var hair=FunHairStyles.Select(style=>new {style.Label,style.Item,NameId=ResolveItem(handle,game,module,style.Item)}).ToArray();
        // SetPositionCatViewFx initializes its omitted Vector from this exact zero constant.
        long instruction=module+0x1CF6E45;
        int displacement=GameMoney.ReadInt32(handle,instruction+4);
        if(GameMoney.ReadBytes(handle,instruction+8+displacement,16).Any(value=>value!=0))
            throw new InvalidOperationException("猫眼的默认位置常量不是零向量。");
        return new {Time=ReadFunTime(),Head=$"0x{target.Head:X}",Beard=GameMoney.ReadInt32(handle,target.Head+0x158),Hair=hair,Prefixes=FunPrefixes.Count,ZeroCatPosition=true};
    });

    internal static int FunTimeTarget(int current,int hour,int minute)
    {
        if(current<0 || hour is <0 or >23 || minute is <0 or >59) throw new ArgumentOutOfRangeException(nameof(hour));
        return checked(current/86400*86400+hour*3600+minute*60);
    }

    internal static int SetFunTime(int hour,int minute) => RunSerialized(()=>
    {
        int target=FunTimeTarget(ReadFunTime(),hour,minute);
        FunNative(0x2271510,[4,..BitConverter.GetBytes(target),9,1,0x20]);
        int actual=ReadFunTime();
        if(Math.Abs((long)actual-target)>120) throw new InvalidOperationException("时间请求已执行，但目标时段尚未回读确认，请检查游戏时钟。");
        return actual;
    });

    internal sealed record FunWeather(string Label,string Name)
    {
        public override string ToString()=>Label;
    }

    internal static FunWeather[] ReadFunWeather() => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        FunTarget(handle,game,module);
        FunCheck(handle,module,0x2393610);
        long instruction=module+GameVersion.Rva(0x2393610)+0xA9;
        if(GameMoney.ReadInt64(handle,instruction+7+GameMoney.ReadInt32(handle,instruction+3))!=game)
            throw new InvalidOperationException("天气管理器的游戏对象未确认。");
        int active=0;
        if(GameMoney.ReadBytes(handle,game+0xFD3A,1)[0]!=0)
        {
            long getter=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game)+0x158);
            if(getter!=module+0x1D6F2B0) throw new InvalidOperationException("活动世界类型入口未确认。");
            long reference=GameMoney.ReadInt64(handle,game+0x16070);
            long obj=reference==0?0:GameMoney.ReadInt64(handle,reference+8);
            if(obj==0) throw new InvalidOperationException("活动世界尚未就绪。");
            active=GameMoney.ReadInt32(handle,obj+0x7C);
        }
        long world=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xF0)+8);
        long scene=GameMoney.ReadInt64(handle,world+(active!=0?0x230:0x228));
        long manager=GameMoney.ReadInt64(handle,scene+0x92270);
        long data=GameMoney.ReadInt64(handle,manager+8);
        int count=GameMoney.ReadInt32(handle,manager+0x10);
        if(count is <1 or >256 || data==0) throw new InvalidOperationException("区域天气列表尚未就绪。");
        int[] ids=Enumerable.Range(0,count).Select(i=>GameMoney.ReadInt32(handle,data+i*0x110)).ToArray();
        var names=GameInventory.ReadNames(handle,module,ids);
        if(GameMoney.ReadInt64(handle,manager+8)!=data || GameMoney.ReadInt32(handle,manager+0x10)!=count)
            throw new InvalidOperationException("天气列表已变化，请重新读取。");
        return ids.Select(id=>names.GetValueOrDefault(id,"")).Where(name=>name.StartsWith("WT_",StringComparison.Ordinal) && !name.StartsWith("WT_q",StringComparison.OrdinalIgnoreCase))
            .Distinct().Select(name=>new FunWeather(name switch
            {
                "WT_Clear"=>"晴朗", "WT_Light_Clouds"=>"少云", "WT_Mid_Clouds"=>"多云", "WT_Mid_Clouds_Dark"=>"厚云",
                "WT_Heavy_Clouds"=>"阴天", "WT_Heavy_Clouds_Dark"=>"浓重阴云", "WT_Rain_Storm"=>"暴风雨",
                "WT_Snow"=>"降雪", "WT_Grey_Sky"=>"灰蒙天气", "WT_Sun_Shower"=>"阵雨", _=>"区域天气（"+name+"）"
            },name)).ToArray();
    });

    internal static void SetFunWeather(string weather) => RunSerialized(()=>
    {
        if(!ReadFunWeather().Any(choice=>choice.Name==weather)) throw new InvalidOperationException("当前区域不支持所选天气，请重新读取区域天气。");
        int name=GameMoney.WithInventory(false,(handle,_,_,module)=>ResolveName(handle,module,weather));
        if(FunNative(0x2393610,[4,..BitConverter.GetBytes(name),6,..BitConverter.GetBytes(1f),9,0,0x20])[0]==0)
            throw new InvalidOperationException("当前区域没有接受所选天气，可能由剧情或区域天气控制。");
        return true;
    });

    internal static float SetFunSlow(float multiplier) => RunSerialized(()=>
    {
        if(!float.IsFinite(multiplier) || multiplier is <0.1f or >1f) throw new ArgumentOutOfRangeException(nameof(multiplier));
        int source=GameMoney.WithInventory(false,(handle,_,_,module)=>ResolveName(handle,module,"CFM_On"));
        if(multiplier==1f) FunNative(0x22717F0,[4,..BitConverter.GetBytes(source),0x20]);
        else FunNative(0x2271860,[6,..BitConverter.GetBytes(multiplier),4,..BitConverter.GetBytes(source),4,..BitConverter.GetBytes(20),9,1,9,1,0x20]);
        float actual=BitConverter.ToSingle(FunNative(0x22719B0,[9,0,0x20]));
        if(!float.IsFinite(actual) || actual is <0 or >100) throw new InvalidOperationException("时间缩放回读异常，请尝试恢复正常速度。");
        return actual;
    });

    internal static void SetFunCat(bool enabled) => RunSerialized(()=>
    {
        // Cat potion brightness, range and fog; the renderer keeps its existing tint.
        // Validate every wrapper before changing the visual state.
        GameMoney.WithInventory(false,(handle,_,_,module)=>
        {
            FunCheck(handle,module,enabled?0x1CF7070:0x1CF6F80);
            if(enabled)
                foreach(long rva in new long[]{0x1CF6B90,0x1CF6AA0,0x1CF67F0,0x1CF6E30}) FunCheck(handle,module,rva);
            return true;
        });
        if(enabled)
        {
            FunNative(0x1CF6B90,[6,..BitConverter.GetBytes(350f),0x20]);
            FunNative(0x1CF6AA0,[6,..BitConverter.GetBytes(200f),0x20]);
            FunNative(0x1CF67F0,[6,..BitConverter.GetBytes(.5f),0,0x20]);
            FunNative(0x1CF6E30,[0,9,1,0x20]);
        }
        FunNative(enabled?0x1CF7070:0x1CF6F80,[6,..BitConverter.GetBytes(1f),0x20]);
        return true;
    });

    internal static void SetFunHair(int index) => RunSerialized(()=>
    {
        if(index<0 || index>=FunHairStyles.Length) throw new ArgumentOutOfRangeException(nameof(index));
        GameMoney.WithInventory(false,(handle,game,_,module)=>FunTarget(handle,game,module));
        string name=FunHairStyles[index].Item;
        var item=GameInventory.Read().FirstOrDefault(item=>item.Name==name);
        if(item is null) { Give(name,1); item=GameInventory.Read().FirstOrDefault(item=>item.Name==name); }
        if(item is null) throw new InvalidOperationException("发型物品未确认，请重新读取，避免重复添加。");
        return RunForItem(item,()=>
        {
            GameMoney.WithInventory(false,(handle,_,_,module)=> { FunCheck(handle,module,0x1F85150); return true; });
            if(ExecuteEquipmentNative(0x1F85150,[4,..BitConverter.GetBytes(item.UniqueId),9,0,9,0,0x20])[0]==0)
                throw new InvalidOperationException("游戏没有接受所选发型，请检查当前角色状态。");
            if(!IsItemMounted(item.UniqueId)) throw new InvalidOperationException("发型装配未回读确认，请检查角色外观。");
            return true;
        });
    });

    internal static int SetFunBeard(int stage) => RunSerialized(()=>
    {
        if(stage is <0 or >4) throw new ArgumentOutOfRangeException(nameof(stage));
        long head=GameMoney.WithInventory(false,(handle,game,_,module)=>FunTarget(handle,game,module,true).Head);
        FunNative(stage==0?0x20FB490:0x20FB390,stage==0?[0x20]:[9,0,4,..BitConverter.GetBytes(stage),0x20],head);
        int actual=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            if(FunTarget(handle,game,module,true).Head!=head) throw new InvalidOperationException("角色已变化，请检查胡须状态。");
            return GameMoney.ReadInt32(handle,head+0x158);
        });
        if(actual!=stage) throw new InvalidOperationException("胡须请求已执行，但所选阶段未回读确认；当前造型可能不支持该阶段。");
        return actual;
    });

    private static byte[] FunNative(long rva,byte[] arguments,long? context=null)
    {
        long target=GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            var objects=FunTarget(handle,game,module,context.HasValue);
            if(context.HasValue && objects.Head!=context.Value) throw new InvalidOperationException("当前角色已变化，本次操作未执行。");
            FunCheck(handle,module,rva);
            return context??game;
        });
        return ExecuteEquipmentNative(rva,arguments,contextOverride:target);
    }
}
