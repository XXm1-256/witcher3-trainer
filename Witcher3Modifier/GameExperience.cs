using System.Runtime.InteropServices;
using System.Text;

namespace Witcher3Modifier;

internal static class GameExperience
{
    private static long EntryRva => GameVersion.Rva(0x26F934E);
    private const int CodeOffset = 0x1000;
    private const long ConfigMarker = 0x1122334455667788;
    private const long ReturnMarker = unchecked((long)0x8877665544332211UL);
    private static readonly byte[] Original = Convert.FromHexString("488B8E880000008BBE900000004803F9");
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("W3MOD-XP-V500B\0");
    private static readonly byte[] Template = Convert.FromHexString(
        "9C5052415249BA8877665544332211493B32755141803F01754B418B470485C07E4341894218F049FF421049837A080A742F490FAF42084883C00541BA0A00000031D249F7F2483DFFFFFF3F7605B8FFFFFF3F4189470449BA88776655443322114189421C415A5A589D488B8E880000008BBE900000004803F9FF25000000001122334455667788");

    internal static decimal Read() => GameMoney.WithInventory(false, (handle, game, _, module) =>
    {
        long function = FindFunction(handle, game, module);
        long page = FindPage(handle, module);
        if (page == 0 || GameMoney.ReadInt64(handle, page) != function) return 1m;
        long tenths = GameMoney.ReadInt64(handle, page + 8);
        if (tenths is < 10 or > 10000) throw new InvalidOperationException("经验倍率数据异常。");
        return tenths / 10m;
    });

    internal static decimal Set(decimal multiplier) => GameMoney.WithInventory(true, (handle, game, _, module) =>
    {
        if (multiplier < 1 || multiplier > 1000 || multiplier != decimal.Round(multiplier, 1))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        long function = FindFunction(handle, game, module);
        long page = FindPage(handle, module);
        if (page == 0)
        {
            if (multiplier == 1) return 1m;
            page = Install(handle, module, function);
        }
        GameProcessPause.Run(handle, () =>
        {
            Write(handle, page, BitConverter.GetBytes(function));
            Write(handle, page + 8, BitConverter.GetBytes(decimal.ToInt64(multiplier * 10)));
        });
        if (GameMoney.ReadInt64(handle, page + 8) != decimal.ToInt64(multiplier * 10))
            throw new InvalidOperationException("经验倍率回读不一致。");
        return multiplier;
    }, 0x800);

    internal static string Diagnostics() => GameMoney.WithInventory(false, (handle, game, _, module) =>
    {
        long function = FindFunction(handle, game, module);
        long page = FindPage(handle, module);
        return page == 0 ? $"AddPoints 0x{function:X}; XP hook absent" :
            $"AddPoints 0x{function:X}; page 0x{page:X}; hits {GameMoney.ReadInt64(handle, page + 16)}; last {GameMoney.ReadInt32(handle, page + 24)} -> {GameMoney.ReadInt32(handle, page + 28)}";
    });

