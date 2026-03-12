using CkCommons;
using CkCommons.Classes;
using CkCommons.Gui;
using CkCommons.Gui.Utility;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.Services;
using Lumina.Excel.Sheets;
using OtterGui.Text;

namespace Loci;

/// <summary>
///     Utility class to select a FFXIV Status Icon from its possible options. <para />
///     Various filtering is provided.
/// </summary>
public class IconDataSelector
{
    private readonly FavoritesConfig _favorites;

    private static readonly HashSet<JobType> UpgradableJobs = [JobType.GLA, JobType.PGL, JobType.MRD, JobType.LNC, JobType.ARC, JobType.CNJ, JobType.THM, JobType.ACN, JobType.ROG];
    private static readonly HashSet<JobType> NonUpgradableJobs = Enum.GetValues<JobType>().Except(UpgradableJobs).ToHashSet();
    private static TriStateBoolCheckbox Stackable = new();
    private static Vector2 IconSize => new(24f);
    
    private SortOption _sortStyle = SortOption.Numerical;
    private TriStateBool _fcStatus = TriStateBool.Null;
    private TriStateBool _stackable = TriStateBool.Null;
    private List<JobType> _jobs = [];
    private string _filterStr = string.Empty;

    public IconDataSelector(FavoritesConfig favorites)
    {
        _favorites = favorites;

        foreach (var x in Svc.Data.GetExcelSheet<Status>())
        {
            if (IconArray.Contains(x.Icon))
                continue;
            if (x.Icon is 0)
                continue;
            if (string.IsNullOrEmpty(x.Name.ExtractText()))
                continue;
            IconArray.Add(x.Icon);
        }
    }

    public List<uint> IconArray = [];
    public bool AutoFill { get; private set; } = false;

    /// <summary>
    ///     Draws the popup display to the topright of the last drawn item. <para />
    ///     Displays various filters for Status Icons from XIV, updating results when selected.
    /// </summary>
    public bool Draw(LociStatus status)
    {
        var statusInfos = IconArray.Select(Utils.GetIconData).Where(x => x.HasValue).Cast<StatusIconData>();
        ImGui.SetNextItemWidth(150f);
        ImGui.InputTextWithHint("##search", "Filter...", ref _filterStr, 50);
        ImGui.SameLine();
        var autoFill = AutoFill;
        if (ImGui.Checkbox("Prefill Data", ref autoFill))
            AutoFill = autoFill;
        CkGui.HelpText("Changes the Status title and description upon selection.");

        ImGui.SameLine();
        if (Stackable.Draw("Stackable", _stackable, out var newVal))
            _stackable = newVal;
        CkGui.HelpText("All effects, only icons that stack, or only icons that don't stack.");
        
        CkGui.TextFrameAlignedInline("Class/Job:");
        ImGui.SameLine();
        DrawJobCombo(120f);

        CkGui.TextFrameAlignedInline("Sorting:");
        ImUtf8.SameLineInner();
        if (CkGuiUtils.EnumCombo("##order", 100f, _sortStyle, out var newStyle))
            _sortStyle = newStyle;

        var changed = false;
        using (var _ = CkRaii.Child("icon tables scrollable", new(ImGui.GetContentRegionAvail().X, CkStyle.GetFrameRowsHeight(12))))
        {
            if (FavoritesConfig.IconIDs.Count > 0 && ImGui.CollapsingHeader("Favorites"))
                DrawIconTable(status.IconID, statusInfos.Where(x => FavoritesConfig.IconIDs.Contains(x.IconID)).OrderBy(x => x.IconID), OnIconSelected);

            if (ImGui.CollapsingHeader("Positive Status Effects"))
                DrawIconTable(status.IconID, statusInfos.Where(x => x.Type is IconType.Positive).OrderBy(x => x.IconID), OnIconSelected);


            if (ImGui.CollapsingHeader("Negative Status Effects"))
                DrawIconTable(status.IconID, statusInfos.Where(x => x.Type is IconType.Negative).OrderBy(x => x.IconID), OnIconSelected);


            if (ImGui.CollapsingHeader("Special Status Effects"))
                DrawIconTable(status.IconID, statusInfos.Where(x => x.Type is IconType.Special).OrderBy(x => x.IconID), OnIconSelected);
        }
        return changed;

        void OnIconSelected(StatusIconData newIcon)
        {
            // Ensure we update the title and description if the data matched.
            var oldInfo = Utils.GetIconData(status.IconID);
            if (AutoFill)
            {
                if (status.Title.Length is 0 || status.Title == oldInfo?.Name)
                    status.Title = newIcon.Name;

                if (status.Description.Length is 0 || status.Description == oldInfo?.Description)
                    status.Description = newIcon.Description;
            }
            // Update icon regardless, then return true
            status.IconID = newIcon.IconID;
            changed = true;
        }
    }

