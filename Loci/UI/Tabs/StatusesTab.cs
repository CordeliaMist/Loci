using CkCommons;
using CkCommons.Gui;
using CkCommons.Gui.Utility;
using CkCommons.Helpers;
using CkCommons.Raii;
using CkCommons.RichText;
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
using Loci.Interop;
using Loci.Processors;
using Loci.Services;
using OtterGui.Text;

namespace Loci.Gui;

public class StatusesTab : IDisposable
{
    private readonly StatusSelector _selector;
    private readonly LociData _data;
    private readonly LociManager _loci;

    private IconDataSelector _iconSelector;
    private SavedStatusesCombo _ownStatusCombo;
    private SavedPresetsCombo _ownPresetsCombo; 

    public StatusesTab(ILogger<StatusesTab> logger, StatusSelector selector, LociData data,
        LociManager loci, FavoritesConfig favorites)
    {
        _selector = selector;
        _data = data;
        _loci = loci;
        _iconSelector = new IconDataSelector(favorites);
        _ownStatusCombo = new SavedStatusesCombo(logger, loci, () => [ .. LociData.Statuses.OrderBy(s => s.Title) ]);
        _ownPresetsCombo = new SavedPresetsCombo(logger, loci, () => [ .. LociData.Presets.OrderBy(p => p.Title) ]);
        _selector.SelectionChanged += ResetTemps;
    }

    private static float SELECTOR_WIDTH => 250f * ImGuiHelpers.GlobalScale;
    private string? _tmpTitle = null;
    private string? _tmpDesc = null;
    private string? _tmpTimeStr = null;
    private string _chainDataFilter = string.Empty;
    private string _selectedHost = string.Empty;

    public void Dispose()
    {
        _selector.SelectionChanged -= ResetTemps;
    }

    private void ResetTemps(LociStatus? oldSel, LociStatus? newSel, in StatusSelector.State _)
    {
        _tmpTitle = null;
        _tmpDesc = null;
        _tmpTimeStr = null;
        _chainDataFilter = string.Empty;
        _selectedHost = string.Empty;
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
        if (_selector.Selected is not { } status)
        {
            CkGui.FontTextCentered("No Status Selected", Fonts.UidFont, ImGuiColors.DalamudGrey);
            return;
        }

        // Do some fancy way of displaying the LociStatus later.
        if (ImGui.Button("Apply"))
            LociManager.GetFromName(PlayerData.NameWithWorld).AddOrUpdate(status.PreApply());

        CkGui.FrameSeparatorV();
        DrawTargetApplication(status);

        // Store maxStacks before drawing further.
        var maxStacks = LociProcessor.IconStackCounts.TryGetValue((uint)status.IconID, out var count) ? (int)count : 1;
        var leftW = ImGui.CalcTextSize("Chaining Behaviorm").X + ImUtf8.FrameHeight;

        DrawEssentials(status, leftW);
        DrawChaining(status, leftW, maxStacks);
        DrawStacking(status, leftW, maxStacks);
        DrawDispelling(status, leftW);

        if (status.IconID != 0)
        {
            ImGui.SetCursorPos(minPos + new Vector2(_.InnerRegion.X - LociIcon.Size.X * 2.5f, 0));
            LociIcon.Draw((uint)status.IconID, status.Stacks, LociIcon.Size * 2);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
            {
                using (ImRaii.Tooltip())
                {
                    CkRichText.Text(200f, status.Description, 2);
                }
            }
        }
    }

