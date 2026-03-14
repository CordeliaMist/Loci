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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Loci.Combos;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;
using OtterGui.Extensions;
using OtterGui.Text;

namespace Loci.Gui;

public class PresetsTab : IDisposable
{
    private const string DRAGDROP_LABEL = "PRESET_ORDER";
    private static float SELECTOR_WIDTH => 250f * ImGuiHelpers.GlobalScale;

    private readonly LociMediator _mediator;
    private readonly PresetSelector _selector;
    private readonly LociData _data;
    private readonly LociManager _manager;

    private readonly Queue<Action> _postDrawActions = new();
    private SavedStatusesCombo _ownStatusCombo;
    public PresetsTab(ILogger<PresetsTab> logger, LociMediator mediator, 
        PresetSelector selector, LociData data, LociManager manager)
    {
        _mediator = mediator;
        _selector = selector;
        _data = data;
        _manager = manager;
        _ownStatusCombo = new SavedStatusesCombo(logger, manager, () => selector.Selected is not null
            ? [ ..LociData.Statuses.Where(s => !selector.Selected.Statuses.Contains(s.GUID)).OrderBy(s => s.Title.StripColorTags()) ]
            : [ ..LociData.Statuses.OrderBy(s => s.Title.StripColorTags()) ]);
        _ownStatusCombo.HintText = "Add new Status...";

        _selector.SelectionChanged += ResetTemps;
    }

    private string? _tmpTitle = null;
    private string? _tmpDesc = null;
    private string _selectedHost = string.Empty;
    private Guid _draggedStatus = Guid.Empty;
    private static Vector2 IconSize => LociIcon.Size * 1.5f;
    public void Dispose()
    {
        _selector.SelectionChanged -= ResetTemps;
    }

    private void ResetTemps(LociPreset? oldSel, LociPreset? newSel, in PresetSelector.State _)
    {
        _tmpTitle = null;
        _tmpDesc = null;
        _selectedHost = string.Empty;
        _draggedStatus = Guid.Empty;
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
        DrawSelectedStatus();
    }

