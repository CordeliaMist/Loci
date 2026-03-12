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
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Loci.Combos;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;
using Lumina.Excel.Sheets;
using NAudio.SoundFont;
using OtterGui.Extensions;
using OtterGui.Text;
using System.Threading;
using TerraFX.Interop.Windows;
using static FFXIVClientStructs.FFXIV.Client.Game.StatusManager.Delegates;

namespace Loci.Gui;

public class LociEventsTab : IDisposable
{
    private static float SELECTOR_WIDTH => 250f * ImGuiHelpers.GlobalScale;

    private readonly LociMediator _mediator;
    private readonly EventsSelector _selector;
    private readonly LociEventData _data;
    private readonly LociManager _manager;

    private SavedStatusesCombo _ownStatusCombo;
    private SavedPresetsCombo _ownPresetsCombo;
    private JobFlagsCombo _jobCombo;
    private IconDataSelector _iconSelector;
    private EmoteCombo _emoteCombo;
    private TerritoryCombo _territoryCombo;
    private OnlineStatusCombo _onlineStatusCombo;

    public LociEventsTab(ILogger<LociEventsTab> logger, LociMediator mediator,
        FavoritesConfig favorites, EventsSelector selector, LociEventData data, LociManager manager)
    {
        _mediator = mediator;
        _selector = selector;
        _data = data;
        _manager = manager;

        _ownStatusCombo = new SavedStatusesCombo(logger, manager, () => [.. LociData.Statuses.OrderBy(s => s.Title)]);
        _ownPresetsCombo = new SavedPresetsCombo(logger, manager, () => [.. LociData.Presets.OrderBy(p => p.Title)]);
        _jobCombo = new JobFlagsCombo(logger, 1.0f);
        _iconSelector = new IconDataSelector(favorites);
        _emoteCombo = new EmoteCombo(logger, 1.0f);
        _territoryCombo = new TerritoryCombo(logger);
        _onlineStatusCombo = new OnlineStatusCombo(logger, 1.0f);

        _selector.SelectionChanged += ResetTemps;
    }

