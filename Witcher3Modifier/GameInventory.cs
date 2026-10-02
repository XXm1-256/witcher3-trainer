using System.Runtime.InteropServices;
using System.Text;

namespace Witcher3Modifier;

internal sealed record InventoryItem(int ProcessId, long Inventory, int UniqueId, int NameId, int Quantity, string Name, GameItem? Template)
{
    internal string DisplayName => Template?.DisplayName ?? Name;
    internal string Category => Template?.Category ?? "other";
    internal bool IsEquipment => Category is "steelsword" or "silversword" or "armor" or "boots" or "pants" or "gloves" or "crossbow";
    internal int? RequiredLevel { get; set; }
    public override string ToString() => $"{DisplayName} · {Template?.CategoryName ?? "其他"} ×{Quantity} · ID {UniqueId}"
        + (RequiredLevel is {} level ? $" · 需要 {level} 级" : "");
}

internal static class GameInventory
{
    private static readonly Dictionary<string, GameItem> Catalog = GameItem.LoadCatalog().ToDictionary(item => item.Name);
    internal static List<InventoryItem> Read() => GameMoney.WithInventory(false, (handle, _, inventory, module) =>
    {
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (count is < 0 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
        long array = GameMoney.ReadInt64(handle, inventory + 0x140);
        byte[] raw = count == 0 ? [] : GameMoney.ReadBytes(handle, array, count * 0x98);
        var requested = new HashSet<int>();
        for (int i = 0; i < count; i++) requested.Add(BitConverter.ToInt32(raw, i * 0x98 + 0x58));
        var names = ReadNames(handle, module, requested);
        int processId = (int)GetProcessId(handle);
        var result = new List<InventoryItem>();
        for (int i = 0; i < count; i++)
        {
            int nameId = BitConverter.ToInt32(raw, i * 0x98 + 0x58);
            string name = names.GetValueOrDefault(nameId) ?? $"名称ID {nameId}";
            int quantity = BitConverter.ToInt32(raw, i * 0x98 + 0x54);
            int id = BitConverter.ToInt32(raw, i * 0x98 + 0x60);
            if (quantity <= 0) continue;
            result.Add(new(processId, inventory, id, nameId, quantity, name, Catalog.GetValueOrDefault(name)));
        }
        if (GameMoney.ReadInt64(handle, inventory + 0x140) != array || GameMoney.ReadInt32(handle, inventory + 0x148) != count)
            throw new InvalidOperationException("背包正在变化，请重新读取。");
        return result;
    });

    internal static Dictionary<int, string> ReadNames(nint handle, long module, IEnumerable<int> ids)
    {
        var wanted = ids.ToHashSet();
        var result = new Dictionary<int, string>();
        long pool = GameMoney.ReadInt64(handle, module + GameVersion.Rva(0x5874E38));
        int total = GameMoney.ReadInt32(handle, pool + 0x11838);
        if (total is < 10000 or > 1000000) throw new InvalidOperationException("游戏名称池结构异常。");
        byte[] buckets = GameMoney.ReadBytes(handle, pool + 0x28, 0x1FFF * 8);
        int visited = 0;
        for (int bucket = 0; bucket < 0x1FFF && wanted.Count != 0; bucket++)
        {
            long node = BitConverter.ToInt64(buckets, bucket * 8);
            for (int chain = 0; node != 0; chain++)
            {
                if (chain >= 10000 || ++visited > total + 0x1FFF) throw new InvalidOperationException("游戏名称链结构异常。");
                byte[] record = GameMoney.ReadBytes(handle, node, 24);
                int id = BitConverter.ToInt32(record, 0x14);
                if (wanted.Remove(id))
                {
                    long text = BitConverter.ToInt64(record);
                    var encoded = new List<byte>();
                    for (int i = 0; i < 512; i++)
                    {
                        byte value = GameMoney.ReadBytes(handle, text + i, 1)[0];
                        if (value == 0) break;
                        encoded.Add(value);
                        if (i == 511) throw new InvalidOperationException("游戏名称长度异常。");
                    }
                    result[id] = Encoding.UTF8.GetString(encoded.ToArray());
                }
                node = BitConverter.ToInt64(record, 8);
            }
        }
        return result;
    }

    internal static void Validate(InventoryItem expected)
    {
        var actual = Read().SingleOrDefault(item => item.UniqueId == expected.UniqueId);
        if (actual is null || actual.ProcessId != expected.ProcessId || actual.Inventory != expected.Inventory || actual.NameId != expected.NameId)
            throw new InvalidOperationException("选中的物品或游戏进程已变化，请重新读取背包。");
    }
    internal static int Verify(nint handle, long inventory, InventoryItem expected)
    {
        if (GetProcessId(handle) != expected.ProcessId || inventory != expected.Inventory)
            throw new InvalidOperationException("游戏进程或库存已变化，请重新读取。");
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (count is < 0 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
        long array = GameMoney.ReadInt64(handle, inventory + 0x140);
        for (int i = 0; i < count; i++)
        {
            long entry = array + i * 0x98;
            if (GameMoney.ReadInt32(handle, entry + 0x60) != expected.UniqueId) continue;
            if (GameMoney.ReadInt32(handle, entry + 0x58) != expected.NameId)
                throw new InvalidOperationException("物品身份已变化，请重新读取。");
            return GameMoney.ReadInt32(handle, entry + 0x54);
        }
        throw new InvalidOperationException("选中的物品已不在当前背包中。");
    }

    internal static int Delete(InventoryItem item, int quantity) => GameItemScheduler.RunForItem(item, () =>
    {
        int before = GameMoney.WithInventory(false, (handle, _, inventory, _) => Verify(handle, inventory, item));
        if (quantity < 1 || quantity > before) throw new InvalidOperationException("删除数量超出当前持有数量。");
        if (!GameItemScheduler.RemoveInventoryItem(item.UniqueId, quantity)) throw new InvalidOperationException("游戏未执行物品删除。");
        var remaining = Read().SingleOrDefault(entry => entry.UniqueId == item.UniqueId);
        int removed = before - (remaining?.Quantity ?? 0);
        if (removed != quantity) throw new InvalidOperationException($"删除后的数量变化为 {removed}，与请求不一致，请重新读取背包。");
        return removed;
    });
    [DllImport("kernel32.dll")] private static extern uint GetProcessId(nint process);
}
