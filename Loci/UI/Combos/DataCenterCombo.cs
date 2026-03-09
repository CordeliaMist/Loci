using Dalamud.Bindings.ImGui;

namespace Loci.Combos;

public sealed class DataCenterCombo : CkFilterComboCache<KeyValuePair<byte, string>>
{
    private ushort _current;
    public DataCenterCombo(ILogger log) : base(GameDataSvc.DataCenterData.OrderBy(kvp => kvp.Value), log)
    {
        Current = new KeyValuePair<byte, string>(byte.MaxValue, "Select DC..");
        CurrentSelectionIdx = 0;
    }

    protected override string ToString(KeyValuePair<byte, string> obj)
        => obj.Value;

    public bool Draw(ushort currentDC, float width, CFlags flags = CFlags.None)
    {
        InnerWidth = width * 1.3f;
        _current = currentDC;
        string previewName = Items.FirstOrDefault(x => x.Key == _current).Value ?? "Select DC...";
        return Draw("##dcCombo", previewName, string.Empty, width, ImGui.GetTextLineHeightWithSpacing(), flags);
    }

    public bool DrawPopup(ushort currentDC, float comboWidth, Vector2 drawPos, uint? searchBg = null)
    {
        InnerWidth = comboWidth;
        _current = currentDC;

        return DrawPopup("##dcCombo", drawPos, ImGui.GetTextLineHeightWithSpacing(), searchBg);
    }
}
