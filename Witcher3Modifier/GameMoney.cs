using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Witcher3Modifier;

internal static class GameMoney
{
    private static long GamePointerRva => GameVersion.Rva(0x5A750E8);
    private static long GameVtableRva => GameVersion.Rva(0x37CFCE0);
    private static long PlayerVtableRva => GameVersion.Rva(0x383A4D0);
    private static long InventoryVtableRva => GameVersion.Rva(0x384F4F0);
    private const int CrownsNameId = 51313;
    private static readonly object VersionLock = new();
    private static (int Id, long Started, string Path) verifiedProcess;
    public const int MaxMoney = 60000000;

    public static int Read() => WithMoneyAddress(false, (handle, address) => ReadInt32(handle, address));

    public static int Set(int target)
    {
        if (target < 0 || target > MaxMoney) throw new ArgumentOutOfRangeException(nameof(target));
        return WithMoneyAddress(true, (handle, address) =>
        {
            if (ReadInt32(handle, address + 4) != CrownsNameId)
                throw new InvalidOperationException("金钱条目已变化，请重新操作。");
            var data = BitConverter.GetBytes(target);
            if (!WriteProcessMemory(handle, (nint)address, data, data.Length, out var written) || written != data.Length)
                throw new InvalidOperationException("写入游戏金钱失败。");
            var actual = ReadInt32(handle, address);
            if (actual != target) throw new InvalidOperationException("游戏金钱回读与目标值不一致。");
            return actual;
        });
    }

    private static T WithMoneyAddress<T>(bool write, Func<nint, long, T> action) =>
        WithInventory(write, (handle, _, inventory, _) =>
        {
            long items = ReadInt64(handle, inventory + 0x140);
            int count = ReadInt32(handle, inventory + 0x148);
            if (count is < 1 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
            long moneyAddress = 0;
            for (int index = 0; index < count; index++)
            {
                long item = items + index * 0x98;
                if (ReadInt32(handle, item + 0x58) != CrownsNameId) continue;
                if (moneyAddress != 0) throw new InvalidOperationException("找到多条金钱记录，已停止操作。");
                moneyAddress = item + 0x54;
            }
            if (moneyAddress == 0) throw new InvalidOperationException("当前库存没有金钱记录。");
            return action(handle, moneyAddress);
        });

    internal static T WithInventory<T>(bool write, Func<nint, long, long, long, T> action, uint extraAccess = 0)
    {
        using var game = Process.GetProcessesByName("witcher3").SingleOrDefault()
            ?? throw new InvalidOperationException("未找到运行中的游戏。");
        var module = game.MainModule ?? throw new InvalidOperationException("无法读取游戏模块。");
        var identity = (game.Id, game.StartTime.ToUniversalTime().Ticks, module.FileName);
        lock (VersionLock)
        {
            if (verifiedProcess != identity)
            {
                using var stream = File.OpenRead(module.FileName);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                GameVersion.Select(hash);
                verifiedProcess = identity;
            }
        }

        long baseAddress = module.BaseAddress.ToInt64();
        uint access = 0x400 | 0x10 | (write ? 0x8u | 0x20u : 0u) | extraAccess;
        nint handle = OpenProcess(access, false, game.Id);
        if (handle == 0) throw new InvalidOperationException("无法连接游戏进程。");
        try
        {
            long gameObject,player,inventory;
            try
            {
                gameObject = ReadInt64(handle, baseAddress + GamePointerRva);
                CheckVtable(handle, gameObject, baseAddress + GameVtableRva, "游戏场景");
                long playerHandle = ReadInt64(handle, gameObject + 0xFD60);
                player = ReadInt64(handle, playerHandle + 8);
                CheckVtable(handle, player, baseAddress + PlayerVtableRva, "玩家");
                long inventoryHandle = ReadInt64(handle, player + 0x1B0);
                inventory = ReadInt64(handle, inventoryHandle + 8);
                CheckVtable(handle, inventory, baseAddress + InventoryVtableRva, "背包");
            }
            catch(InvalidOperationException ex) when(ex.Message=="读取游戏数据失败。")
            {
                throw new InvalidOperationException("游戏场景正在切换，请稍后重试。",ex);
            }

            return action(handle, gameObject, inventory, baseAddress);
        }
        finally { CloseHandle(handle); }
    }

    private static void CheckVtable(nint handle, long address, long expected, string subject)
    {
        if (address == 0 || ReadInt64(handle, address) != expected)
            throw new InvalidOperationException($"未能确认{subject}对象结构，本次操作未执行；请先载入存档。");
    }

    internal static long ReadInt64(nint handle, long address) => BitConverter.ToInt64(ReadBytes(handle, address, 8));
    internal static int ReadInt32(nint handle, long address) => BitConverter.ToInt32(ReadBytes(handle, address, 4));
    internal static float ReadSingle(nint handle, long address) => BitConverter.ToSingle(ReadBytes(handle, address, 4));

    internal static void WriteSingle(nint handle, long address, float value)
    {
        var data = BitConverter.GetBytes(value);
        if (!WriteProcessMemory(handle, (nint)address, data, data.Length, out var written) || written != data.Length)
            throw new InvalidOperationException("写入游戏装备耐久失败。");
        if (ReadSingle(handle, address) != value)
            throw new InvalidOperationException("游戏装备耐久回读与目标值不一致。");
    }

    internal static byte[] ReadBytes(nint handle, long address, int size)
    {
        var data = new byte[size];
        if (address == 0 || !ReadProcessMemory(handle, (nint)address, data, size, out var count) || count != size)
            throw new InvalidOperationException("读取游戏数据失败。");
        return data;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, int size, out nint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, int size, out nint written);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
