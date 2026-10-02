using System.Text.Json;

namespace Witcher3Modifier;

internal sealed class EquipmentEffect
{
    public string Id { get; set; } = "";
    public string Ability { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Equipment { get; set; } = "";
    public string ApplicableCategories => Id switch
    {
        "MA_SlashingResistance" or "MA_PiercingResistance" or "MA_BludgeoningResistance" or "MA_RendingResistance" or
        "MA_ElementalResistance" or "MA_PoisonResistance" or "MA_BleedingResistance" or "MA_BurningResistance" or
        "MA_Vitality" or "MA_StaminaRegeneration" => "armor,boots,pants,gloves",
        "MA_ArmorPenetration" or "MA_BleedingChance" or "MA_FreezingChance" or "MA_PoisonChance" or "MA_BurningChance" or "MA_StaggerChance" => "steelsword,silversword",
        _ => "steelsword,silversword,armor,boots,pants,gloves"
    };
    public bool AppliesTo(string category) => ApplicableCategories.Split(',').Contains(category);
    public string TypeHint => ApplicableCategories=="armor,boots,pants,gloves" ? "护甲" : ApplicableCategories=="steelsword,silversword" ? "钢剑／银剑" : "武器／护甲";
    public override string ToString() => Id.StartsWith("MA_",StringComparison.Ordinal) ? $"{DisplayName}（{TypeHint}）" : DisplayName;
}

internal sealed class EquipmentPresets
{
    public List<EquipmentEffect> Affixes { get; set; } = [];
    public List<EquipmentEffect> Enchantments { get; set; } = [];
    public static EquipmentPresets Load()
    {
        using var stream = typeof(EquipmentPresets).Assembly.GetManifestResourceStream("Witcher3Modifier.EquipmentPresets.json")
            ?? throw new InvalidOperationException("装备效果目录缺失。");
        return JsonSerializer.Deserialize<EquipmentPresets>(stream) ?? throw new InvalidOperationException("装备效果目录无效。");
    }
}

internal static class GameEquipment
{
    internal static readonly EquipmentPresets Presets = EquipmentPresets.Load();
    internal static void ValidateConfiguration(string category,IEnumerable<string> affixes,string enchantment,int slots,IReadOnlyDictionary<string,decimal> targets)
    {
        if(category is not ("steelsword" or "silversword" or "armor" or "boots" or "pants" or "gloves" or "crossbow"))
            throw new InvalidOperationException("请选择基础武器或护甲。");
        GameEquipmentNumbers.ValidateTargets(category,targets);
        if(slots is <0 or >3) throw new InvalidOperationException("最低槽位应在0—3之间。");
        foreach(string id in affixes.Distinct())
        {
            var effect=Presets.Affixes.SingleOrDefault(effect=>effect.Id==id) ?? throw new InvalidOperationException("词条不在游戏效果目录中。");
            if(!effect.AppliesTo(category)) throw new InvalidOperationException($"{effect.DisplayName}仅适用于{effect.TypeHint}。");
        }
        if(enchantment.Length!=0)
        {
            var word=Presets.Enchantments.SingleOrDefault(effect=>effect.Id==enchantment) ?? throw new InvalidOperationException("附魔不在游戏效果目录中。");
            if(!(word.Equipment=="剑" ? category is "steelsword" or "silversword" : category=="armor"))
                throw new InvalidOperationException("附魔不适用于此装备类型。");
        }
    }
    internal sealed record State(int Slots, int Limit, int[] Crafted, string Enchantment, bool Equipped);
    internal static State Inspect(InventoryItem item) => GameItemScheduler.RunForItem(item, () =>
    {
        if (!item.IsEquipment) throw new InvalidOperationException("请选择武器或护甲。");
        int slots = GameItemScheduler.ReadEquipmentSlots(item.UniqueId);
        int limit = GameItemScheduler.ReadEquipmentSlotLimit(item.UniqueId);
        int word = GameItemScheduler.ReadEnchantment(item.UniqueId);
        string name = word == 0 ? "" : GameMoney.WithInventory(false, (handle, _, _, module) =>
            GameInventory.ReadNames(handle, module, [word]).GetValueOrDefault(word) ?? throw new InvalidOperationException("附魔名称读取失败。"));
        int[] crafted = GameItemScheduler.ReadCraftedAbilities(item.UniqueId);
        bool equipped = GameItemScheduler.IsItemHeld(item.UniqueId) || GameItemScheduler.IsItemMounted(item.UniqueId);
        return new State(slots, limit, crafted, name, equipped);
    });

