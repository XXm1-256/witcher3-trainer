namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private const int GwentJobOffset = 0x1500;
    private static readonly byte[] GwentJobCode = Convert.FromHexString("5356574883EC204C89C3488B72304889D7C70302000000488B064839C10F85C1000000488B8160FD00004885C00F84B1000000488B4008483B46380F85A3000000488B4020483B46200F8595000000488B8190FD0000483B46100F8584000000488B88D0010000483B4E0875774885C97472488B4118483B46287568488B4120483B4618755E4885C074594883B84001000000744F488B562083BAEC06000002754280B85801000000753980B859010000007530488B91C80000004885D2742448837A0800741DC6805801000001C68059010000004889FA488B4630FFD0C703010000004883C4205F5E5BC3");

    internal static void WinGwent() => RunSerialized(() =>
    {
        var target = GameMoney.WithInventory(false, (handle, game, _, module) =>
        {
            long gui = GameMoney.ReadInt64(handle, game + 0xFD90);
            if (gui == 0 || GameMoney.ReadInt64(handle, gui) != module + GameVersion.Rva(0x38232C8))
                throw new InvalidOperationException("昆特牌界面管理器不匹配。");
            long menu = GameMoney.ReadInt64(handle, gui + 0x1D0);
            if (menu == 0) throw new InvalidOperationException("请先进入昆特牌对局的出牌界面。");
            long cls = GameMoney.ReadInt64(handle, menu + 0x18);
            if (GameMoney.ReadInt32(handle, cls + 0x2C) != ResolveName(handle, module, "CR4GwintGameMenu"))
                throw new InvalidOperationException("请先进入昆特牌对局的出牌界面。");
            ValidateGwentProperty(handle, module, cls, "playerWon", 0x158);
            ValidateGwentProperty(handle, module, cls, "playerForfeited", 0x159);
            ValidateGwentProperty(handle, module, cls, "m_fxSetGwintResult", 0x140);
            long script = GameMoney.ReadInt64(handle, menu + 0x20);
            long player = GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xFD60) + 8);
            ValidateGwentProperty(handle, module, GameMoney.ReadInt64(handle, player + 0x18), "gwintMinigameState", 0x6EC);
            long playerScript = GameMoney.ReadInt64(handle, player + 0x20);
            if (script == 0 || playerScript == 0 || GameMoney.ReadInt64(handle, script + 0x140) == 0 ||
                GameMoney.ReadInt32(handle, playerScript + 0x6EC) != 2)
                throw new InvalidOperationException("当前没有进行中的昆特牌对局，请停在出牌界面。");
            if (!GameMoney.ReadBytes(handle, module + GameVersion.Rva(0x2050950), 40).SequenceEqual(Convert.FromHexString("4883EC4848FF42304C8BC1488B89C80000004885C90F8495000000488B49084885C90F8488000000")))
                throw new InvalidOperationException("昆特牌结算入口代码校验失败。");
            return (Game: game, Gui: gui, Menu: menu, Class: cls, Script: script, Player: player, PlayerScript: playerScript, Module: module);
        });
        byte[] arguments = [.. BitConverter.GetBytes(target.Game), .. BitConverter.GetBytes(target.Menu),
            .. BitConverter.GetBytes(target.Gui), .. BitConverter.GetBytes(target.Script),
            .. BitConverter.GetBytes(target.PlayerScript), .. BitConverter.GetBytes(target.Class),
            .. BitConverter.GetBytes(target.Module + GameVersion.Rva(0x2050950)), .. BitConverter.GetBytes(target.Player)];
        byte[] result = ExecuteEquipmentNative(0, arguments, contextOverride: target.Game, gwent: true);
        if (BitConverter.ToInt32(result) != 1) throw new InvalidOperationException("对局状态已变化，本次获胜操作未执行。");
        for (int attempt = 0; attempt < 50; attempt++)
        {
            bool won = GameMoney.WithInventory(false, (handle, game, _, _) =>
                game == target.Game && GameMoney.ReadInt64(handle, GameMoney.ReadInt64(handle, game + 0xFD60) + 8) == target.Player &&
                GameMoney.ReadInt64(handle, target.Player + 0x20) == target.PlayerScript &&
                GameMoney.ReadInt32(handle, target.PlayerScript + 0x6EC) == 0x10);
            if (won) { ErrorLog.Write("昆特牌获胜已确认", null, new { target.Menu, State = 0x10 }); return 0; }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("已请求结束对局，但尚未确认胜利结算；请返回游戏观察结果，勿重复操作。");
    });

    private static void ValidateGwentProperty(nint handle, long module, long cls, string name, int offset)
    {
        int id = ResolveName(handle, module, name);
        for (int depth = 0; cls != 0 && depth < 16; depth++, cls = GameMoney.ReadInt64(handle, cls + 0x10))
        {
            int count = GameMoney.ReadInt32(handle, cls + 0x38);
            if (count is < 0 or > 4096) throw new InvalidOperationException("昆特牌属性表异常。");
            long properties = GameMoney.ReadInt64(handle, cls + 0x30);
            for (int index = 0; index < count; index++)
            {
                long property = GameMoney.ReadInt64(handle, properties + index * 8);
                if (GameMoney.ReadInt32(handle, property + 0x10) != id) continue;
                if (GameMoney.ReadInt32(handle, property + 0x20) == offset && (GameMoney.ReadInt32(handle, property + 0x24) & 0x20) != 0) return;
                throw new InvalidOperationException("昆特牌状态字段不匹配。");
            }
        }
        throw new InvalidOperationException("未找到昆特牌状态字段。");
    }
}
