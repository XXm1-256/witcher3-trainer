using System.Text.Json;

namespace Witcher3Modifier;

public sealed class Draft
{
    public string Format { get; set; } = "witcher3-remastered-offline-draft";
    public int Version { get; set; } = 1;
    public bool ConnectedToGame { get; set; } = false;
    public List<ItemEntry> Items { get; set; } = [];
    public List<GearEntry> Gear { get; set; } = [];
    public int? Money { get; set; }
    public int? SkillPoints { get; set; }
    public decimal XpMultiplier { get; set; } = 1;
    public bool RuntimeSettingsSaved { get; set; }
    public bool SaveSwitchesAndMultipliers { get; set; } = true;
    public bool HotkeysEnabled { get; set; } = true;
    public decimal GoldMultiplier { get; set; } = 1;
    public decimal MoveSpeedMultiplier { get; set; } = 1.5m;
    public decimal JumpHeightMultiplier { get; set; } = 1.5m;
    public decimal SwimSpeedMultiplier { get; set; } = 1.5m;
    public decimal EnemySpeedMultiplier { get; set; } = .5m;
    public bool GoldMultiplierEnabled { get; set; }
    public bool XpMultiplierEnabled { get; set; }
    public bool KeepItems { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public bool WindowMaximized { get; set; }
    public Dictionary<string, bool> Toggles { get; set; } = [];
}

public sealed class ItemEntry
{
    public string SampleId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class GearEntry
{
    public string BaseItem { get; set; } = "";
    public string BaseType { get; set; } = "钢剑";
    public string Name { get; set; } = "";
    public int Damage { get; set; }
    public int RuneSlots { get; set; }
    public List<string> Affixes { get; set; } = [];
    public Dictionary<string,decimal> NumericTargets { get; set; } = [];
    public string Enchantment { get; set; } = "";
}

internal static class DraftStore
{
    public static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "draft.json");

    public static Draft Load()
    {
        if (!File.Exists(Path)) return new Draft();
        var draft = JsonSerializer.Deserialize<Draft>(File.ReadAllText(Path)) ?? new Draft();
        draft.Items ??= [];
        draft.Gear ??= [];
        draft.Toggles ??= [];
        return draft;
    }

    public static string ToJson(Draft draft) => JsonSerializer.Serialize(draft, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    public static void Save(Draft draft)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        string temporary = Path + ".tmp";
        File.WriteAllText(temporary, ToJson(draft));
        File.Move(temporary, Path, true);
    }
}
