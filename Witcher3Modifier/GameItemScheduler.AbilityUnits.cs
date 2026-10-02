namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    internal static float EvaluateSeedUnit(float first,float second,ushort seed,float scale)
    {
        uint mixed=unchecked((uint)seed<<13)^(uint)seed;
        uint hash=unchecked((mixed*mixed*0x3D73u+0xC0AE5u)*mixed-0x2DF722F3u)&0x7FFFFFFFu;
        float span=MathF.Max(first,second)-MathF.Min(first,second);
        float value=(float)hash*span;
        value*=scale;
        return value+MathF.Min(first,second);
    }

    internal static decimal ReadSeedAbilityUnit(int uniqueId,string ability,string attribute,bool singleBase=false) => GameMoney.WithInventory(false,(handle,game,inventory,module)=>
    {
        int itemCount=GameMoney.ReadInt32(handle,inventory+0x148);
        if(itemCount is <0 or >10000) throw new InvalidOperationException("库存结构异常。");
        long items=GameMoney.ReadInt64(handle,inventory+0x140), item=0;
        for(int index=0;index<itemCount;index++)
            if(GameMoney.ReadInt32(handle,items+index*0x98+0x60)==uniqueId) {item=items+index*0x98;break;}
        if(item==0) throw new InvalidOperationException("装备已不在背包中。");
        ushort seed=BitConverter.ToUInt16(GameMoney.ReadBytes(handle,item+0x68,2));
        int abilityId=ResolveEquipmentAbility(ability),attributeId=ResolveName(handle,module,attribute);
        long definitions=GameMoney.ReadInt64(handle,game+0xFD68);
        int bucketCount=GameMoney.ReadInt32(handle,definitions+0x60);
        if(bucketCount is <1 or >100000) throw new InvalidOperationException("能力定义表异常。");
        long buckets=GameMoney.ReadInt64(handle,definitions+0x80);
        long node=GameMoney.ReadInt64(handle,buckets+(uint)abilityId%(uint)bucketCount*8);
        for(int index=0;node!=0 && index<10000;index++)
        {
            if(GameMoney.ReadInt32(handle,node)==abilityId && GameMoney.ReadInt32(handle,node+0x60)==abilityId) break;
            node=GameMoney.ReadInt64(handle,node+0x68);
        }
        if(node==0 || GameMoney.ReadInt32(handle,node)!=abilityId || GameMoney.ReadInt32(handle,node+0x60)!=abilityId)
            throw new InvalidOperationException("能力定义已变化。");
        int count=GameMoney.ReadInt32(handle,node+0x10);
        if(count is <1 or >100) throw new InvalidOperationException("词条属性表异常。");
        if(singleBase && count!=1) throw new InvalidOperationException("该原生增量还包含其他属性，不能单独调整。");
        long records=GameMoney.ReadInt64(handle,node+8);
        for(int index=0;index<count;index++)
        {
            byte[] record=GameMoney.ReadBytes(handle,records+index*24,24);
            if(BitConverter.ToInt32(record,0)!=attributeId) continue;
            if(singleBase && BitConverter.ToInt32(record,4)!=0) throw new InvalidOperationException("该原生增量不是基础值。");
            if(record[8]!=0 || unchecked((sbyte)record[20])!=-1)
                throw new InvalidOperationException("此词条使用不同的随机或舍入规则，暂不能精确组合。");
            float first=BitConverter.ToSingle(record,12),second=BitConverter.ToSingle(record,16);
            float scale=BitConverter.ToSingle(GameMoney.ReadBytes(handle,module + GameVersion.Rva(0x3AD3728),4));
            float value=EvaluateSeedUnit(first,second,seed,scale);
            if(!float.IsFinite(value) || value<=0 || !float.IsFinite(scale) || scale<=0 || scale>1e-8f)
                throw new InvalidOperationException("词条原生单位异常。");
            return (decimal)value;
        }
        throw new InvalidOperationException("能力定义中未找到对应属性。");
    });
}