    private sealed record EditPlan(State Before, Dictionary<string,int> Allowed, HashSet<string> Selected, EquipmentEffect? Word, int Slots, bool NumericChecked);

    private static EditPlan PrepareEdit(InventoryItem item, string[] affixes, string enchantment, int slots, IReadOnlyDictionary<string,decimal>? numericTargets)
    {
        if(numericTargets is not null) GameEquipmentNumbers.ValidateTargets(item.Category,numericTargets);
        var before = Inspect(item);
        var allowed = Presets.Affixes.ToDictionary(effect => effect.Id, effect => GameItemScheduler.ResolveEquipmentAbility(effect.Id));
        if (affixes.Any(id => !allowed.ContainsKey(id))) throw new InvalidOperationException("词条不在游戏效果目录中。");
        var selected = affixes.Distinct().ToHashSet();
        foreach(var effect in Presets.Affixes.Where(effect=>selected.Contains(effect.Id) && !effect.AppliesTo(item.Category)))
            if(!before.Crafted.Contains(allowed[effect.Id]))
                throw new InvalidOperationException($"{effect.DisplayName}仅适用于{effect.TypeHint}，不能添加到当前装备。");
        EquipmentEffect? word = null;
        if (enchantment.Length != 0 && enchantment != before.Enchantment)
        {
            word = Presets.Enchantments.SingleOrDefault(effect => effect.Id == enchantment)
                ?? throw new InvalidOperationException("附魔不在游戏效果目录中。");
            bool compatible = word.Equipment == "剑" ? item.Category is "steelsword" or "silversword" : item.Category == "armor";
            if (!compatible) throw new InvalidOperationException("附魔不适用于当前装备。");
            GameItemScheduler.ResolveEquipmentAbility(word.Ability);
            slots = 3;
        }
        if (slots < before.Slots || slots > before.Limit) throw new InvalidOperationException($"槽位应在 {before.Slots}—{before.Limit} 范围内。");
        bool numericChecked=enchantment==before.Enchantment &&
            !allowed.Any(pair=>before.Crafted.Contains(pair.Value)!=selected.Contains(pair.Key));
        if(numericTargets is {Count:>0} && numericChecked)
            GameEquipmentNumbers.Check(item.UniqueId,item.Category,numericTargets);
        return new(before,allowed,selected,word,slots,numericChecked || numericTargets is not {Count:>0});
    }

    internal static bool CheckEdit(InventoryItem item,string[] affixes,string enchantment,int slots,IReadOnlyDictionary<string,decimal> numericTargets) =>
        GameItemScheduler.RunForItem(item,()=>PrepareEdit(item,affixes,enchantment,slots,numericTargets).NumericChecked);

