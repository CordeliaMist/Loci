using CkCommons;
using CkCommons.Gui;
using CkCommons.Gui.Utility;
using CkCommons.Raii;
using CkCommons.RichText;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using OtterGui.Extensions;
using OtterGui.Text;

namespace Loci.Gui;

public class DebugTab
{
    /// <summary> Displays the Debug section within the settings, where we can set our debug level </summary>
    private static readonly (string Label, LoggerType[] Flags)[] FlagGroups =
    {
        ("Essential", [LoggerType.Mediator, LoggerType.Framework, LoggerType.Objects]),
        ("Processing", [LoggerType.Memory, LoggerType.Processors, LoggerType.Updates, LoggerType.SheVfx]),
        ("Data", [LoggerType.Data, LoggerType.DataManagement, LoggerType.Events]),
        ("Ipc", [LoggerType.IpcProvider, LoggerType.Ipc]),
    };

    private readonly MainConfig _mainConfig;
    private readonly DDSDebugger _ddsDebug;
    private readonly SMDrawSystem _smDDS;

    public DebugTab(MainConfig config, DDSDebugger ddsDebug, SMDrawSystem smDDS)
    {
        _mainConfig = config;
        _ddsDebug = ddsDebug;
        _smDDS = smDDS;
    }

    public void DrawLoggers()
    {
        CkGui.FontText("Debug Configuration", Fonts.UidFont);

        // display the combo box for setting the log level we wish to have for our plugin
        if (CkGuiUtils.EnumCombo("Log Level", 200f, MainConfig.LogLevel, out var newValue, flags: CFlags.None))
        {
            MainConfig.LogLevel = newValue;
            _mainConfig.Save();
        }

        DrawFilters();

#if DEBUG
        ImGui.Spacing();
        ImGui.Separator();
        _ddsDebug.DrawDDSDebug("Status Manager DDS", _smDDS);

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Status Managers"))
        {
            foreach (var (name, manager) in LociManager.Managers)
                DrawActorSM(name, manager);
        }
#endif
    }

