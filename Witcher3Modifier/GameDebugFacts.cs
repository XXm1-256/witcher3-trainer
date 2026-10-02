using System.Text;

namespace Witcher3Modifier;

internal static class GameDebugFacts
{
    internal static readonly IReadOnlyDictionary<string,string> Names=new Dictionary<string,string>
    {
        ["unlimitedStamina"]="debug_fact_stamina_boy",
        ["horseStamina"]="debug_fact_stamina_pony",
        ["keepAmmo"]="debug_fact_inf_bolts",
        ["noFallDamage"]="block_falling_damage"
    };

    internal static byte[] Arguments(string feature,bool add)
    {
        byte[] name=Encoding.ASCII.GetBytes(Names[feature]);
        return add
            ? [7,..BitConverter.GetBytes(name.Length),..name,4,..BitConverter.GetBytes(1),4,..BitConverter.GetBytes(-1),9,0,0,0x20]
            : [7,..BitConverter.GetBytes(name.Length),..name,0x20];
    }

    internal static bool Read(string feature) => ReadNamed(Names[feature]);
    internal static bool ReadNamed(string name) => GameMoney.WithInventory(false,(handle,game,_,_)=>
    {
        int systems=GameMoney.ReadInt32(handle,game+0xFDC0);
        if(systems is <2 or >1024) throw new InvalidOperationException("游戏事件系统尚不可用。");
        long manager=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFDB8)+8);
        if(manager==0) throw new InvalidOperationException("游戏事件管理器尚不可用。");
        int buckets=GameMoney.ReadInt32(handle,manager+0x60);
        int count=GameMoney.ReadInt32(handle,manager+0x64);
        if(count==0) return false;
        if(buckets is <1 or >1000000 || count is <0 or >1000000) throw new InvalidOperationException("游戏事件表结构异常。");
        uint hash=0;
        foreach(byte value in Encoding.ASCII.GetBytes(name)) hash=unchecked(hash*31+value);
        long table=GameMoney.ReadInt64(handle,manager+0x80);
        if(table==0) throw new InvalidOperationException("游戏事件索引尚不可用。");
        long node=GameMoney.ReadInt64(handle,table+(hash%(uint)buckets)*8);
        byte[] expected=Encoding.ASCII.GetBytes(name+"\0");
        for(int index=0;node!=0 && index<count;index++)
        {
            if((uint)GameMoney.ReadInt32(handle,node+0x1C)==hash && GameMoney.ReadInt32(handle,node+8)==expected.Length)
            {
                long text=GameMoney.ReadInt64(handle,node);
                if(text!=0 && GameMoney.ReadBytes(handle,text,expected.Length).SequenceEqual(expected)) return true;
            }
            node=GameMoney.ReadInt64(handle,node+0x20);
        }
        if(node!=0) throw new InvalidOperationException("游戏事件链结构异常。");
        return false;
    });

    internal static bool Set(string feature,bool enabled) => GameItemScheduler.RunSerialized(()=>
    {
        if(Read(feature)==enabled) return enabled;
        GameItemScheduler.ExecuteDebugFact(enabled?0x1F5CEF0:0x1F5C4E0,Arguments(feature,enabled));
        bool actual=Read(feature);
        if(actual!=enabled) throw new InvalidOperationException("游戏内置开关执行后回读未确认，请重新读取状态。");
        return actual;
    });
}
