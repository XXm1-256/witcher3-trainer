namespace Witcher3Modifier;

internal static partial class GameEquipmentNumbers
{
    internal sealed record Field(string Id, string Label, string Attribute, string Categories, decimal Unit, string[] Abilities,
        string Component = "", int[]? Units = null, string Affix = "", bool SeedUnit = false);
    internal static readonly Field[] Fields =
    [
        new("damage", "基础伤害", "", "steelsword,silversword", 1, []),
        new("armor", "护甲值", "armor", "armor,boots,pants,gloves", 1, ["autogen_fixed_gloves_base"]),
        new("poison", "中毒几率（%）", "desc_poinsonchance_mult", "steelsword,silversword", .01m,
            ["Rune morana lesser _Stats", "Rune morana _Stats", "Rune morana greater _Stats"]),
        new("burning", "燃烧几率（%）", "desc_burningchance_mult", "steelsword,silversword", .01m,
            ["Rune dazhbog lesser _Stats", "Rune dazhbog _Stats", "Rune dazhbog greater _Stats"]),
        new("bleeding", "流血几率（%）", "desc_bleedingchance_mult", "steelsword,silversword", .01m,
            ["Rune devana lesser _Stats", "Rune devana _Stats", "Rune devana greater _Stats"]),
        new("freezing", "冻结几率（%）", "desc_freezingchance_mult", "steelsword,silversword", .01m,
            ["Rune zoria lesser _Stats", "Rune zoria _Stats", "Rune zoria greater _Stats"]),
        new("attackMult","攻击力加成（%）","attack_power",EquipmentCategories,.01m,
            ["Rune elemental lesser _Stats","Rune elemental _Stats","Rune elemental greater _Stats"],"mult",[2,3,5],"MA_AttackPowerMult"),
        new("focusGain","肾上腺素获取（%）","focus_gain",EquipmentCategories,.01m,
            ["Rune perun lesser _Stats","Rune perun _Stats","Rune perun greater _Stats"],"add",[2,3,5],"MA_AdrenalineGain"),
        new("armorPenetration","护甲穿透","armor_reduction","steelsword,silversword",1,
            ["Rune svarog lesser _Stats","Rune svarog _Stats","Rune svarog greater _Stats"],"base",[10,20,30],"MA_ArmorPenetration"),
        new("aard","阿尔德强度（%）","spell_power_aard",EquipmentCategories,.01m,
            ["Glyph aard lesser _Stats","Glyph aard _Stats","Glyph aard greater _Stats"],"mult",[2,5,10],"MA_AardIntensity"),
        new("igni","伊格尼强度（%）","spell_power_igni",EquipmentCategories,.01m,
            ["Glyph igni lesser _Stats","Glyph igni _Stats","Glyph igni greater _Stats"],"mult",[2,5,10],"MA_IgniIntensity"),
        new("quen","昆恩强度（%）","spell_power_quen",EquipmentCategories,.01m,
            ["Glyph quen lesser _Stats","Glyph quen _Stats","Glyph quen greater _Stats"],"mult",[2,5,10],"MA_QuenIntensity"),
        new("yrden","亚登强度（%）","spell_power_yrden",EquipmentCategories,.01m,
            ["Glyph yrden lesser _Stats","Glyph yrden _Stats","Glyph yrden greater _Stats"],"mult",[2,5,10],"MA_YrdenIntensity"),
        new("axii","亚克席强度（%）","spell_power_axii",EquipmentCategories,.01m,
            ["Glyph axii lesser _Stats","Glyph axii _Stats","Glyph axii greater _Stats"],"mult",[2,5,10],"MA_AxiiIntensity"),
        new("moneyBonus","金币奖励加成（%）","bonus_money",EquipmentCategories,.01m,["fogling_trophy_stats"],"add",[5],"MA_Money"),
        new("humanXp","击杀人类经验加成（%）","human_exp_bonus_when_fatal",EquipmentCategories,.01m,["gravehag_trophy_stats"],"add",[5],"MA_HumanHunter"),
        new("monsterXp","击杀怪物经验加成（%）","nonhuman_exp_bonus_when_fatal",EquipmentCategories,.01m,["cave_troll_trophy_stats"],"add",[5],"MA_MonsterHunter"),
        new("staminaRegen","活力恢复（%）","staminaRegen","armor,boots,pants,gloves",.01m,["MA_StaminaRegeneration"],"mult",[1],"MA_StaminaRegeneration"),
        new("vitality","生命值加成","vitality","armor,boots,pants,gloves",1,["lesser_mutagen_color_green_x"],"base",[50],"MA_Vitality"),
        new("slashingResistance","劈斩抗性（%）","slashing_resistance_perc",ArmorCategories,.01m,["MA_SlashingResistance"],"base",null,"MA_SlashingResistance",true),
        new("piercingResistance","穿刺抗性（%）","piercing_resistance_perc",ArmorCategories,.01m,["MA_PiercingResistance"],"base",null,"MA_PiercingResistance",true),
        new("bludgeoningResistance","钝击抗性（%）","bludgeoning_resistance_perc",ArmorCategories,.01m,["MA_BludgeoningResistance"],"base",null,"MA_BludgeoningResistance",true),
        new("rendingResistance","怪物伤害抗性（%）","rending_resistance_perc",ArmorCategories,.01m,["MA_RendingResistance"],"base",null,"MA_RendingResistance",true),
        new("elementalResistance","元素抗性（%）","elemental_resistance_perc",ArmorCategories,.01m,["MA_ElementalResistance"],"base",null,"MA_ElementalResistance",true),
        new("poisonResistance","中毒抗性（%）","poison_resistance_perc",ArmorCategories,.01m,["MA_PoisonResistance"],"base",null,"MA_PoisonResistance",true),
        new("bleedingResistance","流血抗性（%）","bleeding_resistance_perc",ArmorCategories,.01m,["MA_BleedingResistance"],"base",null,"MA_BleedingResistance",true),
        new("burningResistance","燃烧抗性（%）","burning_resistance_perc",ArmorCategories,.01m,["MA_BurningResistance"],"base",null,"MA_BurningResistance",true),
        new("attackAdd","攻击力数值","attack_power",EquipmentCategories,1,["MA_AttackPowerAdd"],"add",null,"MA_AttackPowerAdd",true),
        new("criticalChance","暴击几率（%）","critical_hit_chance",EquipmentCategories,.01m,["MA_CriticalChance"],"add",null,"MA_CriticalChance",true),
        new("criticalDamage","暴击伤害加成（%）","critical_hit_damage_bonus",EquipmentCategories,.01m,["MA_CriticalDamage"],"add",null,"MA_CriticalDamage",true),
        new("stagger","踉跄几率（%）","desc_staggerchance_mult","steelsword,silversword",.01m,["MA_StaggerChance"],"add",null,"MA_StaggerChance",true)
    ];
    private const string EquipmentCategories="steelsword,silversword,armor,boots,pants,gloves";
    private const string ArmorCategories="armor,boots,pants,gloves";

