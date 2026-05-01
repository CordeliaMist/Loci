using CkCommons;
using CkCommons.Gui;
using CkCommons.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;
using OtterGui.Text;

namespace Loci.Gui;

public class SettingsTab
{
    private readonly ILogger<SettingsTab> _logger;
    private readonly LociMediator _mediator;
    private readonly MainConfig _config;
    private readonly LociData _data;
    private readonly LociManager _manager;
    private readonly StatusesFS _statusFileSystem;
    private readonly PresetsFS _presetFileSystem;

    public SettingsTab(ILogger<SettingsTab> logger, LociMediator mediator, MainConfig config, 
        LociData data, LociManager manager, StatusesFS statusFS, PresetsFS presetFS)
    {
        _logger = logger;
        _mediator = mediator;
        _config = config;
        _data = data;
        _manager = manager;
        _statusFileSystem = statusFS;
        _presetFileSystem = presetFS;
    }

    public unsafe void DrawSettings(Vector2 region)
    {
        var pos = ImGui.GetCursorPos();
        var enabled = _config.Current.Enabled;
        if (ImGui.Checkbox($"Enable Module", ref enabled))
        {
            _config.Current.Enabled = enabled;
            _config.Save();
            _mediator.Publish(new EnabledStateChangeMessage(enabled));
        }
        
        DrawIndentedEnables();

        var openOnStart = _config.Current.OpenOnStartup;
        if (ImGui.Checkbox("Open on Startup", ref openOnStart))
        {
            _config.Current.OpenOnStartup = openOnStart;
            _config.Save();
        }

        if (MoodlesWatcher.APIAvailable)
        {
            var compatibilityMode = _config.Current.MoodlesSupport;
            if (ImGui.Checkbox("Moodle Compatibility", ref compatibilityMode))
            {
#if DEBUG
                _config.Current.MoodlesSupport = compatibilityMode;
                _config.Save();
                _mediator.Publish(new CompatibilityModeChanged(compatibilityMode));
#endif
            }
            CkGui.HelpText("--COL--PLEASE READ CAREFULLY!--COL--" +
                "--SEP----COL--<!>--COL-- This is currently Non-Functional! --COL--<!>--COL--" +
                "--SEP--Enabling this allows for Moodles and Loci to both be enabled." +
                "--NL--Loci will offset its statuses to match any applied to Moodles." +
                "--NL----COL--This does not mean people with Moodles will see your Locis--COL--", ImGuiColors.DalamudOrange, true);
        }

        CkGui.FontText("Limiters", Fonts.Default150Percent);
        CkGui.FramedIconText(FAI.Ban);
        CkGui.TextFrameAlignedInline("Disable in:");
        ImGui.SameLine();
        var offInDuty = _config.Current.OffInDuty;
        if (ImGui.Checkbox("Duties/Instances", ref offInDuty))
        {
            _config.Current.OffInDuty = offInDuty;
            _config.Save();
        }

        ImGui.SameLine();
        var offInCombat = _config.Current.OffInCombat;
        if (ImGui.Checkbox("Combat", ref offInCombat))
        {
            _config.Current.OffInCombat = offInCombat;
            _config.Save();
        }

        var canEsuna = _config.Current.AllowEsuna;
        if (ImGui.Checkbox("Allow esunable statuses", ref canEsuna))
        {
            _config.Current.AllowEsuna = canEsuna;
            _config.Save();
        }

        var othersCanEsuna = _config.Current.OthersCanEsuna;
        if (ImGui.Checkbox("Others can Esuna your statuses", ref othersCanEsuna))
        {
            _config.Current.OthersCanEsuna = othersCanEsuna;
            _config.Save();
        }

        DrawMigrate();

#if DEBUG
        ImGui.Separator();
        MoodlesWatcher.DebugManagers();
#endif
        var buttonSize = new Vector2(150f * ImGuiHelpers.GlobalScale, 0);
        ImGui.SetCursorPos(pos + new Vector2(region.X - buttonSize.X, 0));
        using (ImRaii.Group())
        {
            using (ImRaii.PushColor(ImGuiCol.Button, 0xFFDA8972))
                if (ImGui.Button("Discord Support", buttonSize))
                    Util.OpenLink("https://discord.gg/QJy4zTqpMD");
            CkGui.AttachTooltip("Opens the Loci support discord", CkCol.TriStateCross.Vec4Ref());
            
            using (ImRaii.PushColor(ImGuiCol.Button, 0xFFD5449D))
                if (ImGui.Button("GitHub Page", buttonSize))
                    Util.OpenLink("https://github.com/CordeliaMist/Loci");
            CkGui.AttachTooltip($"View the GitHub repository for Loci");
        }
    }

