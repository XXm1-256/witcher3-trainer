namespace Witcher3Modifier;

internal static partial class GameItemScheduler
{
    private static void FillAdrenaline(nint handle,long module,long ability)
    {
        // 5.00c native GetStatMax registration and BCS_Focus=4 were checked in the installed executable.
        byte[] expected=Convert.FromHexString("48895C24084889742418574883EC20488BDA498BF8BA0C00");
        if(!GameMoney.ReadBytes(handle,module+GameVersion.Rva(0x1D74080),expected.Length).SequenceEqual(expected))
            throw new InvalidOperationException("肾上腺素上限入口未通过检查。");
        float maximum=BitConverter.ToSingle(ExecuteEquipmentNative(0x1D74080,[4,..BitConverter.GetBytes(4),0x20],contextOverride:ability));
        if(!float.IsFinite(maximum) || maximum<=0 || maximum>1000)
            throw new InvalidOperationException("肾上腺素上限尚未确认。");
        ExecuteEquipmentNative(0x1D781C0,[4,..BitConverter.GetBytes(4),6,..BitConverter.GetBytes(maximum),0x20],contextOverride:ability);
        float current=BitConverter.ToSingle(ExecuteEquipmentNative(0x1D78540,[4,..BitConverter.GetBytes(4),9,1,0x20],contextOverride:ability));
        if(!float.IsFinite(current) || Math.Abs(current-maximum)>0.001f)
            throw new InvalidOperationException($"肾上腺素补满后回读未确认。当前={current}，上限={maximum}。");
        ErrorLog.Write("肾上腺素补满",null,new {Current=current,Maximum=maximum});
    }
}
