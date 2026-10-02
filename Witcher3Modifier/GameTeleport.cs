using System.Numerics;

namespace Witcher3Modifier;

internal static partial class GameTeleport
{
    internal sealed record Target(long Game, long Player, long Module, int Id, int Area, float X, float Y, Vector3 Before, ulong SurfaceMask, long PhysicsVtable, string NavigationState);

    private static string ReadNavigationState(nint handle,long world,float x,float y)
    {
        long navigation=GameMoney.ReadInt64(handle,world+0x5738);
        if(navigation==0) return "导航对象不可用";
        int grid=GameMoney.ReadInt32(handle,navigation+0xB8),count=GameMoney.ReadInt32(handle,navigation+0x140);
        float cell=GameMoney.ReadSingle(handle,navigation+0xB0);
        if(grid is <1 or >256 || count is <1 or >32768 || !float.IsFinite(cell) || cell<=0) return "地形格结构不可用";
        float originX=GameMoney.ReadSingle(handle,navigation+0xBC),originY=GameMoney.ReadSingle(handle,navigation+0xC0);
        if(!float.IsFinite(originX) || !float.IsFinite(originY)) return "地形格坐标不可用";
        int tileX=(int)Math.Clamp((x-originX)/cell,0,grid-1),tileY=(int)Math.Clamp((y-originY)/cell,0,grid-1);
        int index=(tileX*grid+tileY)&0x7FFF;
        long tiles=GameMoney.ReadInt64(handle,navigation+0x138);
        if(index>=count || tiles==0) return "目标地形格不在当前表内";
        long tile=GameMoney.ReadInt64(handle,tiles+index*8);
        if(tile==0) return $"目标地形格{index}未载入；局部导航仍可能可用";
        return $"地形格{index}：状态{GameMoney.ReadInt32(handle,tile+0x1C)}，标志{GameMoney.ReadInt32(handle,tile+0x50)}";
    }