    internal static Field? ForAffix(string id) => Fields.SingleOrDefault(field=>field.Affix==id || field.Id==(id switch
    {
        "MA_PoisonChance"=>"poison", "MA_BurningChance"=>"burning", "MA_BleedingChance"=>"bleeding", "MA_FreezingChance"=>"freezing", _=>""
    }));

    private static decimal Value(Field field, GameItemScheduler.AttributeValue parts) =>
        (decimal)((field.Component.Length==0 ? field.Unit==.01m?"add":"base" : field.Component) switch
        { "add"=>parts.Additive,"mult"=>parts.Multiplicative,_=>parts.Base }) / field.Unit;

    internal static Dictionary<string,decimal> Read(int uniqueId, string category)
    {
        var values=new Dictionary<string,decimal>();
        foreach(var field in Fields.Where(field=>field.Categories.Split(',').Contains(category)))
        {
            string attribute=field.Id=="damage" ? category=="steelsword"?"SlashingDamage":"SilverDamage" : field.Attribute;
            var parts=GameItemScheduler.ReadEquipmentAttributeParts(uniqueId,attribute);
            values[field.Id]=Value(field,parts);
        }
        return values;
    }

    internal static int[] Compose(int amount, int[] units)
    {
        if(amount is < 0 or > 5000) throw new InvalidOperationException("数值增量应在 0—5000 内。");
        int[] choices=Enumerable.Repeat(-1,amount+1).ToArray();
        int[] counts=Enumerable.Repeat(int.MaxValue,amount+1).ToArray();
        counts[0]=0;
        for(int value=1; value<=amount; value++)
            for(int index=0; index<units.Length; index++)
                if(units[index]>0 && value>=units[index] && counts[value-units[index]]!=int.MaxValue && counts[value-units[index]]+1<counts[value])
                { choices[value]=index; counts[value]=counts[value-units[index]]+1; }
        if(counts[amount]==int.MaxValue || counts[amount]>500)
            throw new InvalidOperationException("目标值无法由当前游戏的原生能力准确组合，请调整目标值。");
        int[] result=new int[units.Length];
        for(int value=amount; value>0; value-=units[choices[value]]) result[choices[value]]++;
        return result;
    }