    public bool Draw(LociEvent lociEvent)
    {
        var statusInfos = IconArray.Select(Utils.GetIconData).Where(x => x.HasValue).Cast<StatusIconData>();
        ImGui.SetNextItemWidth(150f);
        ImGui.InputTextWithHint("##search", "Filter...", ref _filterStr, 50);
        ImGui.SameLine();
        var autoFill = AutoFill;
        if (ImGui.Checkbox("Prefill Data", ref autoFill))
            AutoFill = autoFill;
        CkGui.HelpText("Changes the Status title and description upon selection.");

        ImGui.SameLine();
        if (Stackable.Draw("Stackable", _stackable, out var newVal))
            _stackable = newVal;
        CkGui.HelpText("All effects, only icons that stack, or only icons that don't stack.");

        CkGui.TextFrameAlignedInline("Class/Job:");
        ImGui.SameLine();
        DrawJobCombo(120f);

        CkGui.TextFrameAlignedInline("Sorting:");
        ImUtf8.SameLineInner();
        if (CkGuiUtils.EnumCombo("##order", 100f, _sortStyle, out var newStyle))
            _sortStyle = newStyle;

        var changed = false;
        using (var _ = CkRaii.Child("icon tables scrollable", new(ImGui.GetContentRegionAvail().X, CkStyle.GetFrameRowsHeight(12))))
        {
            if (FavoritesConfig.IconIDs.Count > 0 && ImGui.CollapsingHeader("Favorites"))
                DrawIconTable(lociEvent.IndicatedID, statusInfos.Where(x => FavoritesConfig.IconIDs.Contains(x.IconID)).OrderBy(x => x.IconID), OnSelected);

            if (ImGui.CollapsingHeader("Positive Status Effects"))
                DrawIconTable(lociEvent.IndicatedID, statusInfos.Where(x => x.Type is IconType.Positive).OrderBy(x => x.IconID), OnSelected);

            if (ImGui.CollapsingHeader("Negative Status Effects"))
                DrawIconTable(lociEvent.IndicatedID, statusInfos.Where(x => x.Type is IconType.Negative).OrderBy(x => x.IconID), OnSelected);

            if (ImGui.CollapsingHeader("Special Status Effects"))
                DrawIconTable(lociEvent.IndicatedID, statusInfos.Where(x => x.Type is IconType.Special).OrderBy(x => x.IconID), OnSelected);
        }
        return changed;

        void OnSelected(StatusIconData newData)
        {
            lociEvent.IndicatedID = newData.IconID;
            changed = true;
        }
    }

