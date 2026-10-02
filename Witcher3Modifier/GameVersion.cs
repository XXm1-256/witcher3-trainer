namespace Witcher3Modifier;

internal static class GameVersion
{
    internal static bool Current500c { get; private set; }
    // The hash selects an address profile; individual operations validate the live structure and code.
    internal static void Select(string hash) => Current500c = hash == "9406ECCC12B68E08920931442EF6A57340E910D3E01F2082E88232487433FE51";
    private static readonly Dictionary<long,long> Addresses500c = new()
    {
        [0x2050950] = 0x204C140,
        [0x3AD3728] = 0x3AACDF8,
        [0xBCB420] = 0xBCC930,
        [0x1D781C0] = 0x1D73DA0,
        [0x1D78540] = 0x1D74120,
        [0x1E94320] = 0x1E90350,
        [0x1F5C4E0] = 0x1F57CF0,
        [0x1F5CEF0] = 0x1F58700,
        [0x1F82BE0] = 0x1F7E4A0,
        [0x1F83AA0] = 0x1F7F360,
        [0x1F83C40] = 0x1F7F500,
        [0x1F870E0] = 0x1F829A0,
        [0x1F871D0] = 0x1F82A90,
        [0x1F879E0] = 0x1F832A0,
        [0x1F87B60] = 0x1F83420,
        [0x1F87D40] = 0x1F83600,
        [0x1F884F0] = 0x1F83DB0,
        [0x1F8C1E0] = 0x1F87AA0,
        [0x1F8C2D0] = 0x1F87B90,
        [0x1F96070] = 0x1F91950,
        [0x1F9D8F0] = 0x1F991D0,
        [0x23A59B0] = 0x23A0F20,
        [0x23A5C40] = 0x23A11B0,
        [0x1D74080] = 0x1D74080,
        [0x1F85150] = 0x1F85150,
        [0x2393610] = 0x2393610,
        [0x22715C0] = 0x22715C0,
        [0x2271510] = 0x2271510,
        [0x2271860] = 0x2271860,
        [0x22717F0] = 0x22717F0,
        [0x22719B0] = 0x22719B0,
        [0x1CF7070] = 0x1CF7070,
        [0x1CF6F80] = 0x1CF6F80,
        [0x1CF6E30] = 0x1CF6E30,
        [0x1CF6B90] = 0x1CF6B90,
        [0x1CF6AA0] = 0x1CF6AA0,
        [0x1CF67F0] = 0x1CF67F0,
        [0x20FB390] = 0x20FB390,
        [0x20FB490] = 0x20FB490,
        [0x26F5580] = 0x26F5580,
        [0x26FC970] = 0x26FC970,
        [0x26F934E] = 0x26F46CE,
        [0x26F93D2] = 0x26F4752,
        [0x26F94A5] = 0x26F4825,
        [0x271CDA0] = 0x27180C0,
        [0x2776C50] = 0x27720C0,
        [0x37CFCE0] = 0x37A9500,
        [0x37D0A40] = 0x37AA230,
        [0x381E000] = 0x37F7810,
        [0x38232C8] = 0x37FCAA8,
        [0x383A4D0] = 0x3813C70,
        [0x3843FC8] = 0x381D798,
        [0x384F4F0] = 0x3828CA0,
        [0x3850698] = 0x3829E50,
        [0x38B8358] = 0x3891B28,
        [0x3A82D58] = 0x3A5C490,
        [0x5874E38] = 0x584DDD8,
        [0x5A750E8] = 0x5A4E0A8,
        [0x5A75F00] = 0x5A4EEC0,
        [0x5A92BC8] = 0x5A6BB88,
        [0x5C97300] = 0x5C702A0,
        [0x5C97790] = 0x5C70730,
        [0x5CC5148] = 0x5C9E108,
        [0x5CC5440] = 0x5C9E408,
    };
    internal static long Rva(long reference)
    {
        if (!Current500c) return reference;
        return Addresses500c.TryGetValue(reference,out long actual) ? actual
            : throw new InvalidOperationException($"该功能的游戏入口 0x{reference:X} 尚未完成定位，本次操作未执行。");
    }
}
