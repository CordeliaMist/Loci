using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Loci.Data;
using OtterGui.Classes;
using OtterGui.Extensions;
using OtterGui.Raii;

namespace Loci.Combos;

/// <summary> Capable of displaying every valid emote, along with its icon and all command variants. </summary>
public sealed class OnlineStatusCombo : CkFilterComboCache<ParsedOnlineStatus>
{
    private float _iconScale = 1.0f;
    private uint _curStatusId;
    
    public OnlineStatusCombo(ILogger log, float scale)
        : base(() => [ ..GameDataSvc.OnlineStatus.Values.OrderBy(e => e.RowId) ], log)
    {
        _iconScale = scale;
        SearchByParts = true;
        _curStatusId = Items.FirstOrDefault().RowId;
    }

    public OnlineStatusCombo(ILogger log, float scale, Func<IReadOnlyList<ParsedOnlineStatus>> gen)
        : base(gen, log)
    {
        SearchByParts = true;
        _curStatusId = Items.FirstOrDefault().RowId;
    }
    protected override string ToString(ParsedOnlineStatus emote)
        => emote.Name ?? string.Empty;

    protected override void DrawList(float width, float itemHeight, float filterHeight)
    {
        base.DrawList(width, itemHeight, filterHeight);
        if (NewSelection != null && Items.Count > NewSelection.Value)
            Current = Items[NewSelection.Value];
    }

    protected override int UpdateCurrentSelected(int currentSelected)
    {
        if (Current.RowId == _curStatusId)
            return currentSelected;

        CurrentSelectionIdx = Items.IndexOf(i => i.RowId == _curStatusId);
        Current = CurrentSelectionIdx >= 0 ? Items[CurrentSelectionIdx] : default;
        return CurrentSelectionIdx;
    }

    public bool Draw(string id, uint current, float width, float innerWidthScaler = 1f, CFlags flags = CFlags.None)
    {
        InnerWidth = width * innerWidthScaler;
        _curStatusId = current;
        var preview = Items.FirstOrDefault(i => i.RowId == _curStatusId).Name ?? "Select Online Status...";
        return Draw(id, preview, string.Empty, width, ImGui.GetFrameHeight() * _iconScale, flags);
    }

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var parsedStatus = Items[globalIdx];

        // Draw a ghost selectable at first.
        var ret = false;
        var pos = ImGui.GetCursorPos();
        var img = Svc.Texture.GetFromGameIcon(parsedStatus.IconId).GetWrapOrEmpty();
        using (ImRaii.Group())
        {
            var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight() * _iconScale);
            ret = ImGui.Selectable("##Entry" + globalIdx, selected, ImGuiSelectableFlags.None, size);
            // Use these positions to go back over and draw it properly this time.
            ImGui.SetCursorPos(pos);
            ImGui.Image(img.Handle, new Vector2(size.Y));
            CkGui.TextFrameAlignedInline(parsedStatus.Name);
        }
        return ret;
    }

    protected override void OnClosePopup()
    {
        var split = Filter.Text.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2 || !ushort.TryParse(split[0], out var setId) || !byte.TryParse(split[1], out var variant))
            return;
    }
}

