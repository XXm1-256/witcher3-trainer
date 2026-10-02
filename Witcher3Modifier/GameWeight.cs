using System.Runtime.InteropServices;
using System.Text;

namespace Witcher3Modifier;

internal static class GameWeight
{
    private static long ConfigManagerRva => GameVersion.Rva(0x5CC5440);
    private static long SetConfigValueRva => GameVersion.Rva(0x271CDA0);
    private const string Group = "Accessibility";
    private const string Key = "WeightlessItems";

    public static bool Read() => GameMoney.WithInventory(false, (handle, _, _, module) =>
        ReadValue(handle, module) == "true");

    public static bool Set(bool enabled) => GameMoney.WithInventory(true, (handle, _, _, module) =>
    {
        string target = enabled ? "true" : "false";
        if (ReadValue(handle, module) == target) return enabled;
        long manager = GameMoney.ReadInt64(handle, module + ConfigManagerRva);
        long layer = GameMoney.ReadInt64(handle, manager + 0x20);
        if (layer == 0 || GameMoney.ReadInt32(handle, layer + 8) is < 1 or > 1000)
            throw new InvalidOperationException("游戏配置结构异常。");

        nint remote = VirtualAllocEx(handle, 0, 0x1000, 0x3000, 0x40);
        if (remote == 0) throw new InvalidOperationException("无法准备游戏运行时设置。");
        nint thread = 0;
        bool finished = false;
        try
        {
            long start = remote.ToInt64();
            var data = new byte[0x100];
            WriteAscii(data, 0x80, Group);
            WriteAscii(data, 0xA0, Key);
            WriteAscii(data, 0xC0, target);
            BitConverter.GetBytes(start + 0xC0).CopyTo(data, 0xD0);
            BitConverter.GetBytes(target.Length + 1).CopyTo(data, 0xD8);

            var code = new List<byte>();
            code.AddRange([0x48, 0x83, 0xEC, 0x28]);
            Mov64(code, [0x48, 0xB9], layer);
            Mov64(code, [0x48, 0xBA], start + 0x80);
            Mov64(code, [0x49, 0xB8], start + 0xA0);
            Mov64(code, [0x49, 0xB9], start + 0xD0);
            Mov64(code, [0x48, 0xB8], module + SetConfigValueRva);
            code.AddRange([0xFF, 0xD0, 0x48, 0x83, 0xC4, 0x28, 0x31, 0xC0, 0xC3]);
            code.CopyTo(data);
            if (!WriteProcessMemory(handle, remote, data, data.Length, out var written) || written != data.Length)
                throw new InvalidOperationException("无法写入游戏运行时设置指令。");

            thread = CreateRemoteThread(handle, 0, 0, remote, 0, 0, out _);
            if (thread == 0) throw new InvalidOperationException("无法执行游戏运行时设置。");
            uint wait = WaitForSingleObject(thread, 5000);
            if (wait != 0) throw new InvalidOperationException("游戏运行时设置未在限定时间内完成。");
            finished = true;
            if (!GetExitCodeThread(thread, out uint exitCode) || exitCode != 0)
                throw new InvalidOperationException("游戏运行时设置执行失败。");
            if (ReadValue(handle, module) != target)
                throw new InvalidOperationException("游戏运行时设置回读与目标值不一致。");
            return enabled;
        }
        finally
        {
            if (thread != 0) CloseHandle(thread);
            if (finished || thread == 0) VirtualFreeEx(handle, remote, 0, 0x8000);
        }
    }, 0x2);

    private static string? ReadValue(nint handle, long module)
    {
        long manager = GameMoney.ReadInt64(handle, module + ConfigManagerRva);
        if (manager == 0) throw new InvalidOperationException("游戏配置管理器不存在。");
        long layer = GameMoney.ReadInt64(handle, manager + 0x20);
        if (layer == 0) throw new InvalidOperationException("游戏配置层不存在。");
        int groups = GameMoney.ReadInt32(handle, layer + 8);
        if (groups is < 1 or > 1000) throw new InvalidOperationException("游戏配置分组异常。");
        long groupArray = GameMoney.ReadInt64(handle, layer);
        for (int i = 0; i < groups; i++)
        {
            long entry = groupArray + i * 16L;
            if (GameMoney.ReadInt32(handle, entry) != Hash(Group)) continue;
            long group = GameMoney.ReadInt64(handle, entry + 8);
            int count = GameMoney.ReadInt32(handle, group + 0x14);
            if (count is < 0 or > 1000) throw new InvalidOperationException("游戏配置项异常。");
            long vars = GameMoney.ReadInt64(handle, group + 0xC);
            for (int j = 0; j < count; j++)
            {
                long variable = vars + j * 0x1CL;
                if (GameMoney.ReadInt32(handle, variable) != Hash(Key)) continue;
                long value = GameMoney.ReadInt64(handle, variable + 0x10);
                int length = GameMoney.ReadInt32(handle, variable + 0x18);
                if (length is < 1 or > 32) throw new InvalidOperationException("游戏负重设置值异常。");
                var data = GameMoney.ReadBytes(handle, value, length);
                if (data[^1] != 0) throw new InvalidOperationException("游戏负重设置格式异常。");
                return Encoding.ASCII.GetString(data, 0, length - 1);
            }
        }
        return null;
    }

    private static int Hash(string value)
    {
        uint hash = 0x811C9DC5;
        foreach (byte part in Encoding.ASCII.GetBytes(value + "\0"))
            hash = (hash ^ part) * 0x1000193;
        return unchecked((int)hash);
    }

    private static void WriteAscii(byte[] buffer, int offset, string value) =>
        Encoding.ASCII.GetBytes(value + "\0").CopyTo(buffer, offset);

    private static void Mov64(List<byte> code, byte[] opcode, long value)
    {
        code.AddRange(opcode);
        code.AddRange(BitConverter.GetBytes(value));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint type);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, int size, out nint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateRemoteThread(nint process, nint attributes, nuint stackSize, nint start, nint parameter, uint flags, out uint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(nint thread, out uint code);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
