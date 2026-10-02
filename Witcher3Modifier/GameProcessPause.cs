using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Witcher3Modifier;

internal static class GameProcessPause
{
    // Check instruction pointers while all game threads are stopped, before replacing code.
    internal static void Run(nint process, Action action, params (long Address, int Length)[] entries)
    {
        if (NtSuspendProcess(process) != 0)
            throw new InvalidOperationException("无法暂停游戏线程以应用设置。");
        try
        {
            if (entries.Length != 0)
            {
                using var game = Process.GetProcessById((int)GetProcessId(process));
                nint allocation = Marshal.AllocHGlobal(1248);
                nint context = (nint)((allocation.ToInt64() + 15) & ~15L);
                try
                {
                    foreach (ProcessThread thread in game.Threads)
                    {
                        nint target = OpenThread(0x8, false, (uint)thread.Id);
                        if (target == 0) throw new InvalidOperationException("无法核对游戏线程状态，请再次应用。");
                        try
                        {
                            Marshal.Copy(new byte[1232], 0, context, 1232);
                            Marshal.WriteInt32(context, 48, 0x100001);
                            if (!GetThreadContext(target, context))
                                throw new InvalidOperationException("无法读取游戏线程状态，请再次应用。");
                            long ip = Marshal.ReadInt64(context, 248);
                            if (entries.Any(entry => ip >= entry.Address && ip < entry.Address + entry.Length))
                                throw new InvalidOperationException("游戏正在执行设置入口，请再次应用。");
                        }
                        finally { CloseHandle(target); }
                    }
                }
                finally { Marshal.FreeHGlobal(allocation); }
            }
            action();
        }
        finally
        {
            int result = NtResumeProcess(process);
            if (result != 0) throw new InvalidOperationException($"无法恢复游戏线程：0x{result:X8}");
        }
    }

    [DllImport("kernel32.dll")] private static extern uint GetProcessId(nint process);
    [DllImport("kernel32.dll")] private static extern nint OpenThread(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll")] private static extern bool GetThreadContext(nint thread, nint context);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(nint process);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(nint process);
}