    private void DrawIndentedEnables()
    {
        using var dis = ImRaii.Disabled(!_config.Current.Enabled);
        using var indent = ImRaii.PushIndent();

        var vfxOn = _config.Current.SheVfxEnabled;
        var vfxLimited = _config.Current.SheVfxRestricted;
        var flyTextOn = _config.Current.FlyText;
        var flyTextLimit = _config.Current.FlyTextLimit;

        if (ImGui.Checkbox($"Loci VFX", ref vfxOn))
        {
            _config.Current.SheVfxEnabled = vfxOn;
            _config.Save();
        }
        CkGui.AttachTooltip("If VFX are applied on Loci Status application");

        if (ImGui.Checkbox($"Restrict VFX", ref vfxLimited))
        {
            _config.Current.SheVfxRestricted = vfxLimited;
            _config.Save();
        }
        CkGui.AttachTooltip("Restricts Vfx to only friends, party and nearby actors");

        if (ImGui.Checkbox($"Fly/Popup Text", ref flyTextOn))
        {
            _config.Current.FlyText = flyTextOn;
            _config.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderInt("Limit", ref flyTextLimit, 5, 20))
        {
            _config.Current.FlyTextLimit = flyTextLimit;
            _config.Save();
        }
        CkGui.AttachTooltip("How many Fly/Popup Texts can be active simultaneously.");
    }

    private void DrawMigrate()
    {
        var oldDirExists = Directory.Exists(GetOldMigratableDirectoryPath());
        if (!oldDirExists)
            return;

        ImGui.Separator();
        CkGui.FontText("Data Import", Fonts.Default150Percent);
        var shiftAndCtrlPressed = ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl;

        if (oldDirExists)
        {
            if (CkGui.IconTextButton(FAI.FileImport, "Statuses (Moodles)", disabled: !shiftAndCtrlPressed))
            {
                try
                {
                    var statusFS = GetOldMigrationFilePath("MoodleFileSystem.json");
                    var statuses = GetOldMigrationFilePath("DefaultConfig.json");
                    if (File.Exists(statusFS) && File.Exists(statuses))
                    {
                        _logger.LogInformation($"Importing from {statusFS}");
                        var defaultJson = JObject.Parse(File.ReadAllText(statuses));
                        _data.MoodleStatusMigration(defaultJson);
                        _statusFileSystem.MergeWithMigratableFile(statusFS);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to Import statuses");
                }

            }
            CkGui.AttachTooltip("Import all statuses to Loci.--SEP----COL--Must hold CTRL+SHIFT to execute.--COL--", ImGuiColors.DalamudOrange);
            ImGui.SameLine();
            if (CkGui.IconTextButton(FAI.FileImport, "Presets (Moodles)", disabled: !shiftAndCtrlPressed))
            {
                try
                {
                    var presetFS = GetOldMigrationFilePath("PresetFileSystem.json");
                    var presets = GetOldMigrationFilePath("DefaultConfig.json");
                    if (File.Exists(presetFS) && File.Exists(presets))
                    {
                        _logger.LogInformation($"Importing from {presetFS}");
                        var defaultJson = JObject.Parse(File.ReadAllText(presets));
                        _data.MoodlePresetMigration(defaultJson);
                        // Then update the FS.
                        _presetFileSystem.MergeWithMigratableFile(presetFS);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to Import presets");
                }
            }
            CkGui.AttachTooltip("Import all presets to Loci.--SEP----COL--Must hold CTRL+SHIFT to execute.--COL--", ImGuiColors.DalamudOrange);
        }
    }

    #region Helpers
    // Locate if we are able to migrate
    private string GetOldMigratableDirectoryPath()
        => Path.GetDirectoryName(FileProvider.Directory) is { } path ? Path.Combine(path, "Moodles") : string.Empty;

    private string GetOldMigrationFilePath(string fileName)
        => Path.Combine(GetOldMigratableDirectoryPath(), fileName);
    #endregion Helpers
}