    internal static State Edit(InventoryItem item, string[] affixes, string enchantment, int slots, IReadOnlyDictionary<string,decimal>? numericTargets = null) => GameItemScheduler.RunForItem(item, () =>
    {
        var plan=PrepareEdit(item,affixes,enchantment,slots,numericTargets);
        var before=plan.Before; var allowed=plan.Allowed; var selected=plan.Selected; var word=plan.Word; slots=plan.Slots;
        ErrorLog.Write("装备编辑计划",null,new {item.UniqueId,item.Name,BeforeSlots=before.Slots,TargetSlots=slots,Effects=affixes,Enchantment=enchantment,NumericTargets=numericTargets});
        int current = before.Slots;
        while (current < slots)
        {
            if (!GameItemScheduler.AddEquipmentSlot(item.UniqueId) || GameItemScheduler.ReadEquipmentSlots(item.UniqueId) != ++current)
                throw new InvalidOperationException("增加槽位回读失败，请重新读取装备。");
        }
        ErrorLog.Write("装备槽位确认",null,new {item.UniqueId,Slots=current});
        foreach (var pair in allowed)
        {
            bool present = before.Crafted.Contains(pair.Value);
            if (selected.Contains(pair.Key) && !present) GameItemScheduler.AddEquipmentAbility(item.UniqueId, pair.Key);
            else if (!selected.Contains(pair.Key) && present)
            {
                int copies = before.Crafted.Count(id => id == pair.Value);
                for (int i = 0; i < copies && GameItemScheduler.ReadCraftedAbilities(item.UniqueId).Contains(pair.Value); i++)
                    GameItemScheduler.RemoveEquipmentAbility(item.UniqueId, pair.Key);
            }
        }
        if (enchantment != before.Enchantment)
        {
            if (before.Enchantment.Length != 0) GameItemScheduler.ClearEnchantment(item.UniqueId);
            if (word is not null && !GameItemScheduler.EnchantEquipment(item.UniqueId, word.Id, word.Ability))
                throw new InvalidOperationException("附魔更换未通过回读，请重新读取装备。");
        }
        var after = Inspect(item);
        if (after.Enchantment != enchantment || after.Slots != slots ||
            allowed.Any(pair => after.Crafted.Contains(pair.Value) != selected.Contains(pair.Key)))
            throw new InvalidOperationException("装备编辑回读与选择不一致，请重新读取；部分修改可能已执行。");
        ErrorLog.Write("装备词条与附魔确认",null,new {item.UniqueId,Effects=affixes,Enchantment=enchantment,Slots=after.Slots});
        if(numericTargets is {Count:>0})
        {
            GameEquipmentNumbers.Apply(item.UniqueId,item.Category,numericTargets);
            return Inspect(item);
        }
        return after;
    });
    internal static int Create(string baseName, IEnumerable<string> requestedAffixes, string enchantment, int slots, IReadOnlyDictionary<string,decimal>? numericTargets = null) =>
        GameItemScheduler.RunSerialized(() =>
        {
            var item = GameItem.LoadCatalog().SingleOrDefault(item => item.Name == baseName);
            if (item is null || item.Category is not ("steelsword" or "silversword" or "armor" or "boots" or "pants" or "gloves" or "crossbow"))
                throw new InvalidOperationException("请选择目录中的基础武器或护甲。");
            ValidateConfiguration(item.Category,requestedAffixes,enchantment,slots,numericTargets ?? new Dictionary<string,decimal>());
            if (slots is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(slots));
            if(numericTargets is not null) GameEquipmentNumbers.ValidateTargets(item.Category,numericTargets);
            var selected = requestedAffixes.Distinct().ToArray();
            foreach (string id in selected)
            {
                var effect=Presets.Affixes.SingleOrDefault(effect=>effect.Id==id) ?? throw new InvalidOperationException("词条不在游戏效果目录中。");
                if(!effect.AppliesTo(item.Category)) throw new InvalidOperationException($"{effect.DisplayName}仅适用于{effect.TypeHint}，不能添加到当前装备。");
                GameItemScheduler.ResolveEquipmentAbility(id);
            }
            EquipmentEffect? word = null;
            if (enchantment.Length != 0)
            {
                word = Presets.Enchantments.SingleOrDefault(effect => effect.Id == enchantment)
                    ?? throw new InvalidOperationException("附魔不在游戏效果目录中。");
                bool compatible = word.Equipment == "剑" ? item.Category is "steelsword" or "silversword" : item.Category == "armor";
                if (!compatible) throw new InvalidOperationException("所选附魔不适用于此装备类型。");
                GameItemScheduler.ResolveEquipmentAbility(word.Ability);
                slots = 3;
            }
            int[] before = GameItemScheduler.ReadEquipmentIds(baseName);
            var added = GameItemScheduler.Give(baseName, 1);
            int[] created = GameItemScheduler.ReadEquipmentIds(baseName).Except(before).ToArray();
            if (added.Added != 1 || created.Length != 1)
                throw new InvalidOperationException("基础装备已请求生成，但没有确认唯一的新装备；请查看背包，避免重复添加。");
            int uniqueId = created[0];
            try
            {
                int current = GameItemScheduler.ReadEquipmentSlots(uniqueId);
                int limit = GameItemScheduler.ReadEquipmentSlotLimit(uniqueId);
                if (slots > limit) throw new InvalidOperationException($"该装备最多支持 {limit} 个槽位。");
                while (current < slots)
                {
                    if (!GameItemScheduler.AddEquipmentSlot(uniqueId)) throw new InvalidOperationException("游戏没有增加装备槽位。");
                    int next = GameItemScheduler.ReadEquipmentSlots(uniqueId);
                    if (next != current + 1) throw new InvalidOperationException("装备槽位回读不一致。");
                    current = next;
                }
                foreach (string id in selected)
                {
                    GameItemScheduler.AddEquipmentAbility(uniqueId, id);
                    if (!GameItemScheduler.ReadCraftedAbilities(uniqueId).Contains(GameItemScheduler.ResolveEquipmentAbility(id)))
                        throw new InvalidOperationException("附加词条回读未找到对应能力。");
                }
                if (word is not null && !GameItemScheduler.EnchantEquipment(uniqueId, word.Id, word.Ability))
                    throw new InvalidOperationException("附魔未通过游戏回读验证。");
                if(numericTargets is not null && numericTargets.Count!=0) GameEquipmentNumbers.Apply(uniqueId,item.Category,numericTargets);
                return uniqueId;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"基础装备已生成（ID {uniqueId}），后续配置未完成：{ex.Message} 请先检查背包。", ex);
            }
        });
}