    private void DrawSelectedStatus()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 8f);
        using var _ = CkRaii.Child("selected", ImGui.GetContentRegionAvail());
        if (!_) return;
        var minPos = ImGui.GetCursorPos();
        if (_selector.Selected is not { } preset)
        {
            CkGui.FontTextCentered("No Preset Selected", Fonts.UidFont, ImGuiColors.DalamudGrey);
            return;
        }

        // Do some fancy way of displaying the LociStatus later.
        if (ImGui.Button("Apply"))
            LociManager.ClientSM.ApplyPreset(preset);
        CkGui.FrameSeparatorV();
        DrawTargetApplication(preset);

        var comboLen = ImGui.CalcTextSize("Ignore Existingm").X + ImUtf8.FrameHeight;
        var endX = ImGui.GetWindowContentRegionMin().X + CkGui.GetWindowContentRegionWidth();
        ImGui.SameLine(endX - comboLen);
        if (CkGuiUtils.EnumCombo("##applyType", comboLen, preset.ApplyType, out var newType, toString: a => a.ToDisplayName(), defaultText: "Select Application Type", flags: CFlags.None))
        {
            preset.ApplyType = newType;
            _data.MarkPresetModified(preset);
        }
        CkGui.AttachToolTip("The Application Rules for this preset.");

        // Now below this draw out the details of the preset
        ImGui.Separator();

        DrawTitleDescription(preset);

        ImGui.Spacing();
        using var arranger = CkRaii.FramedChildPaddedWH("arranger-space", ImGui.GetContentRegionAvail(), 0, LociCol.Silver.Uint());

        var pos = ImGui.GetCursorPos();
        DrawStatusArrangement(preset, arranger.InnerRegion.X);
        ImGui.SetCursorPos(pos + new Vector2(arranger.InnerRegion.X - ImUtf8.FrameHeight, 0));
        CkGui.FramedHoverIconText(FAI.QuestionCircle, ImGuiColors.TankBlue.ToUint(), ImGui.GetColorU32(ImGuiCol.TextDisabled));
        CkGui.AttachToolTip("--COL--Left-Click & Drag--COL-- an icon to rearrange order." +
            "--NL----COL--Right-Click--COL-- a Status to remove it from the list.", ImGuiColors.DalamudOrange);

        // Shift down to the bottom, up by one frame height.
        ImGui.SetCursorPosY(pos.Y + arranger.InnerRegion.Y - ImUtf8.FrameHeightSpacing);
        if (_ownStatusCombo.Draw("presetSelector", Guid.Empty, arranger.InnerRegion.X))
        {
            if (_ownStatusCombo.Current is { } valid && !preset.Statuses.Contains(valid.GUID))
            {
                preset.Statuses.Add(valid.GUID);
                _data.MarkPresetModified(preset);
            }
        }

        while (_postDrawActions.TryDequeue(out Action? action))
        {
            Generic.Safe(() => action());
        }

        // If drag-drop is no longer active, clear the dragged nodes.
        if (_draggedStatus != Guid.Empty && !ImGuiP.IsDragDropActive())
        {
            // Clear the dragged status and mark our preset as modified to save the new order.
            _draggedStatus = Guid.Empty;
            _data.MarkPresetModified(preset);
        }
    }

    private void DrawTitleDescription(LociPreset preset)
    {
        using var _ = CkRaii.Child("titleDesc", new Vector2(ImGui.GetContentRegionAvail().X, CkStyle.GetFrameRowsHeight(4)));
        if (!_) return;

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        // Detect only after deactivation post-edit
        _tmpTitle ??= preset.Title;
        ImGui.InputTextWithHint("##name", "Preset Title...", ref _tmpTitle, 150);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (_tmpTitle != preset.Title)
                _data.RenamePreset(preset, _tmpTitle);
            // null temp
            _tmpTitle = null;
        }

        var endX = ImGui.GetWindowContentRegionMin().X + CkGui.GetWindowContentRegionWidth();
        endX -= ImGui.CalcTextSize($"{preset.Title.Length}/150").X + ImUtf8.FrameHeight + ImUtf8.ItemSpacing.X;
        ImGui.SameLine(endX);
        ColorFormatting();
        CkGui.ColorTextFrameAlignedInline($"{preset.Title.Length}/150", ImGuiColors.DalamudGrey2.ToUint());
        // Check for formatting errors
        var titleErr = Utils.ParseBBSeString(preset.Title, out bool hadError);
        if (hadError)
        {
            endX -= ImUtf8.FrameHeight;
            ImGui.SameLine(endX);
            CkGui.FramedHoverIconText(FAI.ExclamationTriangle, CkCol.TriStateCross.Uint());
            CkGui.AttachToolTip(titleErr.TextValue);
        }

        // Then Description
        _tmpDesc ??= preset.Description;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var pos = ImGui.GetCursorPos();
        ImGui.InputTextMultiline("##desc", ref _tmpDesc, 500, new Vector2(ImGui.GetContentRegionAvail().X, CkStyle.GetFrameRowsHeight(3)));
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (_tmpDesc != preset.Description)
            {
                preset.Description = _tmpDesc;
                _data.MarkPresetModified(preset);
            }
            // null temp
            _tmpDesc = null;
        }

        var boxSize = ImGui.GetItemRectSize();
        ImGui.SetCursorPos(pos + new Vector2(boxSize.X - ImGui.CalcTextSize($"{preset.Description.Length}/500").X - ImUtf8.FrameHeight - ImUtf8.ItemSpacing.X, boxSize.Y - ImUtf8.FrameHeight));
        ColorFormatting();
        CkGui.ColorTextFrameAlignedInline($"{preset.Description.Length}/500", ImGuiColors.DalamudGrey2.ToUint());
        // Check for formatting errors
        var descErr = Utils.ParseBBSeString(preset.Description, out bool descError);
        if (descError)
        {
            endX -= ImUtf8.FrameHeight;
            ImGui.SameLine(endX);
            CkGui.FramedHoverIconText(FAI.ExclamationTriangle, CkCol.TriStateCross.Uint());
            CkGui.AttachToolTip(descErr.TextValue);
        }
    }

    private void DrawStatusArrangement(LociPreset preset, float width)
    {
        var cols = Math.Min(preset.Statuses.Count + 1, (int)(width / IconSize.X));
        using var _ = ImRaii.Table("##statuses", cols, ImGuiTableFlags.SizingFixedFit);
        if (!_) return;

        // Setup the indexed columns for this table
        for (var i = 0; i < cols; i++)
            ImGui.TableSetupColumn($"iconColumn{i}");

        var lookup = LociData.Statuses.ToDictionary(s => s.GUID);
        var orderedStatuses = preset.Statuses.Where(lookup.ContainsKey).Select(guid => lookup[guid]).ToList();
        // Obtain the icons to draw out
        foreach (var (status, idx) in orderedStatuses.WithIndex())
        {
            if (idx % cols is 0)
                ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawStatusCell(preset, status, idx);
        }

        // Finally, draw an end-dummy on the next column
        ImGui.TableNextColumn();
        DrawDummyCell(preset, preset.Statuses.Count);
    }

    private void DrawStatusCell(LociPreset preset, LociStatus status, int idx)
    {
        var pos = ImGui.GetCursorPos();
        if (_draggedStatus == status.GUID)
        {
            var green = CkCol.TriStateCheck.Vec4Ref();
            var color = Gradient.Get(green, green with { W = green.W / 4 }, 500).ToUint();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
        }
        // Dummy invisible button
        ImGui.InvisibleButton($"##preset-status-cell-{status.GUID}", IconSize);
        // Indicate movable nature when hovered
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        // Identify this as a drag-drop source, attaching the GUID to its data
        AsDragDropSource(status, idx);
        AsDragDropTarget(preset, idx);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _postDrawActions.Enqueue(() => preset.Statuses.Remove(status.GUID));
        }


        if (LociIcon.TryGetGameIcon((uint)status.IconID, true, out var wrap))
        {
            ImGui.SetCursorPos(pos);
            ImGui.Image(wrap.Handle, IconSize);

            Utils.AttachTooltip(status);
        }
    }

    private void DrawDummyCell(LociPreset preset, int idx)
    {
        ImGui.InvisibleButton($"dummy-cell", new Vector2(IconSize.X, ImGui.GetContentRegionAvail().X));
        AsDragDropTarget(preset, idx);
    }

    private void AsDragDropSource(LociStatus status, int idx)
    {
        using var source = ImUtf8.DragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip);
        if (!source) return;

        CkGui.SetDragDropPayload(DRAGDROP_LABEL, status.GUID);
        _draggedStatus = status.GUID;
    }

    private void AsDragDropTarget(LociPreset preset, int statusIdx)
    {
        using var _ = ImRaii.DragDropTarget();
        if (!_) return;

        if (CkGui.AcceptDragDropPayload(DRAGDROP_LABEL, out Guid payload, ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect))
            _postDrawActions.Enqueue(() => ToNewPosition(preset, payload, statusIdx));
    }

    private void ToNewPosition(LociPreset preset, Guid toDrop, int newIdx)
    {
        var idx = preset.Statuses.IndexOf(toDrop);
        if (idx == -1 || idx == newIdx)
            return;

        preset.Statuses.RemoveAt(idx);
        if (newIdx >= preset.Statuses.Count)
            preset.Statuses.Add(toDrop);
        else
            preset.Statuses.Insert(newIdx, toDrop);
    }

    private unsafe void DrawTargetApplication(LociPreset preset)
    {
        if (!CharaWatcher.TryGetValue(Svc.Targets.Target?.Address ?? nint.Zero, out Character* chara))
        {
            using (ImRaii.Disabled())
                ImGui.Button("No Target Selected");
            return;
        }

        // We have a target, so get their sm
        var sm = _manager.GetOrCreateSM(chara);
        // If the manager is not ephemeral, simply draw the apply to target button.
        if (!sm.Ephemeral)
        {
            // Perform without any validation
            if (CkGui.IconTextButton(FAI.Crosshairs, $"Apply to {chara->NameString}", disabled: chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion)))
                sm.ApplyPreset(preset);
            CkGui.AttachToolTip($"Applies the status to the target actor!--SEP----COL--Accepts Players and Minions--COL--", ImGuiColors.DalamudOrange);
        }
        else
        {
            // reset the target binder if no longer part of the subset.
            if (!sm.EphemeralHosts.Contains(_selectedHost))
                _selectedHost = string.Empty;

            if (CkGuiUtils.StringCombo("##hosts", 100f, _selectedHost, out string newHost, sm.EphemeralHosts, "Select Host.."))
                _selectedHost = newHost;
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                _selectedHost = string.Empty;

            ImUtf8.SameLineInner();
            var buttonTxt = $"{(_selectedHost.Length is 0 ? "(No Host Chosen)" : $"Apply to Target ({_selectedHost})")}";
            // Sends an event to listeners of the actor address, the host it is intended for, and the tuple data being applied.
            if (CkGui.IconTextButton(FAI.PersonBurst, buttonTxt, disabled: _selectedHost.Length is 0))
            {
                Generic.Safe(() =>
                {
                    var toSend = LociData.Statuses.Where(s => preset.Statuses.Contains(s.GUID)).Select(s => s.ToTuple()).ToList();
                    _mediator.Publish(new ApplyToTargetMessage((nint)chara, _selectedHost, toSend));
                });
            }
        }
    }

    private void ColorFormatting()
    {
        CkGui.FramedHoverIconText(FAI.Code, LociCol.Gold.Uint());
        CkGui.AttachToolTip($"This supports formatting tags." +
            $"--NL----COL--Colors:--COL-- [color=red]...[/color], [color=5]...[/color]" +
            $"--NL----COL--Glow:--COL-- [glow=blue]...[/glow], [glow=7]...[/glow]" +
            $"--NL----COL--Italics:--COL-- [i]...[/i]" +
            $"--SEP--The following colors are available:" +
            $"--NL--{string.Join(", ", Enum.GetValues<XlDataUiColor>().Select(x => x.ToString()).Where(x => !x.StartsWith("_")))}" +
            $"--SEP--For extra color, look up numeric value with --COL--\"/xldata uicolor\"--COL-- command", ImGuiColors.DalamudViolet);
    }
}
