namespace Witcher3Modifier;

internal static class GameDurability
{
    public static int Preview() => Process(false);
    public static int RepairAll() => Process(true);

    private static int Process(bool apply) => GameMoney.WithInventory(apply, (handle, game, inventory, _) =>
    {
        long items = GameMoney.ReadInt64(handle, inventory + 0x140);
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (items == 0 || count is < 1 or > 10000)
            throw new InvalidOperationException("游戏库存结构异常。");

        long definitions = GameMoney.ReadInt64(handle, game + 0xFD68);
        int bucketCount = GameMoney.ReadInt32(handle, definitions + 0x90);
        long buckets = GameMoney.ReadInt64(handle, definitions + 0xB0);
        if (buckets == 0 || bucketCount is < 1 or > 1000000)
            throw new InvalidOperationException("游戏装备定义结构异常。");

        var repairs = new List<(long Address, int NameId, int UniqueId, float Current, float Maximum)>();
        var seenIds = new HashSet<int>();
        for (int index = 0; index < count; index++)
        {
            long item = items + index * 0x98L;
            float current = GameMoney.ReadSingle(handle, item + 0x64);
            if (!float.IsFinite(current) || current < 0 || current > 100000) continue;

            int nameId = GameMoney.ReadInt32(handle, item + 0x58);
            int uniqueId = GameMoney.ReadInt32(handle, item + 0x60);
            if (nameId <= 0 || uniqueId <= 0) continue;
            long node = GameMoney.ReadInt64(handle, buckets + (uint)nameId % (uint)bucketCount * 8L);
            int hops = 0;
            while (node != 0 && hops++ < 10000)
            {
                if (GameMoney.ReadInt32(handle, node) == nameId &&
                    GameMoney.ReadInt32(handle, node + 0x158) == nameId) break;
                node = GameMoney.ReadInt64(handle, node + 0x160);
            }
            if (hops >= 10000) throw new InvalidOperationException("游戏装备定义链异常。");
            if (node == 0) continue;

            float maximum = GameMoney.ReadSingle(handle, node + 0x9C);
            if (!float.IsFinite(maximum) || maximum <= 0 || maximum > 100000) continue;
            long alternate = GameMoney.ReadInt64(handle, node + 0xC0);
            if (alternate != 0)
            {
                float alternateMax = GameMoney.ReadSingle(handle, alternate + 0x94);
                if (!float.IsFinite(alternateMax) || alternateMax != maximum)
                    throw new InvalidOperationException("装备在不同游戏模式下的耐久上限不一致，已停止操作。");
            }
            if (current >= maximum) continue;
            if (!seenIds.Add(uniqueId)) throw new InvalidOperationException("发现重复装备 ID，已停止操作。");
            repairs.Add((item, nameId, uniqueId, current, maximum));
        }

        if (GameMoney.ReadInt64(handle, inventory + 0x140) != items ||
            GameMoney.ReadInt32(handle, inventory + 0x148) != count)
            throw new InvalidOperationException("库存已变化，请重新操作。");

        if (!apply) return repairs.Count;
        int completed = 0;
        foreach (var repair in repairs)
        {
            if (GameMoney.ReadInt64(handle, inventory + 0x140) != items ||
                GameMoney.ReadInt32(handle, inventory + 0x148) != count ||
                GameMoney.ReadInt32(handle, repair.Address + 0x58) != repair.NameId ||
                GameMoney.ReadInt32(handle, repair.Address + 0x60) != repair.UniqueId ||
                GameMoney.ReadSingle(handle, repair.Address + 0x64) != repair.Current)
                throw new InvalidOperationException($"库存操作中变化；已修复 {completed} 件，请重新打开背包核对。");
            GameMoney.WriteSingle(handle, repair.Address + 0x64, repair.Maximum);
            completed++;
        }
        return completed;
    });
}
