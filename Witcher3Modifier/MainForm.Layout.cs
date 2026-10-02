namespace Witcher3Modifier;

public sealed partial class MainForm
{
    private readonly TextBox affixSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索词条名称或效果" };
    private readonly TextBox enchantSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索附魔" };
    private readonly HashSet<string> selectedAffixes = [];
    private bool filteringAffixes;

    private string CurrentGearCategory() => gearType.Text switch
    {
        "钢剑"=>"steelsword","银剑"=>"silversword","胸甲"=>"armor","靴子"=>"boots",
        "裤子"=>"pants","手套"=>"gloves","弩"=>"crossbow",_=>""
    };

    private void RestoreWindowBounds()
    {
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int width = draft.WindowWidth > 0 ? draft.WindowWidth : (int)(area.Width * 0.67);
        int height = draft.WindowHeight > 0 ? draft.WindowHeight : (int)(area.Height * 0.74);
        Size = new Size(Math.Clamp(width, Math.Min(700, area.Width), area.Width), Math.Clamp(height, Math.Min(360, area.Height), area.Height));
        if (draft.WindowWidth > 0)
        {
            var saved = new Rectangle(draft.WindowX, draft.WindowY, Size.Width, Size.Height);
            area = Screen.FromRectangle(saved).WorkingArea;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Math.Clamp(saved.X, area.Left, Math.Max(area.Left, area.Right - Width)),
                Math.Clamp(saved.Y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        }
        if (draft.WindowMaximized) WindowState = FormWindowState.Maximized;
    }

    private void SaveWindowBounds()
    {
        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        draft.WindowX = bounds.X; draft.WindowY = bounds.Y;
        draft.WindowWidth = bounds.Width; draft.WindowHeight = bounds.Height;
        draft.WindowMaximized = WindowState == FormWindowState.Maximized;
        try { DraftStore.Save(draft); }
        catch (Exception ex) { ErrorLog.Write("保存窗口位置", ex); }
    }

    private void FilterAffixes()
    {
        filteringAffixes = true;
        affixes.BeginUpdate();
        affixes.Items.Clear();
        string query = affixSearch.Text.Trim();
        foreach (var effect in GameEquipment.Presets.Affixes.Where(effect =>
            query.Length == 0 || effect.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            effect.Description.Contains(query, StringComparison.OrdinalIgnoreCase) || effect.Id.Contains(query, StringComparison.OrdinalIgnoreCase)))
            affixes.Items.Add(effect, selectedAffixes.Contains(effect.Id));
        affixes.EndUpdate();
        filteringAffixes = false;
    }

    private void FilterEditAffixes()
    {
        filteringEditAffixes = true;
        editAffixes.BeginUpdate();
        editAffixes.Items.Clear();
        string query = editAffixSearch.Text.Trim();
        foreach (var effect in GameEquipment.Presets.Affixes.Where(effect =>
            query.Length == 0 || effect.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            effect.Description.Contains(query, StringComparison.OrdinalIgnoreCase) || effect.Id.Contains(query, StringComparison.OrdinalIgnoreCase)))
            editAffixes.Items.Add(effect, selectedEditAffixes.Contains(effect.Id));
        editAffixes.EndUpdate();
        filteringEditAffixes = false;
    }
}