    internal static Dictionary<string,decimal> MinimumTargets(int uniqueId,string category,IReadOnlyDictionary<string,decimal> values,int[] crafted)
    {
        var minimums=new Dictionary<string,decimal>();
        foreach(var field in Fields.Where(field=>field.Id is "damage" or "armor" && values.ContainsKey(field.Id)))
        {
            var pool=ReadBasePool(uniqueId,category,field.Id,crafted);
            decimal added=pool.Units.Zip(pool.Existing).Sum(pair=>pair.First*pair.Second);
            minimums[field.Id]=Math.Round(Math.Max(0,values[field.Id]-added),3);
        }
        return minimums;
    }

    internal sealed record Adjustment(int[] Add, int[] Remove);
    internal static Adjustment PlanSeedAdjustment(decimal current,decimal target,decimal step,int existing)
    {
        if(step<=0 || existing<0) throw new InvalidOperationException("词条原生增量异常。");
        decimal baseline=current-existing*step;
        decimal count=Math.Round((target-baseline)/step,0,MidpointRounding.AwayFromZero);
        decimal nearest=baseline+Math.Clamp(count,0,500)*step;
        if(count<0 || count>500 || Math.Abs(nearest-target)>.001m)
            throw new InvalidOperationException($"此装备每份原生增量约 {step:0.######}，目标不可精确达到；可达值约 {nearest:0.###}。可在背包页选中词条，点击“使用可达值”。");
        return new([Math.Max(0,(int)count-existing)],[Math.Max(0,existing-(int)count)]);
    }

