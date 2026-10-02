using System.Reflection;
using System.Text.RegularExpressions;
using Witcher3Modifier;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form=new MainForm(offlinePreview:true);
        var tips=(ToolTip)typeof(MainForm).GetField("usageTips",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        var failures=new List<string>();
        int count=0;
        void Walk(Control control)
        {
            count++;
            foreach(string text in new[]{control.Text,tips.GetToolTip(control)??""})
                if(Regex.IsMatch(text,"[\\u4e00-\\u9fff]")) failures.Add(text);
            if(control is ListBox list)
                foreach(var item in list.Items)
                    if(Regex.IsMatch(item?.ToString()??"","[\\u4e00-\\u9fff]")) failures.Add(item!.ToString()!);
            if(control is ComboBox combo)
                foreach(var item in combo.Items)
                    if(Regex.IsMatch(item?.ToString()??"","[\\u4e00-\\u9fff]")) failures.Add(item!.ToString()!);
            foreach(Control child in control.Controls) Walk(child);
        }
        Walk(form);
        if(failures.Count>0) throw new Exception("Untranslated display text: "+string.Join(" | ",failures.Distinct()));
        form.CreateControl();form.PerformLayout();
        var tabs=form.Controls.OfType<TrainerTabs>().Single();
        string output=Path.Combine(AppContext.BaseDirectory,"screenshots");Directory.CreateDirectory(output);
        foreach(var page in tabs.TabPages.ToArray())
        {
            tabs.SelectedTab=page;tabs.PerformLayout();
            Size size=page.Size;int index=tabs.TabPages.IndexOf(page);
            page.Parent=null;page.Dock=DockStyle.None;page.Size=size;page.CreateControl();page.PerformLayout();
            using var bitmap=new Bitmap(page.Width,page.Height);
            page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,page.Size));
            bitmap.Save(Path.Combine(output,$"page-{index}.png"));
            page.Dock=DockStyle.Fill;tabs.Controls.Add(page);
        }
        Console.WriteLine($"English UI: {count} controls and initial list/tooltip text checked; offline mode, no game actions.");
    }
}
