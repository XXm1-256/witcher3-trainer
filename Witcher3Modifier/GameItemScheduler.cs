using System.Runtime.InteropServices;
using System.Text;

namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private static readonly object RequestLock = new();
    [ThreadStatic] private static InventoryItem? selectedTarget;
    internal static T RunSerialized<T>(Func<T> action) { lock (RequestLock) return action(); }
    internal static T RunForItem<T>(InventoryItem item, Func<T> action)
    {
        lock (RequestLock)
        {
            var previous = selectedTarget;
            selectedTarget = item;
            try { return action(); }
            finally { selectedTarget = previous; }
        }
    }
    private static long EntryRva => GameVersion.Rva(0x26F93D2);
    private static long AddCoreRva => GameVersion.Rva(0x1F9D8F0);
    private static long FreeRva => GameVersion.Rva(0xBCB420);
    private const int CodeOffset = 0x1000;
    private const int PolicyOffset = 0x1300;
    private const int RemoveJobOffset = 0x1800;
    private const int RemoveCoreOffset = 0x1880;
    private const int TeleportJobOffset = 0x1A00;
    private static readonly byte[] RemoveOriginal = Convert.FromHexString("48895C2408555657415641574883EC30");
    private static readonly byte[] RemoveJobTemplate = Convert.FromHexString("534883EC204C89C3488B42308B5001448B400648B88877665544332211FFD088034883C4205BC3");
    private static readonly byte[] LegacyEntryOriginal = Convert.FromHexString("483BCF0F83CA0000004C8D251E6C90FD");
    private static readonly byte[] EntryOriginal500c = Convert.FromHexString("483BCF0F83CA0000004C8D259EB890FD");
    private static byte[] EntryOriginal => GameVersion.Current500c ? EntryOriginal500c : LegacyEntryOriginal;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("W3MOD-ITEM-V500B\0");
    private static readonly byte[] Template = Convert.FromHexString(
        "9C505152415041514152415349BA8877665544332211493B320F85180100004D3B6A080F850E010000F049FF422041807A10010F85FE00000041C64210024881EC800000000F294424200F294C24300F295424400F295C24500F296424600F296C2470498B8560FD00004885C00F8488000000488B40084885C0747F488B88B00100004885C97473488B49084885C9746A49C742300000000049C7423800000000498D52304D8D42144D8D4A1848B89988776655443322FFD049BA8877665544332211498B4230418B52384189522C85D27E0A41C7422801000000EB0841C74228020000004885C074194889C148B8AA99887766554433FFD0EB0841C74228030000000F284424200F284C24300F285424400F285C24500F286424600F286C24704881C48000000049BA887766554433221141C6421000415B415A415941585A59589D" +
        "483BCF731849BCBBAA998877665544FF2500000000CCBBAA9988776655FF2500000000DDCCBBAA99887766");
    private static readonly byte[] LegacyPolicyTemplate = Convert.FromHexString(
        "9C5051524152415349BA8877665544332211493B727075274D8B9A800000004D85DB741B41807B100175144D3B2B750F41C70700000000F049FF8288000000415B415A5A59589DFF2500000000CCBBAA9988776655");
    private static readonly byte[] PolicyTemplate = Convert.FromHexString(
        "9C505152415041514152415349BA8877665544332211493B320F85A60000004D3B6A080F859C0000004180BA90000000010F858E00000041C68290000000024881EC800000000F294424200F294C24300F295424400F295C24500F296424600F296C2470498B8AA0000000498D92000200004D8D82800200004531C941FF929800000049BA887766554433221141C782940000000100000041C68290000000000F284424200F284C24300F285424400F285C24500F286424600F286C24704881C480000000493B727075274D8B9A800000004D85DB741B41807B100175144D3B2B750F41C70700000000F049FF8288000000415B415A415941585A59589DFF2500000000CCBBAA9988776655");

    internal static int ReadEquipmentSlots(int uniqueId) => BitConverter.ToInt32(
        ExecuteEquipmentNative(0x1F884F0, [4, .. BitConverter.GetBytes(uniqueId), 0x20]));

    internal static int ReadEquipmentSlotLimit(int uniqueId) => BitConverter.ToInt32(
        ExecuteEquipmentNative(0x1F8C1E0, [4, .. BitConverter.GetBytes(uniqueId), 0x20]));

    internal static bool AddEquipmentSlot(int uniqueId) =>
        ExecuteEquipmentNative(0x1F8C2D0, [4, .. BitConverter.GetBytes(uniqueId), 0x20])[0] != 0;

    internal static int ReadEnchantment(int uniqueId) => BitConverter.ToInt32(
        ExecuteEquipmentNative(0x1F87D40, [4, .. BitConverter.GetBytes(uniqueId), 0x20]));

    internal static bool IsItemHeld(int uniqueId) => ExecuteEquipmentNative(0x1F870E0, [4, .. BitConverter.GetBytes(uniqueId), 0x20])[0] != 0;
    internal static bool IsItemMounted(int uniqueId) => ExecuteEquipmentNative(0x1F871D0, [4, .. BitConverter.GetBytes(uniqueId), 0x20])[0] != 0;
    internal static bool RemoveInventoryItem(int uniqueId, int quantity) =>
        ExecuteEquipmentNative(0, [4, .. BitConverter.GetBytes(uniqueId), 4, .. BitConverter.GetBytes(quantity), 0x20], true)[0] != 0;
    internal static void RemoveEquipmentAbility(int uniqueId, string ability)
    {
        int id = ResolveEquipmentAbility(ability);
        ExecuteEquipmentNative(0x1F83AA0, [4, .. BitConverter.GetBytes(uniqueId), 4, .. BitConverter.GetBytes(id), 0x20]);
    }
    internal static void ClearEnchantment(int uniqueId) =>
        ExecuteEquipmentNative(0x1F879E0, [4, .. BitConverter.GetBytes(uniqueId), 0x20]);

    internal static int ResolveEquipmentAbility(string name) => GameMoney.WithInventory(false, (handle, game, _, module) =>
    {
        int id = ResolveName(handle, module, name);
        long definitions = GameMoney.ReadInt64(handle, game + 0xFD68);
        int count = GameMoney.ReadInt32(handle, definitions + 0x60);
        if (count is < 1 or > 100000) throw new InvalidOperationException("游戏能力定义表结构异常。");
        long buckets = GameMoney.ReadInt64(handle, definitions + 0x80);
        long node = GameMoney.ReadInt64(handle, buckets + (uint)id % (uint)count * 8);
        for (int index = 0; node != 0 && index < 10000; index++)
        {
            if (GameMoney.ReadInt32(handle, node) == id && GameMoney.ReadInt32(handle, node + 0x60) == id) return id;
            node = GameMoney.ReadInt64(handle, node + 0x68);
        }
        throw new InvalidOperationException("当前游戏未载入选中的装备能力。");
    });

    internal static void AddEquipmentAbility(int uniqueId, string ability, bool allowDuplicate = false)
    {
        int id = ResolveEquipmentAbility(ability);
        ExecuteEquipmentNative(0x1F83C40,
            [4, .. BitConverter.GetBytes(uniqueId), 4, .. BitConverter.GetBytes(id), 9, allowDuplicate ? (byte)1 : (byte)0, 0x20]);
    }

    internal static bool EnchantEquipment(int uniqueId, string name, string ability)
    {
        int nameId = GameMoney.WithInventory(false, (handle, _, _, module) => ResolveName(handle, module, name));
        int abilityId = ResolveEquipmentAbility(ability);
        bool result = ExecuteEquipmentNative(0x1F87B60,
            [4, .. BitConverter.GetBytes(uniqueId), 4, .. BitConverter.GetBytes(nameId),
                4, .. BitConverter.GetBytes(abilityId), 0x20])[0] != 0;
        return result && ReadEnchantment(uniqueId) == nameId;
    }

    internal static int[] ReadEquipmentIds(string name) => GameMoney.WithInventory(false, (handle, game, inventory, module) =>
    {
        int nameId = ResolveItem(handle, game, module, name);
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (count is < 0 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
        long items = GameMoney.ReadInt64(handle, inventory + 0x140);
        var ids = new List<int>();
        for (int index = 0; index < count; index++)
        {
            long entry = items + index * 0x98;
            if (GameMoney.ReadInt32(handle, entry + 0x58) == nameId) ids.Add(GameMoney.ReadInt32(handle, entry + 0x60));
        }
        return ids.ToArray();
    });

    internal static int[] ReadCraftedAbilities(int uniqueId) => GameMoney.WithInventory(false, (handle, _, inventory, _) =>
    {
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (count is < 0 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
        long items = GameMoney.ReadInt64(handle, inventory + 0x140);
        for (int index = 0; index < count; index++)
        {
            long entry = items + index * 0x98;
            if (GameMoney.ReadInt32(handle, entry + 0x60) != uniqueId) continue;
            int abilities = GameMoney.ReadInt32(handle, entry + 0x20);
            if (abilities is < 0 or > 1000) throw new InvalidOperationException("装备词条结构异常。");
            long array = GameMoney.ReadInt64(handle, entry + 0x18);
            return Enumerable.Range(0, abilities).Select(i => GameMoney.ReadInt32(handle, array + i * 4)).ToArray();
        }
        throw new InvalidOperationException("选中的装备已不在当前背包中。");
    });

    internal sealed record AttributeValue(float Additive, float Multiplicative, float Base);
    internal static float ReadEquipmentAttribute(int uniqueId, string attribute)
    {
        var value = ReadEquipmentAttributeParts(uniqueId, attribute);
        return (value.Additive + value.Base) * (1 + value.Multiplicative);
    }

    internal static AttributeValue ReadEquipmentAttributeParts(int uniqueId, string attribute, bool fast = false)
    {
        int nameId = GameMoney.WithInventory(false, (handle, _, _, module) => ResolveName(handle, module, attribute));
        byte[] result = ExecuteEquipmentNative(0x1F82BE0,
            [4, .. BitConverter.GetBytes(uniqueId), 4, .. BitConverter.GetBytes(nameId), 0, 9, 0, 0x20], fastPoll: fast);
        return new AttributeValue(BitConverter.ToSingle(result, 0), BitConverter.ToSingle(result, 4), BitConverter.ToSingle(result, 8));
    }

    private static T WithItemRequest<T>(Func<nint, long, long, long, T> action)
    {
        lock (RequestLock) return GameMoney.WithInventory(true, action, 0x800);
    }

    internal static byte[] ExecuteTeleportJob(byte[] code, byte[] arguments,IProgress<string>? progress=null) => ExecuteEquipmentNative(0, arguments, teleportCode: code,progress:progress);

    internal static byte[] ExecuteDebugFact(long nativeRva,byte[] arguments)
    {
        if(nativeRva is not (0x1F5CEF0 or 0x1F5C4E0)) throw new ArgumentOutOfRangeException(nameof(nativeRva));
        return ExecuteEquipmentNative(nativeRva,arguments);
    }

    private static void ValidateDebugFactEntry(nint handle,long module,long nativeRva)
    {
        byte[] prefix=Convert.FromHexString(nativeRva==0x1F5CEF0
            ? "48895C2408488974241855574156488BEC4881EC80000000"
            : "40534883EC30488BDA");
        if(!GameMoney.ReadBytes(handle,module+GameVersion.Rva(nativeRva),prefix.Length).SequenceEqual(prefix))
            throw new InvalidOperationException("游戏内置开关入口未通过代码检查，本次操作未执行。");
    }

    internal static bool ValidateDebugFacts() => GameMoney.WithInventory(false,(handle,_,_,module)=>
    {
        ValidateDebugFactEntry(handle,module,0x1F5CEF0);
        ValidateDebugFactEntry(handle,module,0x1F5C4E0);
        return true;
    });

    private static byte[] ExecuteEquipmentNative(long nativeRva, byte[] arguments, bool remove = false, byte[]? teleportCode = null,long? contextOverride=null,IProgress<string>? progress=null, bool gwent=false, bool fastPoll=false,bool respec=false,bool actorScript=false) => WithItemRequest((handle, game, inventory, module) =>
    {
        if(arguments.Length>0x500) throw new InvalidOperationException("原生请求参数过长。");
        if(nativeRva is 0x1F5CEF0 or 0x1F5C4E0) ValidateDebugFactEntry(handle,module,nativeRva);
        long function = FindTick(handle, game, module);
        long page = FindPage(handle, module);
        if (page == 0) page = Install(handle, module, game, function);
        else if (!GameMoney.ReadBytes(handle, page + PolicyOffset, PolicyTemplate.Length).SequenceEqual(BuildPolicy(page)))
            UpgradePolicy(handle, page, module);
        long selectedPage = page;
        if (remove) PrepareRemoval(handle, page, module);
        if(actorScript)
        {
            byte[] existing=GameMoney.ReadBytes(handle,page+ActorScriptJobOffset,ActorScriptJobCode.Length);
            if(!existing.SequenceEqual(ActorScriptJobCode))
            {
                if(existing.Any(value=>value!=0)) throw new InvalidOperationException("角色脚本调用空间已被占用。");
                GameProcessPause.Run(handle,()=>
                {
                    try
                    {
                        Patch(handle,selectedPage+ActorScriptJobOffset,ActorScriptJobCode);
                        if(!GameMoney.ReadBytes(handle,selectedPage+ActorScriptJobOffset,ActorScriptJobCode.Length).SequenceEqual(ActorScriptJobCode))
                            throw new InvalidOperationException("角色脚本入口安装回读失败。");
                    }
                    catch {Patch(handle,selectedPage+ActorScriptJobOffset,existing);throw;}
                },(page+ActorScriptJobOffset,ActorScriptJobCode.Length));
            }
        }
        if(respec)
        {
            byte[] existing=GameMoney.ReadBytes(handle,page+RespecJobOffset,RespecJobCode.Length);
            if(!existing.SequenceEqual(RespecJobCode))
            {
                if(existing.Any(value=>value!=0)) throw new InvalidOperationException("洗点调用空间已被占用。");
                GameProcessPause.Run(handle,()=>
                {
                    try
                    {
                        Patch(handle,selectedPage+RespecJobOffset,RespecJobCode);
                        if(!GameMoney.ReadBytes(handle,selectedPage+RespecJobOffset,RespecJobCode.Length).SequenceEqual(RespecJobCode))
                            throw new InvalidOperationException("洗点入口安装回读失败。");
                    }
                    catch { Patch(handle,selectedPage+RespecJobOffset,existing); throw; }
                },(page+RespecJobOffset,RespecJobCode.Length));
            }
        }
        if (gwent)
        {
            byte[] existing = GameMoney.ReadBytes(handle, page + GwentJobOffset, GwentJobCode.Length);
            if (!existing.SequenceEqual(GwentJobCode))
            {
                if (existing.Any(value => value != 0)) throw new InvalidOperationException("昆特牌入口代码校验失败。");
                GameProcessPause.Run(handle, () =>
                {
                    try
                    {
                        Patch(handle, selectedPage + GwentJobOffset, GwentJobCode);
                        if (!GameMoney.ReadBytes(handle, selectedPage + GwentJobOffset, GwentJobCode.Length).SequenceEqual(GwentJobCode))
                            throw new InvalidOperationException("昆特牌入口安装回读失败。");
                    }
                    catch { Patch(handle, selectedPage + GwentJobOffset, existing); throw; }
                }, (page + GwentJobOffset, GwentJobCode.Length));
            }
        }
        if (teleportCode is not null)
        {
            if (teleportCode.Length > 0x600) throw new InvalidOperationException("传送入口长度异常。");
            byte[] desired=new byte[0x600];
            teleportCode.CopyTo(desired,0);
            byte[] existing = GameMoney.ReadBytes(handle, page + TeleportJobOffset, desired.Length);
            if (!existing.SequenceEqual(desired))
            {
                if (existing.Any(value => value != 0) && !GameTeleport.IsKnownCode(existing,module)) throw new InvalidOperationException("传送入口代码校验失败。");
                GameProcessPause.Run(handle, () =>
                {
                    try
                    {
                        Patch(handle,selectedPage+TeleportJobOffset,desired);
                        if(!GameMoney.ReadBytes(handle,selectedPage+TeleportJobOffset,desired.Length).SequenceEqual(desired))
                            throw new InvalidOperationException("传送入口安装回读失败。");
                    }
                    catch
                    {
                        Patch(handle,selectedPage+TeleportJobOffset,existing);
                        throw;
                    }
                }, (page + TeleportJobOffset, desired.Length));
            }
        }
        GameProcessPause.Run(handle, () =>
        {
            if (GameMoney.ReadBytes(handle, selectedPage + 0x90, 1)[0] != 0 ||
                GameMoney.ReadBytes(handle, selectedPage + 0x10, 1)[0] != 0)
                throw new InvalidOperationException("装备请求尚未完成。");
            if (selectedTarget is not null)
            {
                int available = GameInventory.Verify(handle, inventory, selectedTarget);
                if (remove && available < BitConverter.ToInt32(arguments, 6)) throw new InvalidOperationException("当前数量不足，请重新读取背包。");
            }
            Write(handle, selectedPage, BitConverter.GetBytes(function));
            Write(handle, selectedPage + 8, BitConverter.GetBytes(game));
            Write(handle, selectedPage + 0x98, BitConverter.GetBytes(actorScript ? selectedPage+ActorScriptJobOffset : respec ? selectedPage + RespecJobOffset : gwent ? selectedPage + GwentJobOffset : teleportCode is not null ? selectedPage + TeleportJobOffset : remove ? selectedPage + RemoveJobOffset : module + GameVersion.Rva(nativeRva)));
            long context=contextOverride??inventory;
            Write(handle, selectedPage + 0xA0, BitConverter.GetBytes(context));
            byte[] frame = new byte[0x80];
            BitConverter.GetBytes(context).CopyTo(frame, 0);
            BitConverter.GetBytes(selectedPage + 0x300).CopyTo(frame, 0x30);
            Write(handle, selectedPage + 0x200, frame);
            Write(handle, selectedPage + 0x280, new byte[16]);
            Write(handle, selectedPage + 0x300, arguments);
            Write(handle, selectedPage + 0x94, BitConverter.GetBytes(0));
            Write(handle, selectedPage + 0x90, [1]);
        });
        bool? reportedPause=null;
        for (int attempt = 0; teleportCode is not null || attempt < (fastPoll ? 1000 : 100); attempt++)
        {
            if (GameMoney.ReadInt32(handle, page + 0x94) == 1)
            {
                if(actorScript && GameMoney.ReadInt32(handle,page+0x320)!=1)
                    throw new InvalidOperationException("角色脚本调用未执行，当前场景或对象可能已变化。");
                return GameMoney.ReadBytes(handle, page + 0x280, 16);
            }
            if(teleportCode is not null)
            {
                bool paused=GameMoney.ReadInt32(handle,game+0x160)!=0;
                if(reportedPause!=paused)
                {
                    progress?.Report(paused ? "等待返回游戏：当前处于暂停状态，恢复游玩后执行传送落点查询。" : "游戏已恢复，正在等待游戏线程查询传送落点。" );
                    reportedPause=paused;
                }
            }
            Thread.Sleep(fastPoll ? 10 : 100);
        }
        bool completed=false,running=false;
        GameProcessPause.Run(handle, () =>
        {
            completed=GameMoney.ReadInt32(handle,selectedPage+0x94)==1;
            running=GameMoney.ReadBytes(handle,selectedPage+0x90,1)[0]==2;
            if(!completed && !running) Write(handle,selectedPage+0x90,[0]);
        });
        // Completion may race the last poll. Never cancel or resubmit a job already executing.
        while(running && !completed)
        {
            Thread.Sleep(10);
            completed=GameMoney.ReadInt32(handle,selectedPage+0x94)==1;
        }
        if(completed)
        {
            if(actorScript && GameMoney.ReadInt32(handle,selectedPage+0x320)!=1)
                throw new InvalidOperationException("角色脚本调用未执行，当前场景或对象可能已变化。");
            return GameMoney.ReadBytes(handle,selectedPage+0x280,16);
        }
        throw new TimeoutException("装备查询正在等待游戏运行。");
    });

    private static void PrepareRemoval(nint handle, long page, long module)
    {
        GameInventoryHooks.ValidateInstalledCode(); // Explicit deletion bypasses the quantity hook and does not require its inventory binding.
        byte[] job = (byte[])RemoveJobTemplate.Clone();
        Replace(job, 0x1122334455667788, page + RemoveCoreOffset, 1);
        byte[] core = [.. RemoveOriginal, 0xFF, 0x25, 0, 0, 0, 0, .. BitConverter.GetBytes(module + GameVersion.Rva(0x1F96070) + RemoveOriginal.Length)];
        byte[] currentJob = GameMoney.ReadBytes(handle, page + RemoveJobOffset, job.Length);
        byte[] currentCore = GameMoney.ReadBytes(handle, page + RemoveCoreOffset, core.Length);
        if (currentJob.SequenceEqual(job) && currentCore.SequenceEqual(core)) return;
        if (currentJob.Any(value => value != 0) || currentCore.Any(value => value != 0))
            throw new InvalidOperationException("删除入口代码校验失败。");
        GameProcessPause.Run(handle, () =>
        {
            Patch(handle, page + RemoveJobOffset, job);
            Patch(handle, page + RemoveCoreOffset, core);
            if (!GameMoney.ReadBytes(handle, page + RemoveJobOffset, job.Length).SequenceEqual(job) ||
                !GameMoney.ReadBytes(handle, page + RemoveCoreOffset, core.Length).SequenceEqual(core))
                throw new InvalidOperationException("删除入口安装回读失败。");
        }, (page + RemoveJobOffset, job.Length), (page + RemoveCoreOffset, core.Length));
    }

    internal static (int Added, int ReturnedIds) Give(string name, int quantity, Action? waiting = null) =>
        WithItemRequest((handle, game, inventory, module) =>
        {
            if (quantity is < 1 or > 9999 || string.IsNullOrWhiteSpace(name) || name == "Crowns")
                throw new ArgumentOutOfRangeException(nameof(quantity));
            int nameId = ResolveItem(handle, game, module, name);
            int before = CountItem(handle, inventory, nameId);
            long function = FindTick(handle, game, module);
            long page = FindPage(handle, module);
            if (page == 0) page = Install(handle, module, game, function);
            long selectedPage = page;
            GameProcessPause.Run(handle, () =>
            {
                if (GameMoney.ReadBytes(handle, selectedPage + 0x10, 1)[0] != 0 ||
                    GameMoney.ReadBytes(handle, selectedPage + 0x90, 1)[0] != 0)
                    throw new InvalidOperationException("上一件物品还在等待游戏处理。");
                Write(handle, selectedPage, BitConverter.GetBytes(function));
                Write(handle, selectedPage + 8, BitConverter.GetBytes(game));
                Write(handle, selectedPage + 0x14, BitConverter.GetBytes(nameId));
                Write(handle, selectedPage + 0x18, BitConverter.GetBytes(quantity));
                Write(handle, selectedPage + 0x1C, BitConverter.GetBytes(0));
                Write(handle, selectedPage + 0x28, BitConverter.GetBytes(0));
                Write(handle, selectedPage + 0x2C, BitConverter.GetBytes(0));
                Write(handle, selectedPage + 0x10, [1]);
            });
            for (int attempt = 0; ; attempt++)
            {
                int status = GameMoney.ReadInt32(handle, page + 0x28);
                byte pending = GameMoney.ReadBytes(handle, page + 0x10, 1)[0];
                if (status != 0 && pending == 0)
                {
                    int returned = GameMoney.ReadInt32(handle, page + 0x2C);
                    long activeInventory = CurrentInventory(handle, game, module);
                    if (activeInventory != inventory) throw new InvalidOperationException("物品请求期间库存已变化，请查看背包确认结果，避免重复添加。");
                    int after = CountItem(handle, activeInventory, nameId);
                    int added = ConfirmGiveResult(status, before, after);
                    ErrorLog.Write("添加物品完成", null, new { Name = name, Quantity = quantity, Before = before, After = after, Status = status, ReturnedIds = returned, WaitMilliseconds = attempt * 100 });
                    return (added, returned);
                }
                if (attempt == 20)
                {
                    ErrorLog.Write("添加物品等待游戏", null, new { Name = name, Quantity = quantity, Before = before, Status = status, Pending = pending });
                    waiting?.Invoke();
                }
                Thread.Sleep(100);
            }
        });

    internal static int ConfirmGiveResult(int status, int before, int after)
    {
        int added = checked(after - before);
        if (added > 0) return added;
        throw new InvalidOperationException(status == 3 ? "当前还没有可用的玩家库存。" : "请求已处理，但背包数量没有增加，请查看背包并查阅失败日志。");
    }

    internal static object Diagnostics()
    {
        try
        {
            return GameMoney.WithInventory(false, (handle, _, inventory, module) =>
            {
                long page = FindPage(handle, module);
                if (page == 0) return (object)new { Installed = false };
                int nameId = GameMoney.ReadInt32(handle, page + 0x14);
                return new
                {
                    Installed = true, Inventory = $"0x{inventory:X}", Page = $"0x{page:X}",
                    NameId = nameId, RequestedQuantity = GameMoney.ReadInt32(handle, page + 0x18),
                    GivePending = GameMoney.ReadBytes(handle, page + 0x10, 1)[0],
                    GiveStatus = GameMoney.ReadInt32(handle, page + 0x28),
                    ReturnedIds = GameMoney.ReadInt32(handle, page + 0x2C),
                    CurrentQuantity = nameId > 0 ? CountItem(handle, inventory, nameId) : 0,
                    NativePending = GameMoney.ReadBytes(handle, page + 0x90, 1)[0],
                    TickCount = GameMoney.ReadInt64(handle, page + 0x20)
                };
            });
        }
        catch (Exception ex) { return new { DiagnosticError = ex.Message }; }
    }

    internal static long TickCount() => GameMoney.WithInventory(false, (handle, _, _, module) =>
    {
        long page = FindPage(handle, module);
        return page == 0 ? 0 : GameMoney.ReadInt64(handle, page + 0x20);
    });

    internal static void InstallForProbe() => GameMoney.WithInventory(true, (handle, game, _, module) =>
    {
        long function = FindTick(handle, game, module);
        if (FindPage(handle, module) == 0) Install(handle, module, game, function);
        return 0;
    }, 0x800);

    internal static void EnsureConsumables(nint handle, long game, long inventory, long module, long inventoryHookPage)
    {
        long page = FindPage(handle, module);
        if (page != 0 && GameMoney.ReadInt64(handle, module + EntryRva + 6) == page + PolicyOffset &&
            GameMoney.ReadInt64(handle, page + 0x78) == inventory &&
            GameMoney.ReadInt64(handle, page + 0x80) == inventoryHookPage &&
            GameMoney.ReadInt64(handle, page + 0x70) != 0 &&
            GameMoney.ReadBytes(handle, page + PolicyOffset, PolicyTemplate.Length).SequenceEqual(BuildPolicy(page)))
            return;
        if (page == 0) page = Install(handle, module, game, FindTick(handle, game, module));
        long ammoFunction = FindAmmoFunction(handle, inventory, module);
        Write(handle, page + 0x70, BitConverter.GetBytes(ammoFunction));
        Write(handle, page + 0x78, BitConverter.GetBytes(inventory));
        Write(handle, page + 0x80, BitConverter.GetBytes(inventoryHookPage));
        if (GameMoney.ReadInt64(handle, page + 0x70) != ammoFunction ||
            GameMoney.ReadInt64(handle, page + 0x78) != inventory ||
            GameMoney.ReadInt64(handle, page + 0x80) != inventoryHookPage)
            throw new InvalidOperationException("消耗品开关状态未能同步到游戏。");
        if (!GameMoney.ReadBytes(handle, page + PolicyOffset, PolicyTemplate.Length).SequenceEqual(BuildPolicy(page)) ||
            GameMoney.ReadInt64(handle, module + EntryRva + 6) == page + CodeOffset)
            UpgradePolicy(handle, page, module);
    }

    internal static long ConsumableInterceptCount() => GameMoney.WithInventory(false, (handle, _, _, module) =>
    {
        long page = FindPage(handle, module);
        return page == 0 ? 0 : GameMoney.ReadInt64(handle, page + 0x88);
    });

    internal static int ResolveName(nint handle, long module, string name)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (nameBytes.Any(value => value > 127))
            throw new InvalidOperationException("当前目录的物品名称编码不受支持。");
        uint hash = 0x811C9DC5;
        foreach (byte value in nameBytes) hash = unchecked((hash ^ value) * 0x1000193);
        hash = unchecked(hash * 0x1000193);
        long pool = GameMoney.ReadInt64(handle, module + GameVersion.Rva(0x5874E38));
        int poolCount = GameMoney.ReadInt32(handle, pool + 0x11838);
        if (poolCount is < 10000 or > 1000000) throw new InvalidOperationException("游戏名称池结构异常。");
        long node = GameMoney.ReadInt64(handle, pool + 0x28 + (hash % 0x1FFF) * 8);
        int id = 0;
        for (int index = 0; node != 0 && index < 10000; index++)
        {
            if ((uint)GameMoney.ReadInt32(handle, node + 0x10) == hash)
            {
                long text = GameMoney.ReadInt64(handle, node);
                if (GameMoney.ReadBytes(handle, text, nameBytes.Length + 1)
                    .SequenceEqual(nameBytes.Concat(new byte[] { 0 }).ToArray()))
                {
                    id = GameMoney.ReadInt32(handle, node + 0x14);
                    break;
                }
            }
            node = GameMoney.ReadInt64(handle, node + 8);
        }
        if (id <= 0) throw new InvalidOperationException("当前游戏未载入这个物品名称。");
        return id;
    }

    private static int ResolveItem(nint handle, long game, long module, string name)
    {
        int id = ResolveName(handle, module, name);
        long definitions = GameMoney.ReadInt64(handle, game + 0xFD68);
        int count = GameMoney.ReadInt32(handle, definitions + 0x90);
        if (count is < 1 or > 100000) throw new InvalidOperationException("游戏物品定义表结构异常。");
        long buckets = GameMoney.ReadInt64(handle, definitions + 0xB0);
        long node = GameMoney.ReadInt64(handle, buckets + (uint)id % (uint)count * 8);
        for (int index = 0; node != 0 && index < 10000; index++)
        {
            if (GameMoney.ReadInt32(handle, node) == id && GameMoney.ReadInt32(handle, node + 0x158) == id)
                return id;
            node = GameMoney.ReadInt64(handle, node + 0x160);
        }
        throw new InvalidOperationException("当前游戏模式未载入这个物品定义。");
    }

    private static int CountItem(nint handle, long inventory, int nameId)
    {
        int count = GameMoney.ReadInt32(handle, inventory + 0x148);
        if (count is < 0 or > 10000) throw new InvalidOperationException("游戏库存结构异常。");
        long items = GameMoney.ReadInt64(handle, inventory + 0x140);
        int total = 0;
        for (int index = 0; index < count; index++)
        {
            long item = items + index * 0x98;
            if (GameMoney.ReadInt32(handle, item + 0x58) == nameId)
                total = checked(total + GameMoney.ReadInt32(handle, item + 0x54));
        }
        return total;
    }

    private static long CurrentInventory(nint handle, long game, long module)
    {
        long player = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xFD60) + 8);
        if (GameMoney.ReadInt64(handle, player) != module + GameVersion.Rva(0x383A4D0))
            throw new InvalidOperationException("游戏玩家对象已变化。");
        long inventory = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, player + 0x1B0) + 8);
        if (GameMoney.ReadInt64(handle, inventory) != module + GameVersion.Rva(0x384F4F0))
            throw new InvalidOperationException("游戏库存对象已变化。");
        return inventory;
    }

    private static long FindTick(nint handle, long game, long module)
    {
        long gameClass = GameMoney.ReadInt64(handle, game + 0x18);
        if (GameMoney.ReadInt64(handle, gameClass) != module + GameVersion.Rva(0x37D0A40) ||
            GameMoney.ReadInt32(handle, gameClass + 0x2C) != 18)
            throw new InvalidOperationException("游戏脚本类结构不匹配。");
        int count = GameMoney.ReadInt32(handle, gameClass + 0x50);
        if (count is < 1 or > 1024) throw new InvalidOperationException("游戏函数表结构异常。");
        long array = GameMoney.ReadInt64(handle, gameClass + 0x48);
        long result = 0;
        for (int index = 0; index < count; index++)
        {
            long candidate = GameMoney.ReadInt64(handle, array + index * 8);
            if (GameMoney.ReadInt32(handle, candidate + 0x10) != ResolveName(handle,module,"IsFocusModeActive")) continue;
            if (result != 0) throw new InvalidOperationException("游戏更新函数重复。");
            result = candidate;
        }
        if (result == 0 || GameMoney.ReadInt64(handle, result) != module + GameVersion.Rva(0x3A82D58) ||
            GameMoney.ReadInt64(handle, result + 8) != gameClass ||
            GameMoney.ReadInt32(handle, result + 0x18) != 0x10000 ||
            GameMoney.ReadInt32(handle, result + 0x78) != 196 ||
            GameMoney.ReadInt32(handle, result + 0x90) != 67)
            throw new InvalidOperationException("游戏更新函数元数据不匹配。");
        return result;
    }

    private static long FindAmmoFunction(nint handle, long inventory, long module)
    {
        long itemClass = GameMoney.ReadInt64(handle, inventory + 0x18);
        if (GameMoney.ReadInt64(handle, itemClass) != module + GameVersion.Rva(0x3850698) ||
            GameMoney.ReadInt32(handle, itemClass + 0x2C) != 1803)
            throw new InvalidOperationException("游戏库存脚本类结构不匹配。");
        int count = GameMoney.ReadInt32(handle, itemClass + 0x50);
        if (count is < 1 or > 1024) throw new InvalidOperationException("游戏库存函数表结构异常。");
        long array = GameMoney.ReadInt64(handle, itemClass + 0x48);
        long result = 0;
        for (int index = 0; index < count; index++)
        {
            long candidate = GameMoney.ReadInt64(handle, array + index * 8);
            if (GameMoney.ReadInt32(handle, candidate + 0x10) != ResolveName(handle,module,"SingletonItemRemoveAmmo")) continue;
            if (result != 0) throw new InvalidOperationException("消耗品函数重复。");
            result = candidate;
        }
        if (result == 0 || GameMoney.ReadInt64(handle, result) != module + GameVersion.Rva(0x3A82D58) ||
            GameMoney.ReadInt64(handle, result + 8) != itemClass ||
            GameMoney.ReadInt32(handle, result + 0x18) != 0x10000 ||
            GameMoney.ReadInt32(handle, result + 0x78) != 3749 ||
            GameMoney.ReadInt32(handle, result + 0x90) != 468)
            throw new InvalidOperationException("消耗品函数元数据不匹配。");
        return result;
    }

    private static long FindPage(nint handle, long module)
    {
        byte[] entry = GameMoney.ReadBytes(handle, module + EntryRva, EntryOriginal.Length);
        if (entry.SequenceEqual(EntryOriginal)) return 0;
        if (!entry.AsSpan(0, 6).SequenceEqual(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 }) ||
            entry[14] != 0x90 || entry[15] != 0x90)
            throw new InvalidOperationException("游戏更新入口已被其他代码修改。");
        long target = BitConverter.ToInt64(entry, 6);
        long page = target - EquipmentLevelOffset;
        bool equipment = page > 0 && (page & 0xFFF) == 0;
        if(!equipment) page = target - PolicyOffset;
        bool policy = page > 0 && (page & 0xFFF) == 0;
        if (!policy) page = target - CodeOffset;
        if (page <= 0 || (page & 0xFFF) != 0 ||
            !GameMoney.ReadBytes(handle, page + 0x40, Magic.Length).SequenceEqual(Magic) ||
            !GameMoney.ReadBytes(handle, page + CodeOffset, Template.Length).SequenceEqual(BuildCode(page, module)) ||
            (equipment && !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,EquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module)) &&
                !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,CachedEquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module,cached:true)) &&
                !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,LegacyAuxiliaryTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module,legacyAuxiliary:true)) &&
                !GameMoney.ReadBytes(handle,page+EquipmentLevelOffset,LegacyEquipmentLevelTemplate.Length).SequenceEqual(BuildEquipmentLevelCode(page,module,true))) ||
            (policy && !GameMoney.ReadBytes(handle, page + PolicyOffset, PolicyTemplate.Length).SequenceEqual(BuildPolicy(page)) &&
                !GameMoney.ReadBytes(handle, page + PolicyOffset, LegacyPolicyTemplate.Length).SequenceEqual(BuildPolicy(page, true))))
            throw new InvalidOperationException("物品运行时代码校验失败。");
        return page;
    }

    private static byte[] BuildCode(long page, long module)
    {
        byte[] code = (byte[])Template.Clone();
        Replace(code, 0x1122334455667788, page, 3);
        Replace(code, 0x2233445566778899, module + AddCoreRva, 1);
        Replace(code, 0x33445566778899AA, module + FreeRva, 1);
        Replace(code, unchecked((long)0x445566778899AABBUL), module, 1);
        Replace(code, 0x5566778899AABBCC, module + EntryRva + EntryOriginal.Length, 1);
        Replace(code, 0x66778899AABBCCDD, module + GameVersion.Rva(0x26F94A5), 1);
        return code;
    }

    private static byte[] BuildPolicy(long page, bool legacy = false)
    {
        byte[] code = (byte[])(legacy ? LegacyPolicyTemplate : PolicyTemplate).Clone();
        Replace(code, 0x1122334455667788, page, legacy ? 1 : 2);
        Replace(code, 0x5566778899AABBCC, page + CodeOffset, 1);
        return code;
    }

    private static void UpgradePolicy(nint handle, long page, long module)
    {
        long entry = module + EntryRva;
        long oldTarget = GameMoney.ReadInt64(handle, entry + 6);
        if (oldTarget != page + CodeOffset && oldTarget != page + PolicyOffset && oldTarget != page + EquipmentLevelOffset)
            throw new InvalidOperationException("物品入口在更新前已变化。");
        GameProcessPause.Run(handle, () =>
        {
            if (GameMoney.ReadInt64(handle, entry + 6) != oldTarget)
                throw new InvalidOperationException("物品入口在更新前已变化。");
            Patch(handle, page + PolicyOffset, BuildPolicy(page));
            byte[] jump = new byte[16] { 0xFF, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x90, 0x90 };
            BitConverter.GetBytes(oldTarget==page+EquipmentLevelOffset ? oldTarget : page+PolicyOffset).CopyTo(jump, 6);
            Patch(handle, entry, jump);
            if (FindPage(handle, module) != page)
                throw new InvalidOperationException("消耗品入口更新后校验失败。");
        }, (entry, 16), (page + CodeOffset, Template.Length), (page + PolicyOffset, PolicyTemplate.Length));
    }

    private static void Replace(byte[] code, long marker, long actual, int expected)
    {
        int count = 0;
        for (int index = 0; index <= code.Length - 8; index++)
            if (BitConverter.ToInt64(code, index) == marker)
            {
                BitConverter.GetBytes(actual).CopyTo(code, index);
                index += 7;
                count++;
            }
        if (count != expected) throw new InvalidOperationException("物品运行时代码标记不匹配。");
    }

    private static long Install(nint handle, long module, long game, long function)
    {
        nint allocation = VirtualAllocEx(handle, 0, 0x2000, 0x3000, 0x04);
        if (allocation == 0) throw new InvalidOperationException("无法准备游戏物品入口。");
        long page = allocation.ToInt64();
        bool safeToFree = true;
        try
        {
            Write(handle, page, BitConverter.GetBytes(function));
            Write(handle, page + 8, BitConverter.GetBytes(game));
            Write(handle, page + 0x40, Magic);
            Write(handle, page + CodeOffset, BuildCode(page, module));
            Write(handle, page + PolicyOffset, BuildPolicy(page));
            if (!VirtualProtectEx(handle, (nint)(page + CodeOffset), 0x1000, 0x20, out _) ||
                !FlushInstructionCache(handle, (nint)(page + CodeOffset), 0x1000))
                throw new InvalidOperationException("无法准备游戏物品运行时代码。");
            GameProcessPause.Run(handle, () =>
            {
                if (!GameMoney.ReadBytes(handle, module + EntryRva, EntryOriginal.Length).SequenceEqual(EntryOriginal))
                    throw new InvalidOperationException("游戏更新入口在安装前已变化。");
                byte[] jump = new byte[16] { 0xFF, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x90, 0x90 };
                BitConverter.GetBytes(page + PolicyOffset).CopyTo(jump, 6);
                safeToFree = false;
                try
                {
                    Patch(handle, module + EntryRva, jump);
                    if (FindPage(handle, module) != page)
                        throw new InvalidOperationException("游戏物品入口安装后校验失败。");
                }
                catch
                {
                    try
                    {
                        Patch(handle, module + EntryRva, EntryOriginal);
                        safeToFree = GameMoney.ReadBytes(handle, module + EntryRva, EntryOriginal.Length).SequenceEqual(EntryOriginal);
                    }
                    catch { /* Keep code allocated if the entry might still reference it. */ }
                    throw;
                }
            }, (module + EntryRva, EntryOriginal.Length));
            return page;
        }
        finally { if (safeToFree) VirtualFreeEx(handle, allocation, 0, 0x8000); }
    }

    private static void Patch(nint handle, long address, byte[] data)
    {
        if (!VirtualProtectEx(handle, (nint)address, (nuint)data.Length, 0x40, out uint previous))
            throw new InvalidOperationException("无法更新游戏物品入口。");
        try
        {
            Write(handle, address, data);
            if (!FlushInstructionCache(handle, (nint)address, (nuint)data.Length))
                throw new InvalidOperationException("无法刷新游戏物品入口。");
        }
        finally { VirtualProtectEx(handle, (nint)address, (nuint)data.Length, previous, out _); }
    }

    private static void Write(nint handle, long address, byte[] data)
    {
        if (!WriteProcessMemory(handle, (nint)address, data, data.Length, out nint count) || count != data.Length)
            throw new InvalidOperationException("写入游戏物品请求失败。");
    }

    [DllImport("kernel32.dll")] private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtectEx(nint process, nint address, nuint size, uint protection, out uint old);
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
    [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(nint process, nint address, byte[] data, int size, out nint written);
}
