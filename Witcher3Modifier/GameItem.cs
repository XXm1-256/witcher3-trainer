using System.Text.Json;

namespace Witcher3Modifier;

internal sealed class GameItem
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string[] SearchNames { get; set; } = [];
    public string Category { get; set; } = "";
    public string Source { get; set; } = "";

    public string CategoryName => Category switch
    {
        "misc" => "杂项", "crafting_schematic" => "制作图纸", "steelsword" => "钢剑",
        "book" => "书籍", "gwint" => "昆特牌", "alchemy_ingredient" => "炼金材料",
        "alchemy_recipe" => "炼金配方", "armor" => "胸甲", "monster_weapon" => "怪物武器",
        "crafting_ingredient" => "制作材料", "junk" => "杂物", "silversword" => "银剑",
        "work" => "工作用品", "steel_scabbards" => "钢剑剑鞘", "boots" => "靴子",
        "potion" => "药水", "key" => "钥匙", "head" => "头部装备", "edibles" => "食物", "dye" => "染色剂",
        "pants" => "裤子", "gloves" => "手套", "mask" => "面具", "upgrade" => "符文与强化物",
        "oil" => "剑油", "silver_scabbards" => "银剑剑鞘", "secondary" => "副武器",
        "work_secondary" => "副手工作用品", "trophy" => "战利品", "petard" => "炸弹",
        "shield" => "盾牌", "decorations" => "装饰物", "crossbow" => "弩", "horse_saddle" => "马鞍",
        "bolt" => "弩箭", "usable" => "可使用物品", "other" => "其他", "tool" => "工具",
        "hammer2h" => "双手锤", "horse_blinder" => "马眼罩", "fist" => "拳套", "hair" => "发型",
        "blunt1h" => "单手钝器", "axe2h" => "双手斧", "halberd2h" => "双手戟",
        "horse_bag" => "马鞍袋", "axe1h" => "单手斧", "spear2h" => "双手矛", "bow" => "弓",
        "horse_tail" => "马尾", "staff2h" => "双手杖", "horse_hair" => "马鬃",
        "horse_reins" => "缰绳", "cleaver1h" => "单手砍刀", "cooking_recipe" => "烹饪配方",
        "polearm" => "长柄武器", "horse_harness" => "马具", "lute" => "鲁特琴",
        _ => Category
    };

    internal bool Matches(string query, bool fuzzy)
    {
        query = query.Trim().Replace("毒蛇学派", "蛇派").Replace("毒蛇派", "蛇派");
        return new[] { Name, DisplayName, Category, CategoryName }.Concat(SearchNames).Any(name =>
        {
            if (!fuzzy) return name.Contains(query, StringComparison.OrdinalIgnoreCase);
            var characters = name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray();
            int position = 0;
            foreach (char character in query.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant))
            {
                while (position < characters.Length && characters[position] != character) position++;
                if (position == characters.Length) return false;
                position++;
            }
            return true;
        }) || (fuzzy && query.Length >= 3 && query.All(character => character is >= '一' and <= '鿿')
            && new[] { DisplayName }.Concat(SearchNames).Any(name => OneChineseTypo(name, query)));
    }

    private static bool OneChineseTypo(string name, string query)
    {
        for (int start = 0; start + query.Length <= name.Length; start++)
        {
            int different = 0;
            for (int index = 0; index < query.Length && different <= 1; index++)
                if (name[start + index] != query[index]) different++;
            if (different == 1) return true;
        }
        return false;
    }

    public override string ToString() => (DisplayName == Name
        ? $"{Name}  ·  {CategoryName}"
        : $"{DisplayName}（{Name}）  ·  {CategoryName}")
        + (Category is "steelsword" or "silversword" or "armor" or "boots" or "pants" or "gloves" or "crossbow"
            ? $" · 参考等级 {GameEquipmentLevels.Describe(GameEquipmentLevels.Reference(Name))}" : "");

    internal static IReadOnlyList<GameItem> LoadCatalog()
    {
        using Stream stream = typeof(GameItem).Assembly.GetManifestResourceStream("Witcher3Modifier.ItemCatalog.json")
            ?? throw new InvalidOperationException("程序缺少物品目录资源。");
        return JsonSerializer.Deserialize<List<GameItem>>(stream)
            ?? throw new InvalidOperationException("无法读取物品目录。");
    }
}
