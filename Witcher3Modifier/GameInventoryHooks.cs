using System.Runtime.InteropServices;
using System.Text;

namespace Witcher3Modifier;

internal static class GameInventoryHooks
{
    private static long AddRva => GameVersion.Rva(0x1F9D8F0);
    private static long RemoveRva => GameVersion.Rva(0x1F96070);
    private const int AddCodeOffset = 0x1100;
    private const int RemoveCodeOffset = 0x1200;
    private const long ConfigMarker = 0x1122334455667788;
    private const long AddReturnMarker = unchecked((long)0x8877665544332211UL);
    private const long RemoveReturnMarker = 0x7766554433221100;
    private static readonly byte[] AddOriginal = Convert.FromHexString("4C8944241848894C24085556574154");
    private static readonly byte[] RemoveOriginal = Convert.FromHexString("48895C2408555657415641574883EC30");
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("W3MOD-INV-V500B\0");
    private static readonly byte[] AddTemplate = Convert.FromHexString(
        "5052415249BA8877665544332211493B0A753441813871C80000752B418B0185C07E24490FAF42084883C00541BA0A00000031D249F7F2483D008793037605B800879303418901415A5A584C8944241848894C2408555657415448B81122334455667788FFE0");
    private static readonly byte[] LegacyRemoveTemplate = Convert.FromHexString(
        "504152415349BA8877665544332211493B0A754941807A100175424C8B99400100004D85DB74368B814801000085C07E2C3D10270000772541395360740D4981C398000000FFC875EFEB1241817B5871C800007408415B415A58B001C3415B415A5848895C2408555657415641574883EC3048B80011223344556677FFE0");
    private static readonly byte[] RemoveTemplate = Convert.FromHexString(
        "504152415349BA8877665544332211493B0A755941807A100175524C8B99400100004D85DB74468B814801000085C07E3C3D10270000773541395360740D4981C398000000FFC875EFEB2241817B5871C800007418418B4358413B4230740E413B42347408415B415A58B001C3415B415A5848895C2408555657415641574883EC3048B80011223344556677FFE0");

    internal static (decimal Gold, bool KeepItems) Read() =>
        GameMoney.WithInventory(false, (handle, _, inventory, module) =>
        {
            long page = FindPage(handle, module);
            if (page == 0) return (1m, false);
            long targetInventory = GameMoney.ReadInt64(handle, page);
            if (targetInventory != inventory)
                throw new InvalidOperationException("读档后库存对象已变化，请重新同步设置。");
            long tenths = GameMoney.ReadInt64(handle, page + 8);
            byte flag = GameMoney.ReadBytes(handle, page + 16, 1)[0];
            if (tenths is < 10 or > 10000 || flag > 1)
                throw new InvalidOperationException("游戏倍率设置结构异常。");
            return (tenths / 10m, flag == 1);
        });

    internal static void ValidateInstalledCode() => GameMoney.WithInventory(false, (handle, _, _, module) => FindPage(handle,module));

    internal static bool Rebind() =>
        GameMoney.WithInventory(true, (handle, game, inventory, module) =>
        {
            long page = FindPage(handle, module);
            if (page == 0) return false;
            EnsureDefaultBoltCleanup(handle, module, page);
            long tenths = GameMoney.ReadInt64(handle, page + 8);
            byte flag = GameMoney.ReadBytes(handle, page + 16, 1)[0];
            if (tenths is < 10 or > 10000 || flag > 1)
                throw new InvalidOperationException("游戏倍率设置结构异常。");
            bool changed = GameMoney.ReadInt64(handle, page) != inventory;
            if (changed)
            {
                Write(handle, page, BitConverter.GetBytes(inventory));
                if (GameMoney.ReadInt64(handle, page) != inventory)
                    throw new InvalidOperationException("游戏库存更新失败。");
            }
            if (flag == 1) GameItemScheduler.EnsureConsumables(handle, game, inventory, module, page);
            return changed;
        }, 0x800);

    internal static (decimal Gold, bool KeepItems) Set(decimal gold, bool keepItems) =>
        GameMoney.WithInventory(true, (handle, game, inventory, module) =>
        {
            if (gold < 1 || gold > 1000 || gold != decimal.Round(gold, 1))
                throw new ArgumentOutOfRangeException(nameof(gold));
            long tenths = decimal.ToInt64(gold * 10);
            long page = FindPage(handle, module);
            if (page == 0) page = Install(handle, inventory, module);
            EnsureDefaultBoltCleanup(handle, module, page);
            long previousTenths = GameMoney.ReadInt64(handle, page + 8);
            byte previousFlag = GameMoney.ReadBytes(handle, page + 16, 1)[0];
            Write(handle, page, BitConverter.GetBytes(inventory));
            try
            {
                Write(handle, page + 8, BitConverter.GetBytes(tenths));
                Write(handle, page + 16, [keepItems ? (byte)1 : (byte)0]);
                if (keepItems) GameItemScheduler.EnsureConsumables(handle, game, inventory, module, page);
                long actual = GameMoney.ReadInt64(handle, page + 8);
                byte flag = GameMoney.ReadBytes(handle, page + 16, 1)[0];
                if (actual != tenths || flag != (keepItems ? 1 : 0))
                    throw new InvalidOperationException("游戏倍率设置回读不一致。");
                return (actual / 10m, flag == 1);
            }
            catch
            {
                Write(handle, page + 8, BitConverter.GetBytes(previousTenths));
                Write(handle, page + 16, [previousFlag]);
                throw;
            }
        }, 0x800);