    private static long FindFunction(nint handle, long game, long module)
    {
        long player = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xFD60) + 8);
        long playerClass = GameMoney.ReadInt64(handle, player + 0x18);
        long property = FindNamed(handle, playerClass + 0x30, GameItemScheduler.ResolveName(handle,module,"levelManager"));
        long levelClass = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, property + 8) + 8);
        if (GameMoney.ReadInt32(handle, levelClass + 0x2C) != GameItemScheduler.ResolveName(handle,module,"W3LevelManager"))
            throw new InvalidOperationException("经验管理器类型不匹配。");
        long function = FindNamed(handle, levelClass + 0x48, GameItemScheduler.ResolveName(handle,module,"AddPoints"));
        if (GameMoney.ReadInt64(handle, function) != module + GameVersion.Rva(0x3A82D58) ||
            GameMoney.ReadInt64(handle, function + 8) != levelClass ||
            GameMoney.ReadInt32(handle, function + 0x18) != 0x10000 ||
            GameMoney.ReadInt32(handle, function + 0x30) != 3 ||
            GameMoney.ReadInt32(handle, function + 0x90) != 1559)
            throw new InvalidOperationException("经验函数结构不匹配。");
        long parameters = GameMoney.ReadInt64(handle, function + 0x28);
        int[] names = new[]{"type","amount","show"}.Select(name=>GameItemScheduler.ResolveName(handle,module,name)).ToArray();
        for (int i = 0; i < 3; i++)
        {
            long parameter = GameMoney.ReadInt64(handle, parameters + i * 8);
            if (GameMoney.ReadInt32(handle, parameter + 0x10) != names[i] ||
                GameMoney.ReadInt32(handle, parameter + 0x20) != i * 4)
                throw new InvalidOperationException("经验函数参数布局不匹配。");
        }
        return function;
    }

    private static long FindNamed(nint handle, long table, int name)
    {
        int count = GameMoney.ReadInt32(handle, table + 8);
        if (count is < 1 or > 2048) throw new InvalidOperationException("游戏函数表结构异常。");
        long array = GameMoney.ReadInt64(handle, table);
        long result = 0;
        for (int i = 0; i < count; i++)
        {
            long item = GameMoney.ReadInt64(handle, array + i * 8);
            if (GameMoney.ReadInt32(handle, item + 0x10) != name) continue;
            if (result != 0) throw new InvalidOperationException("游戏函数表存在重复条目。");
            result = item;
        }
        return result != 0 ? result : throw new InvalidOperationException("未找到游戏经验函数。");
    }

    private static long FindPage(nint handle, long module)
    {
        byte[] entry = GameMoney.ReadBytes(handle, module + EntryRva, Original.Length);
        if (entry.SequenceEqual(Original)) return 0;
        if (!entry.AsSpan(0, 6).SequenceEqual(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 }) ||
            entry[14] != 0x90 || entry[15] != 0x90)
            throw new InvalidOperationException("经验入口已被其他代码修改。");
        long page = BitConverter.ToInt64(entry, 6) - CodeOffset;
        if (page <= 0 || (page & 0xFFF) != 0 ||
            !GameMoney.ReadBytes(handle, page + 0x20, Magic.Length).SequenceEqual(Magic) ||
            !GameMoney.ReadBytes(handle, page + CodeOffset, Template.Length).SequenceEqual(BuildCode(page, module + EntryRva + Original.Length)))
            throw new InvalidOperationException("经验拦截代码校验失败。");
        return page;
    }

    private static byte[] BuildCode(long page, long target)
    {
        byte[] code = (byte[])Template.Clone();
        for (int i = 0; i <= code.Length - 8; i++)
        {
            long value = BitConverter.ToInt64(code, i);
            if (value == ConfigMarker) BitConverter.GetBytes(page).CopyTo(code, i);
            if (value == ReturnMarker) BitConverter.GetBytes(target).CopyTo(code, i);
        }
        return code;
    }

    private static long Install(nint handle, long module, long function)
    {
        nint allocation = VirtualAllocEx(handle, 0, 0x2000, 0x3000, 0x04);
        if (allocation == 0) throw new InvalidOperationException("无法准备经验倍率。");
        long page = allocation.ToInt64();
        bool safeToFree = true;
        try
        {
            Write(handle, page, BitConverter.GetBytes(function));
            Write(handle, page + 8, BitConverter.GetBytes(10L));
            Write(handle, page + 0x20, Magic);
            Write(handle, page + CodeOffset, BuildCode(page, module + EntryRva + Original.Length));
            if (!VirtualProtectEx(handle, (nint)(page + CodeOffset), 0x1000, 0x20, out _) ||
                !FlushInstructionCache(handle, (nint)(page + CodeOffset), 0x1000))
                throw new InvalidOperationException("无法准备经验运行时代码。");
            GameProcessPause.Run(handle, () =>
            {
                if (!GameMoney.ReadBytes(handle, module + EntryRva, Original.Length).SequenceEqual(Original))
                    throw new InvalidOperationException("经验入口在安装前已变化。");
                byte[] jump = new byte[16] { 0xFF, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x90, 0x90 };
                BitConverter.GetBytes(page + CodeOffset).CopyTo(jump, 6);
                safeToFree = false;
                try
                {
                    Patch(handle, module + EntryRva, jump);
                    if (FindPage(handle, module) != page) throw new InvalidOperationException("经验倍率安装校验失败。");
                }
                catch
                {
                    try
                    {
                        Patch(handle, module + EntryRva, Original);
                        safeToFree = GameMoney.ReadBytes(handle, module + EntryRva, Original.Length).SequenceEqual(Original);
                    }
                    catch { /* Keep the allocation while the entry may reference it. */ }
                    throw;
                }
            }, (module + EntryRva, Original.Length));
            return page;
        }
        finally { if (safeToFree) VirtualFreeEx(handle, allocation, 0, 0x8000); }
    }

    private static void Patch(nint handle, long address, byte[] bytes)
    {
        if (!VirtualProtectEx(handle, (nint)address, (nuint)bytes.Length, 0x40, out uint old))
            throw new InvalidOperationException("无法更新经验入口。");
        try
        {
            Write(handle, address, bytes);
            if (!FlushInstructionCache(handle, (nint)address, (nuint)bytes.Length))
                throw new InvalidOperationException("无法刷新经验入口。");
        }
        finally { VirtualProtectEx(handle, (nint)address, (nuint)bytes.Length, old, out _); }
    }

    private static void Write(nint handle, long address, byte[] bytes)
    {
        if (!WriteProcessMemory(handle, (nint)address, bytes, bytes.Length, out nint written) || written != bytes.Length)
            throw new InvalidOperationException("写入经验倍率失败。");
    }

    [DllImport("kernel32.dll")] private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtectEx(nint process, nint address, nuint size, uint protection, out uint old);
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
    [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(nint process, nint address, byte[] data, int size, out nint written);
}