    private void DrawFilters()
    {
        if (ImGui.Button("Enable Recommended"))
        {
            MainConfig.LoggerFilters = LoggerType.Recommended;
            _mainConfig.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable All Filters"))
        {
            MainConfig.LoggerFilters = LoggerType.None;
            _mainConfig.Save();
        }

        // draw a collapsible tree node here to draw the logger settings:
        ImGui.Spacing();

        var height = ImUtf8.FrameHeight * FlagGroups.Length + ImGui.GetStyle().CellPadding.Y * (FlagGroups.Length * 2);
        using var _ = CkRaii.FramedChildPaddedW("access", ImGui.GetContentRegionAvail().X * .6f, height, 0, ImGui.GetColorU32(ImGuiCol.Separator), CkStyle.ChildRounding(), 2f);
        using var t = ImRaii.Table("##loggersTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV);
        if (!t) return;

        var logFilters = MainConfig.LoggerFilters;
        var flags = (ulong)MainConfig.LoggerFilters;
        ImGui.TableSetupColumn("##section");
        ImGui.TableSetupColumn("##values", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        foreach (var (label, flagGroup)in FlagGroups)
        {
            ImGui.TableNextColumn();
            CkGui.TextFrameAligned($"{label}:");
            ImGui.TableNextColumn();
            for (int i = 0; i < flagGroup.Length; i++)
            {
                var flag = flagGroup[i];
                bool flagState = (flags & (ulong)flag) != 0;
                if (ImGui.Checkbox(flag.ToString(), ref flagState))
                {
                    if (flagState)
                        flags |= (ulong)flag;
                    else
                        flags &= ~(ulong)flag;
                    // update the loggerFilters.
                    MainConfig.LoggerFilters = (LoggerType)flags;
                    _mainConfig.Save();
                }

                if (i < flagGroup.Length - 1)
                    ImGui.SameLine();
            }
        }
    }

    private void DrawActorSM(string name, ActorSM manager)
    {
        using var _ = ImRaii.TreeNode(name);
        if (!_) return;

        ImGui.Text("Owner Valid:");
        ImGui.SameLine();
        CkGui.ColorTextBool(manager.OwnerValid ? "Valid" : "Invalid", manager.OwnerValid);

        ImGui.Text("AddTextShown:");
        CkGui.ColorTextInline(string.Join(", ", manager.AddTextShown.Select(g => g.ToString())), ImGuiColors.DalamudViolet);

        ImGui.Text("RemTextShown:");
        CkGui.ColorTextInline(string.Join(", ", manager.RemTextShown.Select(g => g.ToString())), ImGuiColors.DalamudViolet);

        ImGui.Text("Ephemeral:");
        CkGui.ColorTextInline(manager.Ephemeral.ToString(), ImGuiColors.DalamudViolet);
        if (manager.Ephemeral)
        {
            using (ImRaii.PushIndent())
            {
                foreach (var host in manager.EphemeralHosts)
                    CkGui.ColorText(host, ImGuiColors.DalamudViolet);
            }
        }

        using (var locks = ImRaii.TreeNode("Active Locks"))
        {
            if (locks)
            {
                foreach (var (id, key) in manager.LockedStatuses)
                {
                    CkGui.ColorText(id.ToString(), ImGuiColors.DalamudYellow);
                    CkGui.TextInline(" -> Locked by key");
                    CkGui.ColorTextInline($"[{key}]", ImGuiColors.DalamudViolet);
                }
            }
        }

        using (var statuses = ImRaii.TreeNode("Active Statuses"))
        {
            if (statuses)
                DrawStatuses(name, manager.Statuses);
        }

        using (var vanillaStatuses = ImRaii.TreeNode("Game-Controlled Statuses"))
        {
            if (vanillaStatuses)
                DrawVanillaStatuses(name, manager.OwnerAddress);
        }
    }

    private void DrawStatuses(string id, IEnumerable<LociStatus> statuses)
    {
        using var _ = ImRaii.Table($"{id}-statuslist", 11, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!_) return;

        ImGui.TableSetupColumn("ID");
        ImGui.TableSetupColumn("IconID");
        ImGui.TableSetupColumn("Title");
        ImGui.TableSetupColumn("Description");
        ImGui.TableSetupColumn("VFX Path");
        ImGui.TableSetupColumn("Type");
        ImGui.TableSetupColumn("Modifiers");
        ImGui.TableSetupColumn("Stacks");
        ImGui.TableSetupColumn("Stack Steps");
        ImGui.TableSetupColumn("Chain Status");
        ImGui.TableSetupColumn("Chain Trigger");
        ImGui.TableHeadersRow();

        foreach (var status in statuses)
        {
            ImGui.TableNextColumn();
            CkGui.HoverIconText(FAI.InfoCircle, ImGuiColors.TankBlue.ToUint());
            CkGui.AttachTooltip(status.ID);
            ImGui.TableNextColumn();
            if (LociIcon.TryGetGameIcon((uint)status.IconID, false, out var wrap))
            {
                ImGui.Image(wrap.Handle, LociIcon.SizeFramed);
                CkGui.AttachTooltip($"{status.IconID}");
            }
            else
                ImGui.Text($"{status.IconID}");

            ImGui.TableNextColumn();
            CkRichText.Text(status.Title, 777);
            ImGui.TableNextColumn();
            ImGui.Dummy(new(200f, 0));
            CkRichText.Text(200f, status.Description, 777);
            ImGui.TableNextColumn();
            ImGui.Text($"{status.CustomFXPath}");
            ImGui.TableNextColumn();
            ImGui.Text($"{status.Type}");
            ImGui.TableNextColumn();
            ImGui.Text(string.Join("\n", status.Modifiers));
            ImGui.TableNextColumn();
            ImGui.Text($"{status.Stacks}");
            ImGui.TableNextColumn();
            ImGui.Text($"{status.StackSteps}");
            ImGui.TableNextColumn();
            ImGui.Text($"{status.ChainedGUID}");
            ImGui.TableNextColumn();
            ImGui.Text(status.ChainTrigger.ToString());
        }
    }

    private unsafe void DrawVanillaStatuses(string id, nint charaddr)
    {
        var chara = (Character*)charaddr;
        if (chara == null)
        {
            ImGui.Text("Character not rendered, statuses unknown.");
            return;
        }
        if (StatusList.CreateStatusListReference((nint)chara->GetStatusManager()) is not { } statusList)
        {
            ImGui.Text("Unknown status list.");
            return;
        }

        ImGui.Text($"Game Controlled Status Count: ");
        CkGui.ColorTextInline($"{statusList.Count(s => s.StatusId != 0)}", ImGuiColors.DalamudYellow);
        ImGui.SameLine();
        ImGui.Text(" ...with icons:");
        CkGui.ColorTextInline($"{statusList.Count(s => s.GameData.Value.Icon != 0)}", ImGuiColors.DalamudYellow);
        ImGui.Text($"Maximum Statuses: {statusList.Length}");

        using var t = ImRaii.Table($"{id}-VanillaStatusList", 8, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!t) return;

        ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.DefaultSort);
        ImGui.TableSetupColumn("ID");
        ImGui.TableSetupColumn("IconID");
        ImGui.TableSetupColumn("Title");
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Flags");
        ImGui.TableSetupColumn("Flag2");
        ImGui.TableSetupColumn("Description");
        ImGui.TableHeadersRow();

        foreach (var (status, index) in statusList.WithIndex())
        {
            ImGui.TableNextColumn();
            ImGui.Text($"{index}");
            ImGui.TableNextColumn();
            CkGui.HoverIconText(FAI.InfoCircle, ImGuiColors.TankBlue.ToUint());
            CkGui.AttachTooltip($"{status.StatusId}");
            ImGui.TableNextColumn();
            if (LociIcon.TryGetGameIcon(status.GameData.Value.Icon, false, out var wrap))
            {
                ImGui.Image(wrap.Handle, LociIcon.SizeFramed);
                CkGui.AttachTooltip($"{status.GameData.Value.Icon}");
            }
            else
                ImGui.Text($"{status.GameData.Value.Icon}");

            ImGui.TableNextColumn();
            ImGui.Text(status.GameData.Value.Name.ExtractText());
            ImGui.TableNextColumn();
            ImGui.Text($"{status.GameData.Value.StatusCategory}");
            ImGui.TableNextColumn();
            ImGui.Text($"{status.GameData.Value.Flags}");
            ImGui.TableNextColumn();
            ImGui.Text($"{status.GameData.Value.Flag2}");
            ImGui.TableNextColumn();
            ImGui.Text(status.GameData.Value.Description.ExtractText());
        }
    }
}
