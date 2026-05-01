using CkCommons.DrawSystem;
using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.RichText;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;
using OtterGui.Extensions;
using OtterGui.Text;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace Loci.Gui;

public class ManagersTab
{
    private readonly ILogger<ManagersTab> _logger;
    private readonly LociMediator _mediator;
    private readonly LociManager _manager;
    private readonly SMDrawer _drawer;
    
    public ManagersTab(ILogger<ManagersTab> logger, LociMediator mediator,
        LociManager manager, SMDrawer drawer)
    {
        _logger = logger;
        _mediator = mediator;
        _manager = manager;
        _drawer = drawer;
    }
    private static float SELECTOR_WIDTH => 250f * ImGuiHelpers.GlobalScale;
    private ActorSM? Selected => _drawer.Selected;

    public void DrawSection(Vector2 region)
    {
        using var table = ImRaii.Table("divider", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoHostExtendY, region);
        if (!table) return;

        ImGui.TableSetupColumn("selector", ImGuiTableColumnFlags.WidthFixed, SELECTOR_WIDTH);
        ImGui.TableSetupColumn("content", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        _drawer.DrawFilterRow(SELECTOR_WIDTH, 50);
        _drawer.DrawContents(SELECTOR_WIDTH, 0f, 2f, DynamicFlags.SelectableLeaves);

        ImGui.TableNextColumn();
        DrawSelectedManager();
    }

    private void DrawSelectedManager()
    {
        if (Selected is not { } selected)
        {
            CkGui.FontTextCentered("Select an Actor to view their Status Manager!", Fonts.Default150Percent);
            return;
        }

        CkGui.FontText(selected.Identifier, Fonts.Default150Percent);
        using (ImRaii.Group())
        {
            CkGui.IconTextAligned(FAI.Eye);
            CkGui.TextFrameAlignedInline("Is Owner Valid (Present)");
            ImGui.SameLine();
            CkGui.ColorTextBool(selected.OwnerValid ? "Valid" : "Invalid", selected.OwnerValid);

            CkGui.IconTextAligned(FAI.Link);
            CkGui.TextFrameAlignedInline("Managed by Plugins (Ephemeral):");
            CkGui.BoolIconFramed(selected.Ephemeral, true);
            if (selected.Ephemeral)
            {
                foreach (var hostKey in selected.EphemeralHosts)
                    CkGui.BulletText(hostKey, ImGuiColors.DalamudGrey2.ToUint());
            }
        }

        DrawStatuses(selected);
    }

    private void DrawStatuses(ActorSM manager)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 10f);
        using var _ = CkRaii.FramedChildPaddedWH("active-statuses", ImGui.GetContentRegionAvail(), 0, LociCol.Gold.Uint());
        if (!_) return;

        if (manager.Statuses.Count is 0)
        {
            CkGui.FontTextCentered("No Statuses Applied", Fonts.Default150Percent, ImGuiColors.DalamudGrey2);
            return;
        }

        // Push the font first so the height is correct.
        var rowSize = new Vector2(_.InnerRegion.X, LociIcon.Size.Y);
        foreach (var (status, idx) in manager.Statuses.ToList().WithIndex())
        {
            ImGui.TableNextColumn();
            using var id = ImRaii.PushId(status.ID);
            using var entry = ImRaii.Group();
            LociIcon.Draw((uint)status.IconID, status.Stacks, LociIcon.Size);
            Utils.AttachTooltip(status);

            ImGui.SameLine();
            using (Fonts.Default150Percent.Push())
            {
                var adjust = (rowSize.Y - ImUtf8.TextHeight) * 0.5f;
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + adjust);
                CkRichText.Text(status.Title, 10);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - adjust);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - CkGui.IconButtonSize(FAI.TimesCircle).X);
            }
            if (CkGui.IconButton(FAI.Minus, disabled: manager.Ephemeral, inPopup: true))
                manager.Cancel(status.GUID, ManagerChangeType.ApplyRemove);
            CkGui.AttachTooltip("Remove from manager.");

            if (idx > 1 && idx < manager.Statuses.Count)
                ImGui.Separator();
        }
    }
}
