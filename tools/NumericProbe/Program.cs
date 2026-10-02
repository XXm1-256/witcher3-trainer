using System.Text.Json;
using Witcher3Modifier;

// Execute the production numeric planner against an isolated in-memory inventory.
void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
void Reject(string category,Dictionary<string,decimal> targets,bool planningOnly=true)
{
    int attemptsBefore=GameItemScheduler.Attempts;
    bool rejected=false;
    try { GameEquipmentNumbers.Apply(406,category,targets); }
    catch(InvalidOperationException) { rejected=true; }
    Require(rejected,"Invalid request was accepted");
    if(planningOnly) Require(GameItemScheduler.Attempts==attemptsBefore,"Invalid plan attempted a mutation");
}

foreach(var (category,targets) in new (string,Dictionary<string,decimal>)[]
{
    ("steelsword",new(){{"unknown",1}}),
    ("armor",new(){{"poison",25}}),
    ("steelsword",new(){{"poison",101}}),
    ("steelsword",new(){{"damage",5001}}),
    ("steelsword",new(){{"damage",-1}})
})
{
    GameItemScheduler.Reset(); Reject(category,targets);
    Require(GameItemScheduler.Reads==0,"Shape validation should precede runtime reads");
}

GameItemScheduler.Reset();
GameItemScheduler.Baseline["SlashingDamage"]=55;
GameItemScheduler.Baseline["desc_poinsonchance_mult"]=15;
Reject("steelsword",new(){{"poison",25},{"damage",56}});
Require(ErrorLog.LastFailure().GetProperty("Phase").GetString()=="规划","Failure stage missing");

GameItemScheduler.Reset();
GameItemScheduler.Baseline["desc_poinsonchance_mult"]=15;
int greater=GameItemScheduler.ResolveEquipmentAbility("Rune morana greater _Stats");
GameItemScheduler.Crafted.AddRange([greater,greater,greater,9999]);
var result=GameEquipmentNumbers.Apply(406,"steelsword",new Dictionary<string,decimal>{{"poison",29}});
Require(Math.Abs(result["poison"]-29)<.001m && GameItemScheduler.Crafted.Contains(9999),"Decrease or unrelated ability preservation failed");
int priorAttempts=GameItemScheduler.Attempts;
GameEquipmentNumbers.Apply(406,"steelsword",new Dictionary<string,decimal>{{"poison",29}});
Require(GameItemScheduler.Attempts==priorAttempts,"Identical target caused mutations");
Reject("steelsword",new(){{"poison",14}});
Reject("steelsword",new(){{"poison",29.5m}});

GameItemScheduler.Reset();
GameItemScheduler.Crafted.AddRange(Enumerable.Repeat(9999,999));
Reject("armor",new(){{"armor",2}});

GameItemScheduler.Reset();
GameItemScheduler.FailAddAt=2;
Reject("steelsword",new(){{"poison",12}},false);
var failure=ErrorLog.LastFailure();
Require(failure.GetProperty("Phase").GetString()=="增加能力" && failure.GetProperty("ConfirmedAdds").GetInt32()==1 &&
    failure.GetProperty("Ability").GetString()!.Length>0,"Partial add progress missing");

GameItemScheduler.Reset();
GameItemScheduler.Baseline["desc_poinsonchance_mult"]=15;
GameItemScheduler.Crafted.AddRange([greater,greater,greater]);
GameItemScheduler.FailRemoveAt=1;
Reject("steelsword",new(){{"poison",29}},false);
Require(ErrorLog.LastFailure().GetProperty("Phase").GetString()=="移除能力" && GameItemScheduler.AddAttempts==0,"Removal failure must stop additions");

GameItemScheduler.Reset();
GameItemScheduler.CoupleBurningToPoison=true;
Reject("steelsword",new(){{"poison",10},{"burning",5}},false);
Require(ErrorLog.LastFailure().GetProperty("Phase").GetString()=="回读","Final cross-field readback failed to detect later interference");

GameItemScheduler.Reset();
GameItemScheduler.Baseline["SlashingDamage"]=55;
result=GameEquipmentNumbers.Apply(406,"steelsword",new Dictionary<string,decimal>{{"damage",70},{"poison",25},{"burning",5}});
Require(result.Count==3 && Math.Abs(result["damage"]-70)<.001m && Math.Abs(result["poison"]-25)<.001m && Math.Abs(result["burning"]-5)<.001m,"Multi-field targets mismatch");
Console.WriteLine("Production numeric flow passed: invalid shape/range, all-field preflight, exact decrease, no-op, capacity, partial add/remove logs, cross-field interference and final readback. No game process access.");

GameItemScheduler.Reset();
var equipment=new InventoryItem(406,"offline-sword","steelsword");
Require(GameEquipment.CheckEdit(equipment,[],"",0,new Dictionary<string,decimal>{{"poison",25}}),"Unchanged effects did not check numeric plan");
Require(GameItemScheduler.Attempts==0,"Checking equipment mutated inventory");
Require(!GameEquipment.CheckEdit(equipment,["MA_CriticalChance"],"",0,new Dictionary<string,decimal>{{"poison",25}}),"Changed effects falsely reported full numeric check");
foreach(var request in new[]{(new[]{"MA_SlashingResistance"},"",0,new Dictionary<string,decimal>()),(Array.Empty<string>(),"",4,new Dictionary<string,decimal>()),(Array.Empty<string>(),"",0,new Dictionary<string,decimal>{{"poison",.5m}}),(Array.Empty<string>(),"unknown",0,new Dictionary<string,decimal>())})
{
    bool rejected=false;
    try {GameEquipment.CheckEdit(equipment,request.Item1,request.Item2,request.Item3,request.Item4);}
    catch(InvalidOperationException) {rejected=true;}
    Require(rejected && GameItemScheduler.Attempts==0,"Invalid equipment preflight accepted or wrote inventory");
}
Console.WriteLine("Production equipment preflight passed: read-only full/conditional checks; incompatible affix, enchantment, slot limit and unreachable numeric targets rejected before mutation.");