    private static long FindPage(nint handle, long module)
    {
        byte[] add = GameMoney.ReadBytes(handle, module + AddRva, AddOriginal.Length);
        byte[] remove = GameMoney.ReadBytes(handle, module + RemoveRva, RemoveOriginal.Length);
        if (add.SequenceEqual(AddOriginal) && remove.SequenceEqual(RemoveOriginal)) return 0;
        if (!TryPatchTarget(add, AddCodeOffset, out long page) ||
            !TryPatchTarget(remove, RemoveCodeOffset, out long otherPage) || page != otherPage)
            throw new InvalidOperationException("游戏库存入口已被其他代码修改，已停止操作。");
        if (!GameMoney.ReadBytes(handle, page + 0x20, Magic.Length).SequenceEqual(Magic))
            throw new InvalidOperationException("游戏库存拦截标记不匹配。");
        if (!GameMoney.ReadBytes(handle, page + AddCodeOffset, AddTemplate.Length)
                .SequenceEqual(BuildCode(AddTemplate, page, module + AddRva + AddOriginal.Length, AddReturnMarker)) ||
            (!GameMoney.ReadBytes(handle, page + RemoveCodeOffset, RemoveTemplate.Length)
                .SequenceEqual(BuildCode(RemoveTemplate, page, module + RemoveRva + RemoveOriginal.Length, RemoveReturnMarker)) &&
             !GameMoney.ReadBytes(handle, page + RemoveCodeOffset, LegacyRemoveTemplate.Length)
                .SequenceEqual(BuildCode(LegacyRemoveTemplate, page, module + RemoveRva + RemoveOriginal.Length, RemoveReturnMarker))))
            throw new InvalidOperationException("游戏库存拦截代码校验失败。");
        return page;
    }

    private static void EnsureDefaultBoltCleanup(nint handle, long module, long page)
    {
        int bodkin = GameItemScheduler.ResolveName(handle, module, "Bodkin Bolt");
        int harpoon = GameItemScheduler.ResolveName(handle, module, "Harpoon Bolt");
        byte[] ids = [..BitConverter.GetBytes(bodkin), ..BitConverter.GetBytes(harpoon)];
        byte[] code = BuildCode(RemoveTemplate, page, module + RemoveRva + RemoveOriginal.Length, RemoveReturnMarker);
        if (GameMoney.ReadBytes(handle, page + 0x30, 8).SequenceEqual(ids) &&
            GameMoney.ReadBytes(handle, page + RemoveCodeOffset, code.Length).SequenceEqual(code)) return;
        GameProcessPause.Run(handle, () =>
        {
            if (FindPage(handle, module) != page) throw new InvalidOperationException("库存入口在更新前已变化。");
            byte[] previous = GameMoney.ReadBytes(handle, page + RemoveCodeOffset, code.Length);
            byte[] previousIds = GameMoney.ReadBytes(handle, page + 0x30, 8);
            try
            {
                Write(handle, page + 0x30, ids);
                PatchEntry(handle, page + RemoveCodeOffset, code);
                if (FindPage(handle, module) != page || !GameMoney.ReadBytes(handle, page + 0x30, 8).SequenceEqual(ids))
                    throw new InvalidOperationException("默认弩箭清理更新回读失败。");
                ErrorLog.Write("默认弩箭清理更新",null,new {Bodkin= bodkin,Harpoon= harpoon,CodeBytes=code.Length});
            }
            catch
            {
                PatchEntry(handle, page + RemoveCodeOffset, previous);
                Write(handle, page + 0x30, previousIds);
                throw;
            }
        }, (module + RemoveRva, RemoveOriginal.Length), (page + RemoveCodeOffset, code.Length));
    }