    internal static decimal NearestTarget(int uniqueId,string category,string fieldId,decimal requested)
    {
        Field field=Fields.Single(field=>field.Id==fieldId);
        ValidateTargets(category,new Dictionary<string,decimal>{{fieldId,requested}});
        if(fieldId is "damage" or "armor")
        {
            string attribute=fieldId=="armor"?"armor":category=="steelsword"?"SlashingDamage":"SilverDamage";
            decimal value=Value(field,GameItemScheduler.ReadEquipmentAttributeParts(uniqueId,attribute));
            var pool=ReadBasePool(uniqueId,category,fieldId,GameItemScheduler.ReadCraftedAbilities(uniqueId));
            return Math.Round(PlanBaseAdjustment(value,requested,pool.Units,pool.Existing).Target,3);
        }
        if(!field.SeedUnit) return requested;
        decimal current=Value(field,GameItemScheduler.ReadEquipmentAttributeParts(uniqueId,field.Attribute));
        decimal step=GameItemScheduler.ReadSeedAbilityUnit(uniqueId,field.Abilities[0],field.Attribute)/field.Unit;
        int id=GameItemScheduler.ResolveEquipmentAbility(field.Abilities[0]);
        int existing=GameItemScheduler.ReadCraftedAbilities(uniqueId).Count(value=>value==id);
        decimal baseline=current-existing*step;
        decimal minimum=Math.Max(0,Math.Ceiling(-baseline/step));
        decimal maximum=Math.Min(500,Math.Floor(((field.Unit==.01m?100:5000)-baseline)/step));
        if(minimum>maximum) throw new InvalidOperationException("基础值超出当前目标范围，无法计算可达目标。");
        decimal count=Math.Clamp(Math.Round((requested-baseline)/step,0,MidpointRounding.AwayFromZero),minimum,maximum);
        return Math.Round(baseline+count*step,3);
    }
    internal static void ValidateTargets(string category, IReadOnlyDictionary<string,decimal> targets)
    {
        foreach(var pair in targets)
        {
            Field field=Fields.SingleOrDefault(field=>field.Id==pair.Key) ?? throw new InvalidOperationException("属性不在数值目录中。");
            if(!field.Categories.Split(',').Contains(category)) throw new InvalidOperationException($"{field.Label}不适用于此装备类型。");
            if(pair.Value<0 || pair.Value>(field.Unit==.01m?100:5000)) throw new InvalidOperationException($"{field.Label}超出可设置范围。");
        }
    }
    internal static Adjustment PlanAdjustment(decimal current, decimal target, int[] units, int[] existing)
    {
        if(units.Length!=existing.Length || existing.Any(count=>count<0))
            throw new InvalidOperationException("已有能力数量与单位不一致。");
        if(Math.Abs(current-target)<=.001m) return new(new int[units.Length],new int[units.Length]);
        decimal baseline=current-existing.Zip(units).Sum(pair=>(decimal)pair.First*pair.Second);
        decimal difference=target-baseline;
        if(difference<-.0001m || difference>5000.001m)
            throw new InvalidOperationException($"目标 {target:0.###} 低于此装备的固有下限 {baseline:0.###}，或超出可增加的 5000 点；现有原生组合只能移除后加的能力，不能降低装备固有值。");
        int increment=(int)Math.Round(difference);
        if(difference<-.0001m || Math.Abs(difference-increment)>.001m)
            throw new InvalidOperationException($"原生基础值为 {baseline:0.###}，目标必须能由基础值与已有能力准确组合。");
        int[] desired=Compose(increment,units);
        return new(desired.Zip(existing).Select(pair=>Math.Max(0,pair.First-pair.Second)).ToArray(),
            existing.Zip(desired).Select(pair=>Math.Max(0,pair.First-pair.Second)).ToArray());
    }

    private sealed record Job(Field Field,string Attribute,decimal Target,decimal Before,string[] Abilities,Adjustment Plan);
    internal static void Check(int uniqueId,string category,IReadOnlyDictionary<string,decimal> targets) => Prepare(uniqueId,category,targets);

