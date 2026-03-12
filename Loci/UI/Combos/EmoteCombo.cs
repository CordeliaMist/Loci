using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Loci.Data;
using OtterGui.Classes;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;

namespace Loci.Combos;

/// <summary> Capable of displaying every valid emote, along with its icon and all command variants. </summary>
public sealed class EmoteCombo : CkFilterComboCache<ParsedEmote>
{
    private float _iconScale = 1.0f;
    private uint _currentEmoteId;
    
    public EmoteCombo(ILogger log, float scale)
        : base(() => [.. GameDataSvc.ValidLightEmoteCache.OrderBy(e => e.RowId)], log)
    {
        _iconScale = scale;
        SearchByParts = true;
        _currentEmoteId = Items.FirstOrDefault().RowId;
    }

    public EmoteCombo(ILogger log, float scale, Func<IReadOnlyList<ParsedEmote>> gen)
        : base(gen, log)
    {
        SearchByParts = true;
        _currentEmoteId = Items.FirstOrDefault().RowId;
    }
    protected override string ToString(ParsedEmote emote)
        => emote.Name ?? string.Empty;

    protected override bool IsVisible(int globalIndex, LowerString filter)
        => base.IsVisible(globalIndex, filter) && !Items[globalIndex].EmoteCommands.IsDefaultOrEmpty;

    protected override void DrawList(float width, float itemHeight, float filterHeight)
    {
        base.DrawList(width, itemHeight, filterHeight);
        if (NewSelection != null && Items.Count > NewSelection.Value)
            Current = Items[NewSelection.Value];
    }

    protected override int UpdateCurrentSelected(int currentSelected)
    {
        if (Current.RowId == _currentEmoteId)
            return currentSelected;

        CurrentSelectionIdx = Items.IndexOf(i => i.RowId == _currentEmoteId);
        Current = CurrentSelectionIdx >= 0 ? Items[CurrentSelectionIdx] : default;
        return CurrentSelectionIdx;
    }

    public bool Draw(string id, uint current, float width, float innerWidthScaler = 1f, CFlags flags = CFlags.None)
    {
        InnerWidth = width * innerWidthScaler;
        _currentEmoteId = current;
        var preview = Items.FirstOrDefault(i => i.RowId == _currentEmoteId).Name ?? "Select Emote...";
        return Draw(id, preview, string.Empty, width, ImGui.GetFrameHeight() * _iconScale, flags);
    }

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var parsedEmote = Items[globalIdx];

        // Draw a ghost selectable at first.
        var ret = false;
        var pos = ImGui.GetCursorPos();
        using (ImRaii.Group())
        {
            var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight() * _iconScale);
            ret = ImGui.Selectable("##Entry" + globalIdx, selected, ImGuiSelectableFlags.None, size);
            // Use these positions to go back over and draw it properly this time.
            ImGui.SetCursorPos(pos);
            if (GameDataSvc.EmoteData.TryGetValue((ushort)parsedEmote.RowId, out var emoteData))
            {
                var image = Svc.Texture.GetFromGameIcon(emoteData.IconId).GetWrapOrEmpty();
                ImGui.Image(image.Handle, new(ImUtf8.FrameHeight));
                emoteData.AttachTooltip(image);
                ImUtf8.SameLineInner();
            }
            CkGui.TextFrameAligned(parsedEmote.Name);
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