    private unsafe void DrawTargetApplication(LociStatus status)
    {
        if (!CharaWatcher.TryGetValue(Svc.Targets.Target?.Address ?? nint.Zero, out Character* chara))
        {
            using (ImRaii.Disabled())
                ImGui.Button("No Target Selected");
            return;
        }

        // We have a target, so get their sm
        var sm = LociManager.GetFromChara(chara);
        // If the manager is not ephemeral, simply draw the apply to target button.
        if (!sm.Ephemeral)
        {
            // Perform without any validation
            if (CkGui.IconTextButton(FAI.Crosshairs, $"Apply to {chara->NameString}", disabled: chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion)))
                sm.AddOrUpdate(status.PreApply());
            CkGui.AttachToolTip($"Applies the status to the target actor!--SEP----COL--Accepts Players and Minions--COL--", ImGuiColors.DalamudOrange);
        }
        else
        {
            // reset the target binder if no longer part of the subset.
            if (!sm.EphemeralHosts.Contains(_selectedHost))
                _selectedHost = string.Empty;

            if (CkGuiUtils.StringCombo("##binders", 125f, _selectedHost, out string newHost, sm.EphemeralHosts, "Select Host..", CFlags.NoArrowButton))
                _selectedHost = newHost;
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                _selectedHost = string.Empty;

            ImUtf8.SameLineInner();
            var buttonTxt = $"{(_selectedHost.Length is 0 ? "(No Host Chosen)" : $"Apply to Target ({_selectedHost})")}";
            // Sends an event to listeners of the actor address, the host it is intended for, and the tuple data being applied.
            if (CkGui.IconTextButton(FAI.PersonBurst, buttonTxt, disabled: _selectedHost.Length is 0))
                IpcProvider.OnApplyToTarget((nint)chara, _selectedHost, status.ToTuple());
        }
    }

    private void DrawEssentials(LociStatus status, float leftW)
    {
        ImGui.Spacing();
        if (ImGui.BeginTable("##essentials", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
            ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

            // Essentials
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"ID:");
            CkGui.HelpText("Used in commands to apply loci.");
            
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
                ImGui.InputText($"##id-text", Encoding.UTF8.GetBytes(status.ID), ImGuiInputTextFlags.ReadOnly);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"Icon:");
            if (status.IconID is 0)
            {
                ImUtf8.SameLineInner();
                CkGui.FramedHoverIconText(FAI.ExclamationTriangle, CkCol.FavoriteHovered.Uint(), CkCol.Favorite.Uint());
                CkGui.AttachToolTip("You must select an icon!");
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var selinfo = Utils.GetIconData((uint)status.IconID);
            // Redo this in the way we want after we get it at least working.
            if (ImGui.BeginCombo("##sel", $"Icon: #{status.IconID} {selinfo?.Name}", ImGuiComboFlags.HeightLargest))
            {
                var cursor = ImGui.GetCursorPos();
                ImGui.Dummy(new Vector2(100, ImGuiHelpers.MainViewport.Size.Y * .3f));
                ImGui.SetCursorPos(cursor);
                if (_iconSelector.Draw(status))
                {
                    Svc.Logger.Warning($"Selected new Status Icon: {status.IconID}"); 
                    CleanupStatus(status);
                    _data.MarkStatusModified(status);
                }
                ImGui.EndCombo();
            }

            // If right clicked, we should clear the folders filters and refresh.
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                status.IconID = 0;
                CleanupStatus(status);
                _data.MarkStatusModified(status);
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"VFX path:");
            CkGui.HelpText("You may select a custom VFX to play upon application.");
            
            ImGui.TableNextColumn();
            DrawVfxCombo(status, ImGui.GetContentRegionAvail().X);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Title");
            ImUtf8.SameLineInner();
            ColorFormatting();
            var titleErr = Utils.ParseBBSeString(status.Title, out bool hadError);
            if (hadError)
                CkGui.HelpText(titleErr.TextValue, true, CkCol.TriStateCross.Uint());
            if (status.Title.Length is 0)
                CkGui.HelpText("Title cannot be empty!", true, ImGuiColors.DalamudYellow.ToUint());

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            // Detect only after deactivation post-edit
            _tmpTitle ??= status.Title;
            ImGui.InputText("##name", ref _tmpTitle, 150);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (_tmpTitle != status.Title)
                {
                    var prevTitle = status.Title;
                    status.Title = _tmpTitle;
                    _data.MarkStatusModified(status, prevTitle);
                }
                // null temp
                _tmpTitle = null;
            }

            ImGui.SameLine();
            CkGui.RightFrameAlignedColor($"{status.Title.Length}/150", ImGuiColors.DalamudGrey2.ToUint(), ImUtf8.ItemSpacing.X);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Description");
            ImUtf8.SameLineInner();
            ColorFormatting();
            var descErr = Utils.ParseBBSeString(status.Description, out bool descError);
            if (descError)
                CkGui.HelpText(descErr.TextValue, true, CkCol.TriStateCross.Uint());

            ImGui.TableNextColumn();
            _tmpDesc ??= status.Description;
            var descPos = ImGui.GetCursorPos();
            ImGui.InputTextMultiline("##desc", ref _tmpDesc, 500, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.CalcTextSize("A").Y * Math.Clamp(_tmpDesc.Split("\n").Length + 1, 2, 10)));
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (_tmpDesc != status.Description)
                {
                    status.Description = _tmpDesc;
                    _data.MarkStatusModified(status);
                }
                // null temp
                _tmpDesc = null;
            }
            var boxSize = ImGui.GetItemRectSize();
            ImGui.SetCursorPos(descPos + new Vector2(boxSize.X - ImGui.CalcTextSize($"{status.Description.Length}/500").X, boxSize.Y - ImUtf8.FrameHeight));
            CkGui.ColorTextFrameAlignedInline($"{status.Description.Length}/500", ImGuiColors.DalamudGrey2.ToUint());

            // Category
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Category:");

            ImGui.TableNextColumn();
            if (ImGui.RadioButton("Positive", status.Type is StatusType.Positive))
            {
                status.Type = StatusType.Positive;
                _data.MarkStatusModified(status);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Negative", status.Type is StatusType.Negative))
            {
                status.Type = StatusType.Negative;
                _data.MarkStatusModified(status);
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Special", status.Type is StatusType.Special))
            {
                status.Type = StatusType.Special;
                _data.MarkStatusModified(status);
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Duration:");
            if (status.TotalMilliseconds < 1 && !status.NoExpire)
            {
                ImUtf8.SameLineInner();
                CkGui.FramedHoverIconText(FAI.ExclamationTriangle, CkCol.FavoriteHovered.Uint(), CkCol.Favorite.Uint());
                CkGui.AttachToolTip("Duration must be at least 1 second");
            }

            ImGui.TableNextColumn();
            var isPerma = status.NoExpire;
            if (ImGui.Checkbox("Permanent", ref isPerma))
            {
                status.NoExpire = isPerma;
                _data.MarkStatusModified(status);
            }
            CkGui.AttachToolTip("Is no time limit should exist for this status");

            if (!status.NoExpire)
            {
                CkGui.FrameSeparatorV();
                _tmpTimeStr ??= TimeSpan.FromMilliseconds(status.TotalMilliseconds).ToTimeSpanStr();
                if (CkGui.IconInputText(FAI.HourglassHalf, "Duration", "2h13m6s..", ref _tmpTimeStr, 32, 125f, true))
                {
                    if (_tmpTimeStr != TimeSpan.FromMilliseconds(status.TotalMilliseconds).ToTimeSpanStr() && CkTimers.TryParseTimeSpan(_tmpTimeStr, out var newTime))
                    {
                        status.Days = newTime.Days;
                        status.Hours = newTime.Hours;
                        status.Minutes = newTime.Minutes;
                        status.Seconds = newTime.Seconds;
                        _data.MarkStatusModified(status);
                    }
                    // Clear the time string regardless
                    _tmpTimeStr = null;
                }
                CkGui.AttachToolTip($"The duration this status is applied for.");
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Status Behavior:");

            ImGui.TableNextColumn();
            var persistTime = status.Modifiers.Has(Modifiers.PersistExpireTime);
            if (ImGui.Checkbox("Persist Expire Time##noOverlapTime", ref persistTime))
            {
                status.Modifiers.Set(Modifiers.PersistExpireTime, persistTime);
                _data.MarkStatusModified(status);
            }
            CkGui.AttachToolTip("When enabled, any reapplication of this loci keeps it's expire time.");

            ImGui.EndTable();
        }
    }

    private void DrawChaining(LociStatus status, float leftW, int maxStacks)
    {
        ImGui.Spacing();
        using var t = ImRaii.Table("##chaining", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Chained Data:");

        ImGui.TableNextColumn();
        var typeWidth = ImGui.CalcTextSize("statusmm").X + ImUtf8.FrameHeight;
        if (CkGuiUtils.EnumCombo("##chainType", typeWidth, status.ChainedType, out var newType, flags: CFlags.NoArrowButton))
        {
            status.ChainedType = newType;
            _data.MarkStatusModified(status);
        }

        ImUtf8.SameLineInner();
        if (status.ChainedType is ChainType.Status)
        {
            if (_ownStatusCombo.Draw("##chainStatus", status.ChainedGUID, ImGui.GetContentRegionAvail().X, 1f, CFlags.HeightLargest))
            {
                if (!status.ChainedGUID.Equals(_ownStatusCombo.Current?.GUID))
                {
                    status.ChainedGUID = _ownStatusCombo.Current?.GUID ?? Guid.Empty;
                    _data.MarkStatusModified(status);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                status.ChainedGUID = Guid.Empty;
                _data.MarkStatusModified(status);
            }
        }
        else
        {
            if (_ownPresetsCombo.Draw("##chainPreset", status.ChainedGUID, ImGui.GetContentRegionAvail().X, 1f, CFlags.HeightLargest))
            {
                if (!status.ChainedGUID.Equals(_ownPresetsCombo.Current?.GUID))
                {
                    status.ChainedGUID = _ownPresetsCombo.Current?.GUID ?? Guid.Empty;
                    _data.MarkStatusModified(status);
                }
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                status.ChainedGUID = Guid.Empty;
                _data.MarkStatusModified(status);
            }
        }

        // The Chain Trigger segment
        if (status.ChainedGUID != Guid.Empty)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned("Chain Trigger:");

            ImGui.TableNextColumn();
            var comboW = ImGui.GetContentRegionAvail().X * (status.ChainTrigger is ChainTrigger.HitSetStacks ? .5f : 1f);
            // var options = maxStacks <= 1 ? Utils.ChainTypesNoStk : Utils.ChainTypes; Find a more stable way later.
            if (CkGuiUtils.EnumCombo("##chainTrigger", comboW, status.ChainTrigger, out var newTrigger, flags: CFlags.None))
            {
                status.ChainTrigger = newTrigger;
                _data.MarkStatusModified(status);
            }
            if (status.ChainTrigger is ChainTrigger.HitSetStacks)
            {
                ImUtf8.SameLineInner();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.BeginCombo("##triggerStk", StackText(status.StackToChain)))
                {
                    for (var i = 1; i <= maxStacks; i++)
                    {
                        if (ImGui.Selectable(StackText(i), status.StackToChain == i))
                        {
                            status.StackToChain = i;
                            _data.MarkStatusModified(status);
                        }
                    }
                    ImGui.EndCombo();
                }
            }
        }
    }

    // Stacking based paramaters.
    private void DrawStacking(LociStatus status, float leftW, int maxStacks)
    {
        if (maxStacks <= 1) return;

        ImGui.Spacing();
        using var t = ImRaii.Table("##stacking", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextColumn();
        CkGui.TextFrameAligned($"Initial Stacks:");
        CkGui.HelpText("The number of stacks initially applied.", true);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.BeginCombo("##initStk", StackText(status.Stacks)))
        {
            for (var i = 1; i <= maxStacks; i++)
            {
                if (ImGui.Selectable(StackText(i), status.Stacks == i))
                {
                    status.Stacks = i;
                    _data.MarkStatusModified(status);
                }
            }
            ImGui.EndCombo();
        }
        
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned($"Stack Steps:");
        CkGui.HelpText("If the status is reapplied, the stacks increment by this amount.", true);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
        if (ImGui.BeginCombo("##incStk", StackText(status.StackSteps)))
        {
            for (var i = 0; i <= maxStacks; i++)
            {
                if (ImGui.Selectable(StackText(i), status.StackSteps == i))
                {
                    status.StackSteps = i;
                    // Update modifiers.
                    status.Modifiers = (status.StackSteps > 0) ? status.Modifiers | Modifiers.StacksIncrease : status.Modifiers & ~Modifiers.StacksIncrease;
                    _data.MarkStatusModified(status);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        var stacksRoll = status.Modifiers.Has(Modifiers.StacksRollOver);
        if (ImGui.Checkbox("Roll Over Stacks##stkroll", ref stacksRoll))
        {
            status.Modifiers.Set(Modifiers.StacksRollOver, stacksRoll);
            _data.MarkStatusModified(status);
        }
        CkGui.AttachToolTip("When a stack reaches its cap, it starts over and counts up again.", true);

        // Handle chained status behavior if configured.
        if (status.ChainedGUID != Guid.Empty)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"Chaining Behavior:");
            CkGui.HelpText("How stacks from this status carry to the chained status.", true);
            
            ImGui.TableNextColumn();
            var moveStacks = status.Modifiers.Has(Modifiers.StacksMoveToChain);
            if (ImGui.Checkbox("Transfer Stacks", ref moveStacks))
            {
                status.Modifiers.Set(Modifiers.StacksMoveToChain, moveStacks);
                _data.MarkStatusModified(status);
            }

            ImGui.SameLine();
            var carryStacks = status.Modifiers.Has(Modifiers.StacksCarryToChain);
            if (ImGui.Checkbox("Carry Over Stacks", ref carryStacks))
            {
                status.Modifiers.Set(Modifiers.StacksCarryToChain, carryStacks);
                _data.MarkStatusModified(status);
            }
            CkGui.AttachToolTip("When the reapplication increase exceeds the max stacks, the remainder is added to the chained status.");

            ImGui.SameLine();
            var persist = status.Modifiers.Has(Modifiers.PersistAfterTrigger);
            if (ImGui.Checkbox("Persist", ref persist))
            {
                status.Modifiers.Set(Modifiers.PersistAfterTrigger, persist);
                _data.MarkStatusModified(status);
            }
            CkGui.AttachToolTip("Keeps this loci after chain is triggered.");
        }
    }

    private string StackText(int stacks) => stacks is 0 ? "No Stack Increase" : $"{stacks} {(stacks == 1 ? "Stack" : "Stacks")}";

    private void DrawDispelling(LociStatus status, float leftW)
    {
        if (!LociProcessor.DispelableIcons.Contains((uint)status.IconID))
            return;

        ImGui.Spacing();
        using var t = ImRaii.Table("##dispelling", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame);
        if (!t) return;
        
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, leftW);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);
        
        ImGui.TableNextColumn();
        CkGui.TextFrameAligned("Dispelable:");
        CkGui.HelpText("Makes the status dispelable. Functionality works when the option 'Statuses can be Esunad' is enabled in settings.");
        
        ImGui.TableNextColumn();
        var canDispel = status.Modifiers.Has(Modifiers.CanDispel);
        if (ImGui.Checkbox("##dispel", ref canDispel))
        {
            status.Modifiers.Set(Modifiers.CanDispel, canDispel);
            _data.MarkStatusModified(status);
        }
        
        if (canDispel)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"Allowed Dispeller:");
            CkGui.HelpText("An optional field to spesify who the status must be dispelled by, preventing others from doing so.", true);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("Dispeller##dispeller", "Player Name@World", ref status.Dispeller, 150);
            if (ImGui.IsItemDeactivatedAfterEdit())
                _data.MarkStatusModified(status);
        }
    }

    private void DrawVfxCombo(LociStatus status, float width)
    {
        using var combo = ImRaii.Combo("##vfx", $"VFX: {status.CustomFXPath}", ImGuiComboFlags.HeightLargest);
        if (combo)
        {
            for (var i = 0; i < LociProcessor.StatusEffectPaths.Count; i++)
                if (ImGui.Selectable(LociProcessor.StatusEffectPaths[i]))
                {
                    // Update it
                    status.CustomFXPath = LociProcessor.StatusEffectPaths[i];
                    _loci.Save();
                    IpcProvider.OnStatusModified(status, false);
                }
        }
        // Detect clearing.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            status.CustomFXPath = string.Empty;
            _loci.Save();
            IpcProvider.OnStatusModified(status, false);
        }
        CkGui.AttachToolTip("Select a custom VFX that summons upon application.");
    }

    // When a status is changed, it should be cleaned up to respect the new properties.
    private void CleanupStatus(LociStatus status)
    {
        Svc.Logger.Information($"Total IconStacks determined by LociProcessor: {LociProcessor.IconStackCounts.Count}");
        var maxStacks = LociProcessor.IconStackCounts.TryGetValue((uint)status.IconID, out var count) ? (int)count : 1;
        status.Stacks = maxStacks < 1 ? 1 : Math.Min(status.Stacks, maxStacks);
        status.StackSteps = maxStacks < 1 ? 0 : Math.Min(status.StackSteps, maxStacks);
        status.StackToChain = maxStacks;
        Svc.Logger.Information($"Max Stacks calculated: {maxStacks} | InitStacks: {status.Stacks} | Steps: {status.StackSteps}");
        // Ensure modifiers are correct.
        status.Modifiers = (status.StackSteps > 0)
            ? status.Modifiers | Modifiers.StacksIncrease : status.Modifiers & ~Modifiers.StacksIncrease;
        // Clear dispeller if not dispellable.
        if (!LociProcessor.DispelableIcons.Contains((uint)status.IconID))
        {
            status.Modifiers &= ~Modifiers.CanDispel;
            status.Dispeller = string.Empty;
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
