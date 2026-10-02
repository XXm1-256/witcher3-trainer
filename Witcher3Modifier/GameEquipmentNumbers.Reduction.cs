namespace Witcher3Modifier;

internal static partial class GameEquipmentNumbers
{
    private sealed record BasePool(string[] Abilities,decimal[] Units,int[] Existing);
    private static BasePool ReadBasePool(int uniqueId,string category,string field,int[] crafted)
    {
        string attribute=field=="armor"?"armor":category=="steelsword"?"SlashingDamage":"SilverDamage";
        string[] primary=field=="armor"?["autogen_fixed_gloves_base"]:category=="steelsword"?["sq304_sword_upgrade _Stats","autogen_fixed_steel_dmg"]:["autogen_fixed_silver_dmg"];
        var names=GameMoney.WithInventory(false,(handle,_,_,module)=>GameInventory.ReadNames(handle,module,crafted.Distinct()));
        string[] candidates=field=="armor"
            ? ["autogen_armor_armor","autogen_armor_base","autogen_pants_armor","autogen_pants_base","autogen_gloves_armor","autogen_gloves_base","autogen_fixed_armor_armor","autogen_fixed_armor_base","autogen_fixed_pants_armor","autogen_fixed_pants_base","autogen_fixed_gloves_armor"]
            : category=="steelsword" ? ["autogen_steel_dmg","autogen_steel_base","autogen_fixed_steel_base","q402_sword_upgrade _Stats"]
            : ["autogen_silver_dmg","autogen_silver_base","autogen_fixed_silver_base"];
        var pool=primary.Concat(candidates.Where(name=>names.ContainsValue(name))).Distinct().Select(name=>
        {
            int id=GameItemScheduler.ResolveEquipmentAbility(name);
            return (Name:name,Unit:GameItemScheduler.ReadSeedAbilityUnit(uniqueId,name,attribute,true),Count:crafted.Count(value=>value==id));
        }).OrderBy(value=>value.Unit%1!=0).ToArray();
        return new(pool.Select(p=>p.Name).ToArray(),pool.Select(p=>p.Unit).ToArray(),pool.Select(p=>p.Count).ToArray());
    }

    internal sealed record BaseAdjustment(decimal Target,Adjustment Plan);
    internal static BaseAdjustment PlanBaseAdjustment(decimal current,decimal requested,decimal[] units,int[] existing)
    {
        if(units.Length!=existing.Length || units.Length==0 || units.Any(unit=>unit<=0) || existing.Any(count=>count<0))
            throw new InvalidOperationException("装备基础增量数据异常。");
        if(Math.Abs(current-requested)<=.001m) return new(current,new(new int[units.Length],new int[units.Length]));
        decimal baseline=current-units.Zip(existing).Sum(pair=>pair.First*pair.Second);
        int fixedCount=units.TakeWhile(unit=>unit%1==0).Count();
        if(fixedCount==0 || units.Skip(fixedCount).Any(unit=>unit%1==0) || units.Length-fixedCount>2)
            throw new InvalidOperationException("此装备的随机增量组合暂不支持精确规划。");
        int[] choices=Enumerable.Repeat(-1,5001).ToArray(),cost=Enumerable.Repeat(int.MaxValue,5001).ToArray();
        cost[0]=0;
        for(int value=1;value<=5000;value++)
            for(int i=0;i<fixedCount;i++)
                if(units[i]<=value && cost[value-(int)units[i]]<500 && cost[value-(int)units[i]]+1<cost[value])
                {cost[value]=cost[value-(int)units[i]]+1;choices[value]=i;}
        int[] reachable=Enumerable.Range(0,5001).Where(value=>cost[value]<=500).ToArray();
        decimal bestDistance=decimal.MaxValue,bestTarget=0;
        int bestOperations=int.MaxValue;
        int[]? best=null;
        int a=fixedCount,b=fixedCount+1;
        int maxA=a<units.Length?existing[a]:0,maxB=b<units.Length?existing[b]:0;
        for(int keepA=0;keepA<=maxA;keepA++)
            for(int keepB=0;keepB<=maxB;keepB++)
            {
                decimal retained=baseline+(a<units.Length?units[a]*keepA:0)+(b<units.Length?units[b]*keepB:0);
                int at=Array.BinarySearch(reachable,(int)Math.Clamp(Math.Floor(requested-retained),0,5000));
                if(at<0) at=~at;
                for(int index=Math.Max(0,at-1);index<=Math.Min(reachable.Length-1,at+1);index++)
                {
                    int amount=reachable[index];decimal target=retained+amount;
                    if(target<0 || target>5000) continue;
                    decimal distance=Math.Abs(target-requested);
                    if(distance>bestDistance) continue;
                    int[] desired=new int[units.Length];
                    for(int rest=amount;rest>0;rest-=(int)units[choices[rest]]) desired[choices[rest]]++;
                    if(a<units.Length) desired[a]=keepA;
                    if(b<units.Length) desired[b]=keepB;
                    int operations=desired.Zip(existing).Sum(pair=>Math.Abs(pair.First-pair.Second));
                    if(distance==bestDistance && operations>=bestOperations) continue;
                    bestDistance=distance;bestTarget=target;bestOperations=operations;best=desired;
                }
            }
        if(best is null) throw new InvalidOperationException("没有找到范围内的原生组合目标。");
        return new(bestTarget,new(best.Zip(existing).Select(pair=>Math.Max(0,pair.First-pair.Second)).ToArray(),existing.Zip(best).Select(pair=>Math.Max(0,pair.First-pair.Second)).ToArray()));
    }
}
