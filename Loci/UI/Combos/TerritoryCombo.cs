using Dalamud.Bindings.ImGui;

namespace Loci.Combos;

public sealed class TerritoryCombo : CkFilterComboCache<KeyValuePair<ushort, string>>
{
    private ushort _current;
    public TerritoryCombo(ILogger log) : base(GameDataSvc.TerritoryData.OrderBy(kvp => kvp.Value), log)
    {
        Current = new KeyValuePair<ushort, string>(ushort.MaxValue, "Select Area..");
        CurrentSelectionIdx = 0;
    }

    protected override string ToString(KeyValuePair<ushort, string> obj)
        => obj.Value;

    /// <summary> Simple draw invoke. </summary>
    public bool Draw(ushort currentArea, float width, CFlags flags = CFlags.None)
    {
        InnerWidth = width * 1.3f;
        _current = currentArea;
        string previewName = Items.FirstOrDefault(x => x.Key == _current).Value ?? "Select Area...";
        return Draw("##territoryCombo", previewName, string.Empty, width, ImGui.GetTextLineHeightWithSpacing(), flags);
    }

    public bool DrawPopup(ushort currentArea, float comboWidth, Vector2 drawPos, uint? searchBg = null)
    {
        InnerWidth = comboWidth;
        _current = currentArea;

        return DrawPopup("##territoryCombo", drawPos, ImGui.GetTextLineHeightWithSpacing(), searchBg);
    }
}