    private static List<Job> Prepare(int uniqueId,string category,IReadOnlyDictionary<string,decimal> targets)
    {
        ValidateTargets(category,targets);
        var jobs=new List<Job>();
        int[] crafted=GameItemScheduler.ReadCraftedAbilities(uniqueId);
        foreach(var pair in targets)
        {
            Field field=Fields.Single(field=>field.Id==pair.Key);
            bool chance=field.Unit==.01m;
            string attribute=field.Id=="damage" ? category=="steelsword"?"SlashingDamage":"SilverDamage" : field.Attribute;
            var parts=GameItemScheduler.ReadEquipmentAttributeParts(uniqueId,attribute);
            decimal before=Value(field,parts);
            if(field.Id is "damage" or "armor")
            {
                var pool=ReadBasePool(uniqueId,category,field.Id,crafted);
                var proposed=PlanBaseAdjustment(before,pair.Value,pool.Units,pool.Existing);
                if(Math.Abs(proposed.Target-pair.Value)>.001m)
                    throw new InvalidOperationException($"{field.Label}目标无法精确组合，可达值约 {proposed.Target:0.###}。可点击“使用可达目标”填入后应用。");
                jobs.Add(new(field,attribute,pair.Value,before,pool.Abilities,proposed.Plan));
                continue;
            }
            string[] abilities=field.Abilities;
            int[] units=field.Units ?? (chance?[2,3,5]:[1]);
            int[] ids=abilities.Select(GameItemScheduler.ResolveEquipmentAbility).ToArray();
            int[] existing=ids.Select(id=>crafted.Count(value=>value==id)).ToArray();
            try
            {
                var plan=field.SeedUnit ? PlanSeedAdjustment(before,pair.Value,
                    GameItemScheduler.ReadSeedAbilityUnit(uniqueId,abilities[0],attribute)/field.Unit,existing[0])
                    : PlanAdjustment(before,pair.Value,units,existing);
                jobs.Add(new(field,attribute,pair.Value,before,abilities,plan));
            }
            catch(InvalidOperationException ex) { throw new InvalidOperationException($"{field.Label}：{ex.Message}",ex); }
        }
        int additions=jobs.Sum(job=>job.Plan.Add.Sum());
        int removals=jobs.Sum(job=>job.Plan.Remove.Sum());
        if(crafted.Length-removals+additions>1000)
            throw new InvalidOperationException("目标配置需要的能力数量超出当前安全读取上限，请降低目标值。");
        return jobs;
    }

    internal static Dictionary<string, decimal> Apply(int uniqueId, string category, IReadOnlyDictionary<string, decimal> targets)
    {
        var applied=new Dictionary<string,decimal>();
        int added=0,removed=0;
        string phase="规划",fieldId="",ability="";
        try
        {
            var jobs=Prepare(uniqueId,category,targets);
            ErrorLog.Write("装备数值计划",null,new {ItemId=uniqueId,category,Jobs=jobs});
            phase="移除能力";
            foreach(var job in jobs)
                for(int index=0; index<job.Abilities.Length; index++)
                    for(int count=0; count<job.Plan.Remove[index]; count++)
                    {
                        fieldId=job.Field.Id; ability=job.Abilities[index];
                        GameItemScheduler.RemoveEquipmentAbility(uniqueId,ability); removed++;
                    }
            phase="增加能力";
            foreach(var job in jobs)
                for(int index=0; index<job.Abilities.Length; index++)
                    for(int count=0; count<job.Plan.Add[index]; count++)
                    {
                        fieldId=job.Field.Id; ability=job.Abilities[index];
                        GameItemScheduler.AddEquipmentAbility(uniqueId,ability,true); added++;
                    }
            phase="回读"; ability="";
            foreach(var job in jobs)
            {
                fieldId=job.Field.Id;
                var parts=GameItemScheduler.ReadEquipmentAttributeParts(uniqueId,job.Attribute);
                decimal actual=Value(job.Field,parts);
                if(Math.Abs(actual-job.Target)>.001m)
                    throw new InvalidOperationException($"{job.Field.Label}回读为 {actual:0.###}，目标为 {job.Target}；部分修改可能已执行，请重新读取装备。");
                applied[job.Field.Id]=actual;
                ErrorLog.Write("装备数值修改",null,new {ItemId=uniqueId,job.Field.Id,job.Before,Target=job.Target,Actual=actual});
            }
            return applied;
        }
        catch(Exception ex)
        {
            ErrorLog.Write("装备数值未完成",ex,new {ItemId=uniqueId,category,Targets=targets,Phase=phase,Field=fieldId,Ability=ability,ConfirmedAdds=added,ConfirmedRemoves=removed,ConfirmedFields=applied});
            throw;
        }
    }
}
