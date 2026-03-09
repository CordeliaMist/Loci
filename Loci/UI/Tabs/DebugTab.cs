using CkCommons;
using CkCommons.Gui;
using CkCommons.Gui.Utility;
using CkCommons.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using OtterGui.Extensions;
using OtterGui.Text;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;

namespace Loci.Gui;

public class DebugTab
{
    /// <summary> Displays the Debug section within the settings, where we can set our debug level </summary>
    private static readonly (string Label, LoggerType[] Flags)[] FlagGroups =
    {
        ("Essential", [ LoggerType.Mediator, LoggerType.Framework, LoggerType.Objects]),
        ("Processing", [ LoggerType.Memory, LoggerType.Processors, LoggerType.Updates, LoggerType.SheVfx ]),
        ("Data", [ LoggerType.Data, LoggerType.DataManagement ]),
        ("Ipc", [ LoggerType.IpcProvider, LoggerType.Ipc ]),
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

        ImGui.Spacing();
        ImGui.Separator();
        _ddsDebug.DrawDDSDebug("Status Manager DDS", _smDDS);
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
}