    private static bool TryPatchTarget(byte[] code, int offset, out long page)
    {
        page = 0;
        if (code.Length < 14 || !code.AsSpan(0, 6).SequenceEqual(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 }) ||
            code.AsSpan(14).ToArray().Any(value => value != 0x90)) return false;
        long address = BitConverter.ToInt64(code, 6);
        page = address - offset;
        return page > 0 && (page & 0xFFF) == 0;
    }

    private static long Install(nint handle, long inventory, long module)
    {
        nint remote = VirtualAllocEx(handle, 0, 0x2000, 0x3000, 0x04);
        if (remote == 0) throw new InvalidOperationException("无法准备游戏运行时拦截。");
        long page = remote.ToInt64();
        bool safeToFree = true;
        try
        {
            byte[] data = new byte[0x2000];
            BitConverter.GetBytes(inventory).CopyTo(data, 0);
            BitConverter.GetBytes(10L).CopyTo(data, 8);
            Magic.CopyTo(data, 0x20);
            BuildCode(AddTemplate, page, module + AddRva + AddOriginal.Length, AddReturnMarker).CopyTo(data, AddCodeOffset);
            BuildCode(RemoveTemplate, page, module + RemoveRva + RemoveOriginal.Length, RemoveReturnMarker).CopyTo(data, RemoveCodeOffset);
            Write(handle, page, data);
            if (!VirtualProtectEx(handle, (nint)(page + 0x1000), 0x1000, 0x20, out _))
                throw new InvalidOperationException("无法保护游戏运行时拦截代码。");
            if (!FlushInstructionCache(handle, (nint)(page + 0x1000), 0x1000))
                throw new InvalidOperationException("无法刷新游戏库存拦截代码缓存。");

            GameProcessPause.Run(handle, () =>
            {
                if (!GameMoney.ReadBytes(handle, module + AddRva, AddOriginal.Length).SequenceEqual(AddOriginal) ||
                    !GameMoney.ReadBytes(handle, module + RemoveRva, RemoveOriginal.Length).SequenceEqual(RemoveOriginal))
                    throw new InvalidOperationException("游戏库存入口在安装前已变化。");
                safeToFree = false;
                try
                {
                    PatchEntry(handle, module + AddRva, MakeJump(page + AddCodeOffset, AddOriginal.Length));
                    PatchEntry(handle, module + RemoveRva, MakeJump(page + RemoveCodeOffset, RemoveOriginal.Length));
                    if (FindPage(handle, module) != page)
                        throw new InvalidOperationException("游戏库存拦截安装后校验失败。");
                }
                catch
                {
                    try
                    {
                        PatchEntry(handle, module + RemoveRva, RemoveOriginal);
                        PatchEntry(handle, module + AddRva, AddOriginal);
                        safeToFree = GameMoney.ReadBytes(handle, module + AddRva, AddOriginal.Length).SequenceEqual(AddOriginal) &&
                            GameMoney.ReadBytes(handle, module + RemoveRva, RemoveOriginal.Length).SequenceEqual(RemoveOriginal);
                    }
                    catch { /* Keep the trampoline allocated if either entry might still point to it. */ }
                    throw;
                }
            }, (module + AddRva, AddOriginal.Length), (module + RemoveRva, RemoveOriginal.Length));
            return page;
        }
        finally
        {
            if (safeToFree) VirtualFreeEx(handle, remote, 0, 0x8000);
        }
    }

    private static byte[] BuildCode(byte[] template, long page, long returnAddress, long returnMarker)
    {
        byte[] code = (byte[])template.Clone();
        int configAt = FindMarker(code, ConfigMarker);
        int returnAt = FindMarker(code, returnMarker);
        BitConverter.GetBytes(page).CopyTo(code, configAt);
        BitConverter.GetBytes(returnAddress).CopyTo(code, returnAt);
        return code;
    }

    private static int FindMarker(byte[] code, long marker)
    {
        byte[] token = BitConverter.GetBytes(marker);
        for (int i = 0; i <= code.Length - token.Length; i++)
            if (code.AsSpan(i, token.Length).SequenceEqual(token)) return i;
        throw new InvalidOperationException("运行时代码重定位标记缺失。");
    }

    private static byte[] MakeJump(long target, int length)
    {
        var bytes = Enumerable.Repeat((byte)0x90, length).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0x25;
        bytes.AsSpan(2, 4).Clear();
        BitConverter.GetBytes(target).CopyTo(bytes, 6);
        return bytes;
    }

    private static void PatchEntry(nint handle, long address, byte[] data)
    {
        if (!VirtualProtectEx(handle, (nint)address, (nuint)data.Length, 0x40, out uint previous))
            throw new InvalidOperationException("无法解锁游戏库存入口。");
        try
        {
            Write(handle, address, data);
            if (!FlushInstructionCache(handle, (nint)address, (nuint)data.Length))
                throw new InvalidOperationException("无法刷新游戏指令缓存。");
        }
        finally { VirtualProtectEx(handle, (nint)address, (nuint)data.Length, previous, out _); }
    }

    private static void Write(nint handle, long address, byte[] data)
    {
        if (!WriteProcessMemory(handle, (nint)address, data, data.Length, out nint written) || written != data.Length)
            throw new InvalidOperationException("写入游戏运行时数据失败。");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint type);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtectEx(nint process, nint address, nuint size, uint protection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, int size, out nint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
}