    private string? _tmpTitle = null;
    private string? _tmpDesc = null;
    private short _tmpGearset = short.MaxValue;
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
        DrawEventContents();
    }

    private void DrawEventContents()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 8f);
        using var _ = CkRaii.Child("selected", ImGui.GetContentRegionAvail());
        if (!_) return;

        var minPos = ImGui.GetCursorPos();
        if (_selector.Selected is not { } sel)
        {
            CkGui.FontTextCentered("No Event Selected", Fonts.UidFont, ImGuiColors.DalamudGrey);
            return;
        }

        // Draw out the enabled button and priority setter
        var isSet = sel.Enabled;
        if (ImGui.Checkbox("Enabled", ref isSet))
        {
            sel.Enabled = isSet;
            _data.MarkEventModified(sel);
        }
        CkGui.AttachToolTip("If this event is being actively monitored");

        ImGui.SameLine();
        var priority = sel.Priority;
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.DragInt("Priority", ref priority, 1.0f, 0, 100, "%d"))
            sel.Priority = priority;
        CkGui.AttachToolTip("The priority of this event");

        // Title, description, and event type selection.
        var leftW = ImGui.CalcTextSize("Detect Emotemmm").X;
        DrawPrimary(sel, leftW);

        switch (sel.EventType)
        {
            case LociEventType.JobChange:
                DrawJobBased(sel, leftW);
                break;
            case LociEventType.GameBuffDebuff:
                DrawBuffDebuffBased(sel, leftW);
                break;
            case LociEventType.Emote:
                DrawEmoteBased(sel, leftW);
                break;
            case LociEventType.ZoneBased:
                DrawAreaBased(sel, leftW);
                break;
            case LociEventType.OnlineStatus:
                DrawStatusBased(sel, leftW);
                break;
            case LociEventType.TimeOfDay:
                DrawTimeBased(sel, leftW);
                break;
        }
    }

    private void DrawPrimary(LociEvent sel, float leftW)
    {
        using var _ = ImRaii.Table("event-main", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!_) return;
        
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        // Title
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Title");
        ImUtf8.SameLineInner();
        Utils.ShowFormattingInfo();
        var titleErr = Utils.ParseBBSeString(sel.Title, out bool hadError);
        if (hadError)
            CkGui.HelpText(titleErr.TextValue, true, CkCol.TriStateCross.Uint());
        if (sel.Title.Length is 0)
            CkGui.HelpText("Title cannot be empty!", true, ImGuiColors.DalamudYellow.ToUint());

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        // Detect only after deactivation post-edit
        _tmpTitle ??= sel.Title;
        ImGui.InputText("##name", ref _tmpTitle, 150);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (_tmpTitle != sel.Title)
            {
                var prevTitle = sel.Title;
                sel.Title = _tmpTitle;
                _data.MarkEventModified(sel, prevTitle);
            }
            _tmpTitle = null;
        }
        
        ImGui.SameLine();
        CkGui.RightFrameAlignedColor($"{sel.Title.Length}/150", ImGuiColors.DalamudGrey2.ToUint(), ImUtf8.ItemSpacing.X);

        // Then Description
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Description");
        ImUtf8.SameLineInner();
        Utils.ShowFormattingInfo();
        var descErr = Utils.ParseBBSeString(sel.Description, out bool descError);
        if (descError)
            CkGui.HelpText(descErr.TextValue, true, CkCol.TriStateCross.Uint());

        ImGui.TableNextColumn();
        _tmpDesc ??= sel.Description;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var pos = ImGui.GetCursorPos();
        ImGui.InputTextMultiline("##desc", ref _tmpDesc, 500, new Vector2(ImGui.GetContentRegionAvail().X, CkStyle.GetFrameRowsHeight(2)));
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (_tmpDesc != sel.Description)
            {
                sel.Description = _tmpDesc;
                _data.MarkEventModified(sel);
            }
            // null temp
            _tmpDesc = null;
        }
        var boxSize = ImGui.GetItemRectSize();
        ImGui.SetCursorPos(pos + new Vector2(boxSize.X - ImGui.CalcTextSize($"{sel.Description.Length}/500").X, boxSize.Y - ImUtf8.FrameHeight));
        CkGui.ColorTextFrameAligned($"{sel.Description.Length}/500", ImGuiColors.DalamudGrey2.ToUint());

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Reaction:");
        CkGui.HelpText("What loci data to apply when the condition is met.", true);

        ImGui.TableNextColumn();
        var typeWidth = ImGui.CalcTextSize("statusmm").X + ImUtf8.FrameHeight;
        if (CkGuiUtils.EnumCombo("##reactType", typeWidth, sel.ReactionType, out var newReact, [ChainType.Status, ChainType.Preset], flags: CFlags.NoArrowButton))
        {
            sel.ReactionType = newReact;
            _data.MarkEventModified(sel);
        }

        ImUtf8.SameLineInner();
        if (sel.ReactionType is ChainType.Status)
        {
            if (_ownStatusCombo.Draw("##chainStatus", sel.ReactionGUID, ImGui.GetContentRegionAvail().X, 1f, CFlags.HeightLargest))
            {
                if (!sel.ReactionGUID.Equals(_ownStatusCombo.Current?.GUID))
                {
                    sel.ReactionGUID = _ownStatusCombo.Current?.GUID ?? Guid.Empty;
                    _data.MarkEventModified(sel);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                sel.ReactionGUID = Guid.Empty;
                _data.MarkEventModified(sel);
            }
        }
        else
        {
            if (_ownPresetsCombo.Draw("##chainPreset", sel.ReactionGUID, ImGui.GetContentRegionAvail().X, 1f, CFlags.HeightLargest))
            {
                if (!sel.ReactionGUID.Equals(_ownPresetsCombo.Current?.GUID))
                {
                    sel.ReactionGUID = _ownPresetsCombo.Current?.GUID ?? Guid.Empty;
                    _data.MarkEventModified(sel);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                sel.ReactionGUID = Guid.Empty;
                _data.MarkEventModified(sel);
            }
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Event Type:");
        ImGui.TableNextColumn();
        if (CkGuiUtils.EnumCombo("##eventType", ImGui.GetContentRegionAvail().X, sel.EventType, out var newType, flags: CFlags.None))
        {
            sel.EventType = newType;
            _data.MarkEventModified(sel);
        }
    }

    private void DrawJobBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        using var t = ImRaii.Table("##emote", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Detection:");
        CkGui.HelpText("When switching to the defined Job/GearsetIdx, this event is triggered.");

        ImGui.TableNextColumn();
        var dispText = sel.GearsetIdx == -1 ? "Jobs" : "Gearset";
        if (CkGui.IconTextButton(FAI.Cog, dispText))
        {
            if (sel.GearsetIdx == -1)
            {
                sel.GearsetIdx = 1;
                sel.IndicatedID = uint.MaxValue;
            }
            else
            {
                sel.GearsetIdx = -1;
                sel.IndicatedID = 0;
            }
            _data.MarkEventModified(sel);
        }
        // Then the combo
        ImUtf8.SameLineInner();
        var width = ImGui.GetContentRegionAvail().X;
        if (sel.GearsetIdx != -1)
        {
            if (_tmpGearset == short.MaxValue)
                _tmpGearset = sel.GearsetIdx;
            ImGui.SetNextItemWidth(width);
            ImUtf8.InputScalar("##gearsetIdx", ref _tmpGearset);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                // Clamp selection
                _tmpGearset = Math.Clamp(_tmpGearset, (short)1, (short)100);
                if (_tmpGearset != sel.GearsetIdx)
                {
                    sel.GearsetIdx = _tmpGearset;
                    _data.MarkEventModified(sel);
                }
                _tmpGearset = short.MaxValue;
            }
        }
        else
        {
            if (_jobCombo.Draw("##jobflags", width, ref sel.JobFlags))
                _data.MarkEventModified(sel);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                sel.JobFlags = 0;
                _data.MarkEventModified(sel);
            }
            CkGui.AttachToolTip("When switching to these jobs, this event will trigger.");
        }
    }

    private void DrawBuffDebuffBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        CkGui.ColorTextCentered("Currently WIP", CkCol.TriStateCross.Vec4Ref());
        using var t = ImRaii.Table("##buffdebuff", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Detect Icon:");;
        if (sel.IndicatedID > 0)
            CkGui.ColorTextFrameAlignedInline($"#{sel.IndicatedID}", ImGuiColors.DalamudGrey2);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var selinfo = Utils.GetIconData(sel.IndicatedID);
        // Redo this in the way we want after we get it at least working.
        if (ImGui.BeginCombo("##sel", $"{selinfo?.Name}", ImGuiComboFlags.HeightLargest))
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.Dummy(new Vector2(100, ImGuiHelpers.MainViewport.Size.Y * .3f));
            ImGui.SetCursorPos(cursor);
            if (_iconSelector.Draw(sel))
            {
                Svc.Logger.Verbose($"Selected new Status Icon: {sel.IndicatedID}");
                _data.MarkEventModified(sel);
            }
            ImGui.EndCombo();
        }
        // If right clicked, we should clear the folders filters and refresh.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            sel.IndicatedID = 0;
            _data.MarkEventModified(sel);
        }
    }

    private void DrawEmoteBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        using var t = ImRaii.Table("##emote", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Detect Emote:");
        
        ImGui.TableNextColumn();
        if (sel.IndicatedID > 0 && GameDataSvc.EmoteData.TryGetValue((ushort)sel.IndicatedID, out var emoteData))
        {
            var image = Svc.Texture.GetFromGameIcon(emoteData.IconId).GetWrapOrEmpty();
            ImGui.Image(image.Handle, new(ImUtf8.FrameHeight));
            emoteData.AttachTooltip(image);
            ImUtf8.SameLineInner();
        }
        if (_emoteCombo.Draw("##emote-sel", sel.IndicatedID, ImGui.GetContentRegionAvail().X, 1.0f))
        {
            sel.IndicatedID = _emoteCombo.Current.RowId;
            _data.MarkEventModified(sel);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            sel.IndicatedID = uint.MaxValue;
            _data.MarkEventModified(sel);
        }
        
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Direction:");
        ImUtf8.SameLineInner();
        CkGui.FramedHoverIconText(FAI.QuestionCircle, ImGuiColors.TankBlue.ToUint(), ImGui.GetColorU32(ImGuiCol.TextDisabled));
        CkGui.AttachToolTip(
            "--COL--[Self]:--COL-- Emote is from you" +
            "--NL----COL--[Self ⇒ Others]:--COL-- You used an emote on someone" +
            "--NL----COL--[Others]:--COL-- Someone else used an emote" +
            "--NL----COL--[Others ⇒ Self]:--COL-- Done by someone else, and the target WAS you." +
            "--NL----COL--[Any]--COL-- Ignores Direction.", LociCol.Gold.Vec4Ref());

        ImGui.TableNextColumn();
        if (CkGuiUtils.EnumCombo("##direction", ImGui.GetContentRegionAvail().X, sel.Direction, out var newVal, _ => _.ToDisplayName(), flags: CFlags.None))
        {
            sel.Direction = newVal;
            if (newVal is (KnownDirection.Self or KnownDirection.Any))
                sel.WhitelistedName = string.Empty;
            _data.MarkEventModified(sel);
        }

        if (sel.Direction is not (KnownDirection.Self or KnownDirection.Any))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Mark \"Other\":");
            CkGui.HelpText("Enforce other to be a spesific target." +
                "--SEP--This supports PlayerName@World and PlayerNames PetName format", true);

            var nameStr = sel.WhitelistedName;
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("##whitelisted", "John Loci@Cactuar / John Locis Mameshiba..", ref nameStr, 68);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                sel.WhitelistedName = nameStr;
                _data.MarkEventModified(sel);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                sel.WhitelistedName = string.Empty;
                _data.MarkEventModified(sel);
            }
            CkGui.AttachToolTip("Defines the --COL--Target--COL----SEP--Leaving this blank allows anyone.", LociCol.Gold.Vec4Ref());
        }
    }

    private void DrawAreaBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        using var t = ImRaii.Table("##zonebased", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Zone / Area:");

        ImGui.TableNextColumn();
        var dispText = sel.IsZoneBased() ? "Territory" : "Content Type";
        if (CkGui.IconTextButton(FAI.MapSigns, dispText))
        {
            if (sel.IsZoneBased())
            {
                sel.IntendedUse = IntendedUseEnum.Town;
                sel.IndicatedID = uint.MaxValue;
            }
            else
            {
                sel.IntendedUse = IntendedUseEnum.UNK;
                sel.IndicatedID = 0;
            }
            _data.MarkEventModified(sel);
        }
        ImUtf8.SameLineInner();
        var width = ImGui.GetContentRegionAvail().X;
        if (sel.IsZoneBased())
        {
            if (_territoryCombo.Draw((ushort)sel.IndicatedID, width))
            {
                sel.IndicatedID = _territoryCombo.Current.Key;
                _data.MarkEventModified(sel);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                sel.IndicatedID = uint.MaxValue;
                _data.MarkEventModified(sel);
            }
            CkGui.AttachToolTip("The required territory to match. (Working on polishing this)");
        }
        else
        {
            if (CkGuiUtils.EnumCombo("##usage", width, sel.IntendedUse, out var newUse, defaultText: "Related Content..", flags: CFlags.None))
            {
                sel.IntendedUse = newUse;
                _data.MarkEventModified(sel);
            }
            CkGui.AttachToolTip("The required Related Content Area to match.");
        }
    }

    private void DrawStatusBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        using var t = ImRaii.Table("##onlineStatus", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("For Status:");
        if (sel.IndicatedID > 0 && GameDataSvc.OnlineStatus.TryGetValue((byte)sel.IndicatedID, out var statusData))
        {
            // Draw it out
            var image = Svc.Texture.GetFromGameIcon(sel.IndicatedID).GetWrapOrEmpty();
            ImUtf8.SameLineInner();
            ImGui.Image(image.Handle, new(ImUtf8.FrameHeight));
            CkGui.AttachToolTip(statusData.Name);
        }

        ImGui.TableNextColumn();
        if (_onlineStatusCombo.Draw("##online-status", sel.IndicatedID, ImGui.GetContentRegionAvail().X, 1.0f))
        {
            sel.IndicatedID = _onlineStatusCombo.Current.RowId;
            _data.MarkEventModified(sel);
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            sel.IndicatedID = uint.MaxValue;
            _data.MarkEventModified(sel);
        }
    }

    private void DrawTimeBased(LociEvent sel, float leftW)
    {
        ImGui.Spacing();
        CkGui.ColorTextCentered("WIP...", CkCol.TriStateCross.Vec4Ref());
    }
}