    internal static Target ReadTarget() => GameMoney.WithInventory(false, (handle, game, _, module) =>
    {
        long player = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xFD60) + 8);
        long systems = GameMoney.ReadInt64(handle, game + 0xFDB8);
        if (GameMoney.ReadInt32(handle, game + 0xFDC0) <= 23) throw new InvalidOperationException("地图系统尚未载入。");
        long manager = GameMoney.ReadInt64(handle, systems + 0xB8);
        if (GameMoney.ReadInt64(handle, manager) != module + GameVersion.Rva(0x381E000)) throw new InvalidOperationException("地图对象结构不匹配。");
        long world = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xF0) + 8);
        if (GameMoney.ReadInt64(handle, world) != module + GameVersion.Rva(0x38B8358)) throw new InvalidOperationException("游戏世界对象类型不匹配。");
        int count = GameMoney.ReadInt32(handle, manager + 0x174);
        if (count is < 1 or > 256) throw new InvalidOperationException("请先在当前区域地图放置一个自定义标记，然后关闭地图返回游戏。");
        int currentArea = GameMoney.ReadInt32(handle, manager + 0x110);
        long pins = GameMoney.ReadInt64(handle, manager + 0x16C);
        byte[] data = GameMoney.ReadBytes(handle, pins, count * 20);
        for (int i = 0; i < count; i++)
        {
            int offset = i * 20;
            if (BitConverter.ToInt32(data, offset + 16) != 0) continue;
            int area = BitConverter.ToInt32(data, offset + 4);
            if (area != currentArea) throw new InvalidOperationException("标记位于其他区域，请在当前区域放置标记。");
            float x = BitConverter.ToSingle(data, offset + 8), y = BitConverter.ToSingle(data, offset + 12);
            if (!float.IsFinite(x) || !float.IsFinite(y)) throw new InvalidOperationException("标记坐标无效。");
            long physics=GameMoney.ReadInt64(handle,world+0x1F0);
            long physicsVtable=physics==0 ? 0 : GameMoney.ReadInt64(handle,physics);
            ulong surfaceMask=0;
            if(physicsVtable==module + GameVersion.Rva(0x3843FC8) && GameMoney.ReadInt64(handle,physicsVtable+8)==module + GameVersion.Rva(0x1E94320))
            {
                long groups=GameMoney.ReadInt64(handle,module + GameVersion.Rva(0x5A75F00));
                if(groups!=0)
                {
                    int groupCount=GameMoney.ReadInt32(handle,groups+0x10);
                    if(groupCount is >0 and <=64)
                    {
                        int[] ids=Enumerable.Range(0,groupCount).Select(index=>GameMoney.ReadInt32(handle,GameMoney.ReadInt64(handle,groups+8)+index*4)).ToArray();
                        int first=Array.IndexOf(ids,GameMoney.ReadInt32(handle,module + GameVersion.Rva(0x5C97300)));
                        int second=Array.IndexOf(ids,GameMoney.ReadInt32(handle,module + GameVersion.Rva(0x5C97790)));
                        if(first>=0 && second>=0) surfaceMask=(1UL<<first)|(1UL<<second);
                    }
                }
            }
            return new Target(game, player, module, BitConverter.ToInt32(data, offset), area, x, y,
                new Vector3(GameMoney.ReadSingle(handle, player + 0xA0), GameMoney.ReadSingle(handle, player + 0xA4), GameMoney.ReadSingle(handle, player + 0xA8)),surfaceMask,physicsVtable,ReadNavigationState(handle,world,x,y));
        }
        throw new InvalidOperationException("没有找到自定义路径标记，请在地图上放置路径标记。");
    });

    internal static (Target Target, Vector3 Position, bool Confirmed) Execute(bool move = true,IProgress<string>? progress=null) => GameItemScheduler.RunSerialized(() =>
    {
        var target = ReadTarget();
        byte[] arguments = new byte[64];
        BitConverter.GetBytes(target.Game).CopyTo(arguments, 0);
        BitConverter.GetBytes(target.Player).CopyTo(arguments, 8);
        BitConverter.GetBytes(target.Id).CopyTo(arguments, 16);
        BitConverter.GetBytes(target.Area).CopyTo(arguments, 20);
        BitConverter.GetBytes(move ? 0 : 1).CopyTo(arguments, 24);
        BitConverter.GetBytes(target.SurfaceMask).CopyTo(arguments,32);
        BitConverter.GetBytes(target.PhysicsVtable).CopyTo(arguments,40);
        byte[] result = GameItemScheduler.ExecuteTeleportJob(BuildCode(target.Module), arguments,progress);
        int status = BitConverter.ToInt32(result);
        if(move && status is 3 or 4 or 6)
        {
            result=LoadDistantLanding(target,arguments,progress);
            status=BitConverter.ToInt32(result);
        }
        if (status is not (1 or 7))
            ErrorLog.Write("传送落点查询失败", null,
                new { Status = status, target.Id, target.Area, target.X, target.Y,
                    Before = target.Before.ToString(),
                    HorizontalDistance = Vector2.Distance(new Vector2(target.Before.X, target.Before.Y), new Vector2(target.X, target.Y)),
                    MoveRequested = move, target.SurfaceMask, target.PhysicsVtable, target.NavigationState });
        if (status is not (1 or 7)) throw new InvalidOperationException(status switch
        {
            2 => "玩家、区域或地图标记已变化，请重新设置当前区域标记。",
            3 => "标记附近未找到可用的碰撞表面或导航数据；目标可能尚未加载。请尝试附近道路标记，或接近目标后重试。",
            4 => "标记附近没有找到可落脚位置，请更换地面标记。",
            6 => "标记及附近位置的地面高度查询失败，请更换标记。",
            _ => "当前玩家移动组件尚不可用。"
        });
        var position = new Vector3(BitConverter.ToSingle(result, 4), BitConverter.ToSingle(result, 8), BitConverter.ToSingle(result, 12));
        bool confirmed = !move;
        if (move)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var actual = GameMoney.WithInventory(false, (handle, _, _, _) => new Vector3(
                    GameMoney.ReadSingle(handle, target.Player + 0xA0), GameMoney.ReadSingle(handle, target.Player + 0xA4), GameMoney.ReadSingle(handle, target.Player + 0xA8)));
                if (Vector3.Distance(actual, position) <= 3) { confirmed = true; break; }
                Thread.Sleep(100);
            }
        }
        ErrorLog.Write(move ? (confirmed ? "传送完成" : "传送已提交待确认") : "传送落点查询", null,
            new { target.Id, target.Area, target.X, target.Y, Before = target.Before.ToString(), Position = position.ToString(), Confirmed = confirmed, LandingMethod=status==7?"静态表面与站立范围":"导航落点", target.SurfaceMask });
        return (target, position, confirmed);
    });

    private static byte[] LoadDistantLanding(Target target,byte[] arguments,IProgress<string>? progress)
    {
        bool protectedBefore=GameDebugFacts.Read("noFallDamage");
        bool safelyFinished=false,staged=false;
        try
        {
            GameDebugFacts.Set("noFallDamage",true);
            progress?.Report("目标落点尚不可用：已开启临时落地保护，正在等待目标区域加载并校正位置。");
            // Same staging height as the game's xy fallback. Navigation and
            // collision are queried again after the native teleport streams the area.
            staged=true;
            Stage(new Vector3(target.X,target.Y,500.1f));
            ErrorLog.Write("传送远处加载开始",null,new {target.Id,target.Area,target.X,target.Y,ProtectedBefore=protectedBefore});
            for(int attempt=0;attempt<40;attempt++)
            {
                EnsureTarget();
                BitConverter.GetBytes(0).CopyTo(arguments,24);
                byte[] landing=GameItemScheduler.ExecuteTeleportJob(BuildCode(target.Module),arguments,progress);
                int state=BitConverter.ToInt32(landing);
                if(state is 1 or 7)
                {
                    var position=new Vector3(BitConverter.ToSingle(landing,4),BitConverter.ToSingle(landing,8),BitConverter.ToSingle(landing,12));
                    safelyFinished=ConfirmPosition(position);
                    if(!safelyFinished) throw new InvalidOperationException("落点已提交，但角色位置尚未确认。");
                    ErrorLog.Write("传送远处落点校正",null,new {Attempt=attempt+1,Position=position.ToString(),LandingStatus=state});
                    return landing;
                }
                if(state==2 || state==5) throw new InvalidOperationException("等待传送期间玩家、地图标记或移动组件已变化。");
                Thread.Sleep(500);
            }
            throw new TimeoutException("目标区域加载后仍未找到可落脚位置。");
        }
        catch(Exception error)
        {
            if(staged)
            {
                try
                {
                    EnsureTarget();
                    Stage(target.Before);
                    safelyFinished=ConfirmPosition(target.Before);
                    ErrorLog.Write("传送失败返回原位置",null,new {Confirmed=safelyFinished,Position=target.Before.ToString()});
                }
                catch(Exception rollback) {ErrorLog.Write("传送返回原位置失败",rollback);}
            }
            else safelyFinished=true;
            throw new InvalidOperationException(safelyFinished ? $"{error.Message} {(staged?"已返回原位置。":"角色位置未改变。")}" : $"{error.Message} 临时落地保护保持开启，请先确认角色位置。",error);
        }
        finally
        {
            if(!protectedBefore && safelyFinished) GameDebugFacts.Set("noFallDamage",false);
        }

        void EnsureTarget()
        {
            var current=ReadTarget();
            if(current.Game!=target.Game || current.Player!=target.Player || current.Id!=target.Id || current.Area!=target.Area ||
                current.X!=target.X || current.Y!=target.Y) throw new InvalidOperationException("玩家或地图标记已经变化。");
        }
        void Stage(Vector3 position)
        {
            if(!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                throw new InvalidOperationException("临时传送坐标无效。");
            BitConverter.GetBytes(2).CopyTo(arguments,24);
            BitConverter.GetBytes(position.Z).CopyTo(arguments,48);
            BitConverter.GetBytes(position.X).CopyTo(arguments,52);
            BitConverter.GetBytes(position.Y).CopyTo(arguments,56);
            byte[] response=GameItemScheduler.ExecuteTeleportJob(BuildCode(target.Module,StageTemplate),arguments,progress);
            if(BitConverter.ToInt32(response)!=8) throw new InvalidOperationException("游戏没有接受临时传送位置。");
        }
        bool ConfirmPosition(Vector3 position)
        {
            int stable=0;
            for(int attempt=0;attempt<30;attempt++)
            {
                var actual=ReadCurrentPosition(target);
                stable=Vector3.Distance(actual,position)<=0.5f ? stable+1 : 0;
                if(stable>=5) return true;
                Thread.Sleep(100);
            }
            return false;
        }
    }

    internal static Vector3 ReadCurrentPosition(Target target) => GameMoney.WithInventory(false,(handle,game,_,module)=>
    {
        if(game!=target.Game || module!=target.Module ||
            GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFD60)+8)!=target.Player)
            throw new InvalidOperationException("当前玩家已变化。");
        long manager=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,game+0xFDB8)+0xB8);
        if(GameMoney.ReadInt64(handle,manager)!=module + GameVersion.Rva(0x381E000) || GameMoney.ReadInt32(handle,manager+0x110)!=target.Area)
            throw new InvalidOperationException("当前游戏区域已变化。");
        var position=new Vector3(GameMoney.ReadSingle(handle,target.Player+0xA0),GameMoney.ReadSingle(handle,target.Player+0xA4),GameMoney.ReadSingle(handle,target.Player+0xA8));
        if(!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            throw new InvalidOperationException("当前玩家坐标无效。");
        return position;
    });

    internal static bool IsLegacyCode(byte[] existing,long module)
    {
        return new[]{LegacyTemplate,PreviousTemplate}.Any(template=>
        {
            byte[] legacy=BuildCode(module,template);
            return existing.Length>=legacy.Length && existing.AsSpan(0,legacy.Length).SequenceEqual(legacy) &&
                existing.AsSpan(legacy.Length).IndexOfAnyExcept((byte)0)<0;
        });
    }

    internal static bool IsKnownCode(byte[] existing,long module) => IsLegacyCode(existing,module) ||
        new[]{Template,StageTemplate}.Any(template=>
        {
            byte[] code=BuildCode(module,template);
            return existing.Length>=code.Length && existing.AsSpan(0,code.Length).SequenceEqual(code) &&
                existing.AsSpan(code.Length).IndexOfAnyExcept((byte)0)<0;
        });

    private static byte[] BuildCode(long module) => BuildCode(module,Template);

    private static byte[] BuildCode(long module,byte[] template)
    {
        byte[] code = (byte[])template.Clone();
        foreach (var (marker, value) in new[]
        {
            (0x1122334455667788L, module + GameVersion.Rva(0x381E000)), (0x2233445566778899L, module + GameVersion.Rva(0x38B8358)),
            (0x33445566778899AAL, module + GameVersion.Rva(0x23A59B0)), (0x445566778899AABBL, module + GameVersion.Rva(0x23A5C40))
        })
        {
            int replacements = 0;
            for (int i = 0; i <= code.Length - 8; i++)
                if (BitConverter.ToInt64(code, i) == marker) { BitConverter.GetBytes(value).CopyTo(code, i); replacements++; i += 7; }
            if (replacements != 1) throw new InvalidOperationException("传送代码地址标记不匹配。");
        }
        return code;
    }
}