namespace Witcher3Modifier
{
    internal sealed record InventoryItem(int UniqueId,string Name,string Category) {internal bool IsEquipment=>true;}
    internal sealed class GameItem
    {
        internal string Name="offline-sword",Category="steelsword";
        internal static GameItem[] LoadCatalog()=>[new()];
    }
    internal static class GameMoney {internal static T WithInventory<T>(bool write,Func<int,int,int,int,T> action)=>action(0,0,0,0);}
    internal static class GameInventory {internal static Dictionary<int,string> ReadNames(int handle,int module,int[] ids)=>[];}
    internal static class GameItemScheduler
    {
        internal static T RunForItem<T>(InventoryItem item,Func<T> action)=>action();
        internal static T RunSerialized<T>(Func<T> action)=>action();
        internal static int ReadEquipmentSlots(int id)=>0;
        internal static int ReadEquipmentSlotLimit(int id)=>3;
        internal static int ReadEnchantment(int id)=>0;
        internal static bool IsItemHeld(int id)=>false;
        internal static bool IsItemMounted(int id)=>false;
        internal static bool AddEquipmentSlot(int id) {Attempts++;return true;}
        internal static bool EnchantEquipment(int id,string word,string ability) {Attempts++;return true;}
        internal static void ClearEnchantment(int id) {Attempts++;}
        internal static int[] ReadEquipmentIds(string name)=>[];
        internal static (int Added,int Requested) Give(string name,int amount) {Attempts++;return (amount,amount);}
        internal sealed record AttributeValue(float Additive,float Multiplicative,float Base);
        internal static decimal ReadSeedAbilityUnit(int uniqueId,string ability,string attribute) => throw new InvalidOperationException("This probe covers fixed-unit fields only.");
        internal static readonly Dictionary<string,decimal> Baseline=[];
        internal static readonly List<int> Crafted=[];
        internal static readonly Dictionary<string,(int Id,string Attribute,int Units)> Abilities=[];
        internal static int Reads,Attempts,AddAttempts,RemoveAttempts,FailAddAt,FailRemoveAt;
        internal static bool CoupleBurningToPoison;
        static GameItemScheduler()
        {
            Add("sq304_sword_upgrade _Stats","SlashingDamage",7);
            Add("autogen_fixed_steel_dmg","SlashingDamage",8);
            Add("autogen_fixed_silver_dmg","SilverDamage",10);
            Add("autogen_fixed_gloves_base","armor",1);
            foreach(var (stem,attribute) in new[]{("morana","desc_poinsonchance_mult"),("dazhbog","desc_burningchance_mult"),("devana","desc_bleedingchance_mult"),("zoria","desc_freezingchance_mult")})
            {
                Add($"Rune {stem} lesser _Stats",attribute,2);
                Add($"Rune {stem} _Stats",attribute,3);
                Add($"Rune {stem} greater _Stats",attribute,5);
            }
            foreach(var effect in GameEquipment.Presets.Affixes)
                if(!Abilities.ContainsKey(effect.Id)) Add(effect.Id,"",0);
            void Add(string name,string attribute,int unit) => Abilities.Add(name,(Abilities.Count+1,attribute,unit));
        }
        internal static void Reset()
        {
            Baseline.Clear(); Crafted.Clear(); ErrorLog.Records.Clear();
            Reads=Attempts=AddAttempts=RemoveAttempts=FailAddAt=FailRemoveAt=0;
            CoupleBurningToPoison=false;
        }
        internal static int ResolveEquipmentAbility(string name) => Abilities[name].Id;
        internal static int[] ReadCraftedAbilities(int _) { Reads++; return Crafted.ToArray(); }
        internal static AttributeValue ReadEquipmentAttributeParts(int _,string attribute)
        {
            Reads++;
            decimal amount=Baseline.GetValueOrDefault(attribute)+Abilities.Values.Where(value=>value.Attribute==attribute)
                .Sum(value=>(decimal)value.Units*Crafted.Count(id=>id==value.Id));
            return attribute.StartsWith("desc_") ? new((float)(amount/100),0,0) : new(0,0,(float)amount);
        }
        internal static void AddEquipmentAbility(int _,string name,bool __=false)
        {
            Attempts++;
            if(++AddAttempts==FailAddAt) throw new InvalidOperationException("Injected add failure");
            Crafted.Add(ResolveEquipmentAbility(name));
            if(CoupleBurningToPoison && name.StartsWith("Rune dazhbog")) Baseline["desc_poinsonchance_mult"]=Baseline.GetValueOrDefault("desc_poinsonchance_mult")+1;
        }
        internal static void RemoveEquipmentAbility(int _,string name)
        {
            Attempts++;
            if(++RemoveAttempts==FailRemoveAt) throw new InvalidOperationException("Injected remove failure");
            if(!Crafted.Remove(ResolveEquipmentAbility(name))) throw new InvalidOperationException("Missing ability");
        }
    }
    internal static class ErrorLog
    {
        internal static readonly List<(string Operation,JsonElement Context)> Records=[];
        internal static bool Write(string operation,Exception? error,object context)
        { Records.Add((operation,JsonSerializer.SerializeToElement(context))); return true; }
        internal static JsonElement LastFailure() => Records.Last(record=>record.Operation=="装备数值未完成").Context;
    }
}