    private void DrawIconTable(uint curIconId, IEnumerable<StatusIconData> allInfos, Action<StatusIconData> onSelected)
    {
        var infos = ApplyFilters(allInfos);

        // Process sorts
        if (_sortStyle is SortOption.Alphabetical)
            infos = infos.OrderBy(x => x.Name);
        else if (_sortStyle is SortOption.Numerical)
            infos = infos.OrderBy(x => x.IconID);

        // If no infos display that nothing matches the conditions.
        if (!infos.Any())
            CkGui.FontTextCentered("0 Results match your filter conditions.", Fonts.Default150Percent, CkCol.TriStateCross.Vec4Ref());

        // Determine the total columns, and then display the table.
        var cols = Math.Clamp((int)(ImGui.GetWindowSize().X / 200f), 1, 10);
        using var table = ImRaii.Table("StatusTable", cols, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!table) return;

        // Setup the columns
        for (var i = 0; i < cols; i++)
            ImGui.TableSetupColumn($"iconColumn{i}");

        // Iterate through the icons, moving to the next row when applicable.
        var index = 0;
        foreach (var info in infos)
        {
            if (index % cols is 0)
                ImGui.TableNextRow();
            
            index++;
            ImGui.TableNextColumn();
            if (LociIcon.TryGetGameIcon(info.IconID, false, out var wrap))
            {
                ImGui.Image(wrap.Handle, LociIcon.Size);
                CkGui.AttachToolTip($"{info.IconID}--SEP----COL--{info.Description}--COL--", ImGuiColors.DalamudGrey2);

                ImGui.SameLine();
                if (ImGui.RadioButton($"{info.Name}##{info.IconID}", curIconId == info.IconID))
                {
                    onSelected?.Invoke(info);
                    return;
                }

                ImGui.SameLine();
                FavStar.Draw(_favorites, info.IconID, true);
            }
        }
    }

    private IEnumerable<StatusIconData> ApplyFilters(IEnumerable<StatusIconData> toFilter)
    {
        var toRet = new List<StatusIconData>();
        // Filter through a single pass only.
        foreach (var icon in toFilter)
        {
            if (_filterStr.Length != 0 && !icon.Name.Contains(_filterStr, StringComparison.OrdinalIgnoreCase) && !icon.IconID.ToString().Contains(_filterStr))
                continue;
            // Skip if fc status doesnt match.
            if (_fcStatus != TriStateBool.Null && _fcStatus != icon.IsFCBuff)
                continue;
            // Stackable match
            if (_stackable != TriStateBool.Null && _stackable != icon.IsStackable)
                continue;
            // Job filter
            if (_jobs.Count > 0)
            {
                if (icon.ClassJobCategory.RowId <= 1)
                    continue;

                var matched = false;
                foreach (var j in _jobs)
                {
                    if (icon.ClassJobCategory.IsJobInCategory(j.GetUpgradedJob()) || icon.ClassJobCategory.IsJobInCategory(j.GetDowngradedJob()))
                    {
                        matched = true;
                        break;
                    }
                }
                // If we didnt match after the check, skip
                if (!matched)
                    continue;
            }
            
            // It was a match, so add.
            toRet.Add(icon);
        }
        return toRet;
    }

    private void DrawJobCombo(float width)
    {
        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo("##jerb", _jobs.Select(x => x.ToString().Replace("_", " ")).PrintRange(out var fullList));
        if (!combo) return;

        // Combo is open, process display
        foreach (var cond in NonUpgradableJobs.OrderByDescending(x => Svc.Data.GetExcelSheet<ClassJob>().GetRow((uint)x).Role))
        {
            if (cond is JobType.ADV)
                continue;
            var name = cond.ToString().Replace("_", " ");
            var iconId = cond is JobType.ADV ? 62143 : (062100 + (int)cond);
            if (LociIcon.GetGameIconOrDefault((uint)iconId, false) is { } wrap)
            {
                ImGui.Image(wrap.Handle, IconSize);
                ImGui.SameLine();
            }
            var selected = _jobs.Contains(cond);
            if (ImGui.Checkbox(name, ref selected))
            {
                if (!_jobs.Remove(cond))
                    _jobs.Add(cond);
            }
        }
    }
}


