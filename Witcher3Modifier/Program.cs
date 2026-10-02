namespace Witcher3Modifier;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            ErrorLog.Write("界面未处理异常",e.Exception);
            MessageBox.Show($"修改器遇到错误，详细记录已保存到：\n{ErrorLog.Path}\n\n{e.Exception.Message}","修改器错误");
            Application.Exit();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorLog.Write("程序未处理异常",e.ExceptionObject as Exception);
        try
        {
            ErrorLog.Write("程序启动",null);
            using var form=new MainForm();
            form.FormClosing += (_,e) => ErrorLog.Write("程序关闭",null,new { Reason=e.CloseReason.ToString() });
            Application.Run(form);
        }
        catch(Exception ex)
        {
            ErrorLog.Write("程序启动或运行失败",ex);
            MessageBox.Show($"{ex.Message}\n\n详细记录：{ErrorLog.Path}","修改器错误");
        }
    }
}
