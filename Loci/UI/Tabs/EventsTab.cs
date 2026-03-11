using CkCommons;
using CkCommons.Classes;
using CkCommons.Gui;
using CkCommons.Gui.Utility;
using CkCommons.Helpers;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Combos;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;
using OtterGui.Extensions;
using OtterGui.Text;

namespace Loci.Gui;

public class LociEventsTab : IDisposable
{
    private static float SELECTOR_WIDTH => 250f * ImGuiHelpers.GlobalScale;

    private readonly LociMediator _mediator;
    private readonly EventsSelector _selector;
    private readonly LociEventData _data;
    private readonly LociManager _manager;

    public LociEventsTab(ILogger<LociEventsTab> logger, LociMediator mediator,
        EventsSelector selector, LociEventData data, LociManager manager)
    {
        _mediator = mediator;
        _selector = selector;
        _data = data;
        _manager = manager;

        _selector.SelectionChanged += ResetTemps;
    }

    private string? _tmpTitle = null;
    private string? _tmpDesc = null;
    public void Dispose()
    {
        _selector.SelectionChanged -= ResetTemps;
    }

    private void ResetTemps(LociEvent? oldSel, LociEvent? newSel, in EventsSelector.State _)
    {
        _tmpTitle = null;
        _tmpDesc = null;
    }

    public void DrawSection(Vector2 region)
    {
        using var table = ImRaii.Table("divider", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoHostExtendY, region);
        if (!table) return;

        ImGui.TableSetupColumn("selector", ImGuiTableColumnFlags.WidthFixed, SELECTOR_WIDTH);
        ImGui.TableSetupColumn("content", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        _selector.DrawFilterRow(SELECTOR_WIDTH);
        _selector.DrawList(SELECTOR_WIDTH);

        ImGui.TableNextColumn();
        DrawSelectedEvent();
    }

    private void DrawSelectedEvent()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 8f);
        using var _ = CkRaii.Child("selected", ImGui.GetContentRegionAvail());
        if (!_) return;
        var minPos = ImGui.GetCursorPos();

        CkGui.FontTextCentered("Events are currently WIP", Fonts.Default150Percent, ImGuiColors.DalamudOrange);

        CkGui.CenterTextAligned("Events is icon management on steroids, allowing automation to great lengths.");
        CkGui.CenterTextAligned("Link events to various monitors, detections, and triggers for conditional application.");
        CkGui.CenterTextAligned("Loci plans to support the following types:");
        CkGui.CenterColorTextAligned("JobChange, GameBuffDebuff, Emote Related, Zone Related,", ImGuiColors.DalamudYellow);
        CkGui.CenterColorTextAligned("ContentType Related, OnlineStatus, TimeOfDay, and more..", ImGuiColors.DalamudYellow);
    }
}
