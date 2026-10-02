namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private const int ActorScriptJobOffset=0x1980;
    private static readonly byte[] ActorScriptJobCode=Convert.FromHexString("53564883EC284C89C3488B7230488B8160FD00004885C07451488B4008483B067548837E2401742D488B5618488B462848394208753448395008752E488B4E08488B46104C8D46304989D9FFD00FB6C0894620EB15488B4E08488B4610488D5630FFD0C74620010000004883C4285E5BC3");

    internal static (long Game,long Player,long Function,long ReturnType,long Entry,long Reference) ActorScriptTarget(string name,int count,long? actor=null,string owner="CActor") =>
        GameMoney.WithInventory(false,(handle,game,_,module)=>
        {
            long player=FunTarget(handle,game,module).Player;
            long context=actor??player;
            if(!FunClass(handle,module,context,owner)) throw new InvalidOperationException("当前角色脚本对象类型未确认。");
            long reference=GameMoney.ReadInt64(handle,context+8);
            if(reference==0 || GameMoney.ReadInt64(handle,reference+8)!=context) throw new InvalidOperationException("当前角色生命周期未确认。");
            long fn=FindAuxiliaryFunction(handle,module,context,name,owner,count);
            long parameters=GameMoney.ReadInt64(handle,fn+0x28);
            for(int i=0;i<count;i++)
            {
                long property=GameMoney.ReadInt64(handle,parameters+i*8);
                if(GameMoney.ReadInt32(handle,property+0x20)!=i*4 || (GameMoney.ReadInt32(handle,property+0x24)&0x80)==0)
                    throw new InvalidOperationException("角色脚本的参数位置未通过检查。");
            }
            long ret=GameMoney.ReadInt64(handle,fn+0x20);
            return (game,player,fn,ret==0?0:GameMoney.ReadInt64(handle,ret+8),RespecCallEntry(handle,module),reference);
        });

    internal static byte[] InvokeActorScript(long game,long player,long fn,long entry,long context,long reference,byte[] parameters)
    {
        byte[] args=[..BitConverter.GetBytes(player),..BitConverter.GetBytes(fn),..BitConverter.GetBytes(entry),..BitConverter.GetBytes(context),..new byte[8],..BitConverter.GetBytes(reference),..parameters];
        return ExecuteEquipmentNative(0,args,contextOverride:game,actorScript:true,fastPoll:true);
    }

    internal static void ReleaseActorArray(long game,long player,long type,byte[] array)
    {
        if(BitConverter.ToInt64(array)==0 && BitConverter.ToInt32(array,8)==0) return;
        long destructor=GameMoney.WithInventory(false,(handle,current,_,module)=>
        {
            game=current;
            player=FunTarget(handle,current,module).Player;
            long fn=GameMoney.ReadInt64(handle,GameMoney.ReadInt64(handle,type)+0x70);
            byte[] prefix=Convert.FromHexString("48895C240848896C2410488974241848897C242041564883EC20488BF2488BE9");
            if(fn!=module+GameVersion.Rva(0x26FC970) || !GameMoney.ReadBytes(handle,fn,prefix.Length).SequenceEqual(prefix))
                throw new InvalidOperationException("角色查询数组的释放入口未确认。");
            return fn;
        });
        byte[] args=[..BitConverter.GetBytes(player),..BitConverter.GetBytes(type),..BitConverter.GetBytes(destructor),..new byte[8],..BitConverter.GetBytes(0),..BitConverter.GetBytes(1),..new byte[8],..array];
        ExecuteEquipmentNative(0,args,contextOverride:game,actorScript:true,fastPoll:true);
    }

}
