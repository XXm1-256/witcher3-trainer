using System.Text.Json;

namespace Witcher3Modifier;

internal static class GameEquipmentLevels
{
    internal sealed class Profile
    {
        public bool Known { get; set; }
        public string Category { get; set; } = "";
        public float Quality { get; set; }
        public float StatMin { get; set; }
        public float StatMax { get; set; }
        public float PrimaryMin { get; set; }
        public float PrimaryMax { get; set; }
        public string[] Tags { get; set; } = [];
        public bool Dynamic { get; set; }
    }
    internal sealed class Modes { public Profile? Normal { get; set; } public Profile? Plus { get; set; } }
    internal sealed record Live(int Level, Profile Values);
    private static readonly Dictionary<string,Modes> Profiles = Load();
    internal static bool NewGamePlus { get; set; }
    private static Dictionary<string,Modes> Load()
    {
        using var stream = typeof(GameEquipmentLevels).Assembly.GetManifestResourceStream("Witcher3Modifier.EquipmentLevels.json")
            ?? throw new InvalidOperationException("缺少装备等级目录。");
        return JsonSerializer.Deserialize<Dictionary<string,Modes>>(stream)!;
    }
    internal static Profile? Reference(string name) => Profiles.TryGetValue(name, out var modes)
        ? NewGamePlus ? modes.Plus ?? modes.Normal : modes.Normal ?? modes.Plus : null;

    internal static int Calculate(Profile profile, float stat)
    {
        int level = profile.Category switch
        {
            "armor" => (int)MathF.Floor(1 + (stat - 25) / 5),
            "boots" or "pants" => (int)MathF.Floor(1 + (stat - 5) / 2),
            "gloves" => (int)MathF.Floor(1 + (stat - 1) / 2),
            "steelsword" => (int)MathF.Ceiling(1 + (stat - 25) / 8),
            "silversword" => (int)MathF.Ceiling(1 + (stat - 90) / 10),
            "crossbow" => CrossbowLevel(stat),
            _ => 1
        };
        int baseLevel = Math.Max(1, level - 1);
        level = NewGamePlus && baseLevel > 100 ? baseLevel : Math.Min(100, baseLevel);
        int quality = (int)MathF.Floor(profile.Quality + .5f);
        if (quality == 5) level -= 2;
        else if (quality == 4) level--;
        level = Math.Max(1, level);
        if (profile.Tags.Contains("OlgierdSabre")) level -= 3;
        if (profile.Tags.Contains("EP1") && quality is 4 or 5) level--;
        return NewGamePlus ? Math.Min(100, level) : level;
    }
    private static int CrossbowLevel(float stat)
    {
        float[] thresholds = [1.01f,1.1f,1.2f,1.3f,1.4f,1.5f,1.6f,1.7f,1.8f,1.9f];
        int[] levels = [2,4,8,11,15,19,22,25,27,32];
        int level = 1;
        for(int i=0;i<thresholds.Length;i++) if(stat>thresholds[i]) level=levels[i];
        return level;
    }
    internal static (int Min,int Max)? Range(Profile? profile, IReadOnlyDictionary<string,decimal>? targets=null)
    {
        if(profile is not {Known:true}) return null;
        float min=profile.StatMin,max=profile.StatMax;
        string field=profile.Category.EndsWith("sword") ? "damage" : "armor";
        if(targets is not null && targets.TryGetValue(field,out decimal target) && profile.Category!="crossbow")
        {
            min=min-profile.PrimaryMin+(float)target;
            max=max-profile.PrimaryMax+(float)target;
        }
        int a=Calculate(profile,min),b=Calculate(profile,max);
        return (Math.Min(a,b),Math.Max(a,b));
    }
    internal static string Describe(Profile? profile,IReadOnlyDictionary<string,decimal>? targets=null)
    {
        var range=Range(profile,targets);
        return range is {} value ? value.Min==value.Max ? $"{value.Min} 级" : $"{value.Min}—{value.Max} 级" : "生成后确定";
    }
    internal static Live Read(InventoryItem item) => GameItemScheduler.RunForItem(item,()=>
    {
        GameMoney.WithInventory(false,(handle,_,inventory,_)=>GameInventory.Verify(handle,inventory,item));
        var qualityParts=GameItemScheduler.ReadEquipmentAttributeParts(item.UniqueId,"quality",true);
        float quality=(qualityParts.Additive+qualityParts.Base)*(1+qualityParts.Multiplicative);
        float primary=0,stat;
        if(item.Category is "steelsword" or "silversword")
        {
            string main=item.Category=="steelsword" ? "SlashingDamage" : "SilverDamage";
            string[] names=item.Category=="steelsword"
                ? ["SlashingDamage","BludgeoningDamage","RendingDamage","ElementalDamage","FireDamage","SilverDamage","PiercingDamage"]
                : ["SilverDamage","BludgeoningDamage","RendingDamage","ElementalDamage","FireDamage","PiercingDamage"];
            stat=1;
            foreach(string name in names)
            {
                float value=GameItemScheduler.ReadEquipmentAttributeParts(item.UniqueId,name,true).Base;
                stat+=value-1;
                if(name==main) primary=value;
            }
        }
        else stat=primary=item.Category=="crossbow" ? GameItemScheduler.ReadEquipmentAttributeParts(item.UniqueId,"attack_power",true).Multiplicative
            : GameItemScheduler.ReadEquipmentAttributeParts(item.UniqueId,"armor",true).Base;
        var profile=new Profile {Known=true,Category=item.Category,Quality=quality,StatMin=stat,StatMax=stat,
            PrimaryMin=primary,PrimaryMax=primary,Tags=Reference(item.Name)?.Tags ?? []};
        GameMoney.WithInventory(false,(handle,_,inventory,_)=>GameInventory.Verify(handle,inventory,item));
        return new Live(Calculate(profile,stat),profile);
    });
}
