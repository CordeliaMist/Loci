using CkCommons;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using LociApi.Enums;
using Lumina.Excel.Sheets;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;

namespace Loci.Combos;

/// <summary>
///   An enhanced JobCombo selector that works via bitshifting to identify selection. <para />
///   Still ironing out, may be a bit unoptimized for lookups.
/// </summary>
public sealed class JobFlagsCombo
{
    private static float _iconScale = 1.0f;
    public static Vector2 _iconSize => new Vector2(ImUtf8.FrameHeight * _iconScale);

    private LowerString _filterString = LowerString.Empty;
    private float _innerWidth;
    public JobFlagsCombo(ILogger log, float scale)
    {
        _iconScale = scale;
        _curSelFlags = 0;
    }

    private JobFlags _curSelFlags;
    public bool Draw(string label, float width, ref JobFlags current, float scaler = 1.0f, CFlags flags = CFlags.None)
    {
        var preview = current is JobFlags.None ? "Any Job..." : current.ToString();
        _curSelFlags = current;
        _innerWidth = width * scaler;

        ImGui.SetNextItemWidth(width);
        using var combo = ImRaii.Combo(label, preview, flags);
        if (combo)
        {
            DrawFilter();

            var toDraw = Enum.GetValues<JobType>()
                .Where(x => _filterString.Length > 0 ? x.ToString().Contains(_filterString, StringComparison.OrdinalIgnoreCase) : true)
                .OrderByDescending(x => Svc.Data.GetExcelSheet<ClassJob>().GetRow((uint)x).Role)
                .ToList();

            // Combo is open, process display
            foreach (var cond in toDraw)
            {
                if (cond is JobType.ADV)
                    continue;

                var name = cond.ToString().Replace("_", " ");
                var iconId = cond is JobType.ADV ? 62143 : (062100 + (int)cond);
                if (LociIcon.GetGameIconOrDefault((uint)iconId, false) is { } wrap)
                {
                    ImGui.Image(wrap.Handle, _iconSize);
                    ImGui.SameLine();
                }
                var flagCond = (JobFlags)(1UL << (int)cond);
                var selected = _curSelFlags.Has(flagCond);
                if (ImGui.Checkbox(name, ref selected))
                    current.Toggle(flagCond);
            }
        }

        return current != _curSelFlags;
    }

    private void DrawFilter()
    {
        ImGui.SetNextItemWidth(_innerWidth);
        LowerString.InputWithHint("##filter", "Filter...", ref _filterString);
    }
}

