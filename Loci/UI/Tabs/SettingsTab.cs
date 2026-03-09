using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Services;
using Loci.Services.Mediator;

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

    public unsafe void DrawSettings()
    {
        CkGui.FontText("Functionality", Fonts.Default150Percent);
        var enabled = _config.Current.Enabled;
        if (ImGui.Checkbox($"Enable Module", ref enabled))
        {
            _config.Current.Enabled = enabled;
            _config.Save();
            _mediator.Publish(new EnabledStateChanged(enabled));
        }
        DrawIndentedEnables();

        CkGui.FontText("Limiters", Fonts.Default150Percent);
        var offInDuty = _config.Current.OffInDuty;
        if (ImGui.Checkbox("Disable in Duties/Instances", ref offInDuty))
        {
            _config.Current.OffInDuty = offInDuty;
            _config.Save();
        }

        var offInCombat = _config.Current.OffInCombat;
        if (ImGui.Checkbox("Disable in Combat", ref offInCombat))
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
        CkGui.AttachToolTip("If VFX are applied on Loci Status application");

        if (ImGui.Checkbox($"Restrict VFX", ref vfxLimited))
        {
            _config.Current.SheVfxRestricted = vfxLimited;
            _config.Save();
        }
        CkGui.AttachToolTip("Restricts Vfx to only friends, party and nearby actors");

        if (ImGui.Checkbox($"Fly/Popup Text", ref flyTextOn))
        {
            _config.Current.FlyText = flyTextOn;
            _config.Save();
        }

        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderInt("Fly/Popup Text Limit", ref flyTextLimit, 5, 20))
        {
            _config.Current.FlyTextLimit = flyTextLimit;
            _config.Save();
        }
        CkGui.AttachToolTip("How many Fly/Popup Texts can be active simultaneously.");
    }

    private void DrawMigrate()
    {
        if (!OtherDirectoryExists())
            return;

        ImGui.Separator();
        var shiftAndCtrlPressed = ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl;

        if (CkGui.IconTextButton(FAI.FileImport, "Import Statuses", disabled: !shiftAndCtrlPressed))
        {
            var statusFS = GetMigratableFile("MoodleFileSystem.json");
            var statuses = GetMigratableFile("DefaultConfig.json");
            if (File.Exists(statusFS) && File.Exists(statuses))
            {
                _logger.LogInformation($"Migrating from {statusFS}");
                try
                {
                    var defaultJson = JObject.Parse(File.ReadAllText(statuses));
                    _data.MoodleStatusMigration(defaultJson);
                    _statusFileSystem.MergeWithMigratableFile(statusFS);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to migrate statuses from {statuses}");
                }
            }
        }
        CkGui.AttachToolTip("Migrate all statuses to Loci." +
            "--SEP----COL--Must hold CTRL+SHIFT to execute.--COL--", ImGuiColors.DalamudOrange);

        if (CkGui.IconTextButton(FAI.FileImport, "Import Presets", disabled: !shiftAndCtrlPressed))
        {
            var presetFS = GetMigratableFile("PresetFileSystem.json");
            var presets = GetMigratableFile("DefaultConfig.json");
            if (File.Exists(presetFS) && File.Exists(presets))
            {
                _logger.LogInformation($"Migrating from {presetFS}");
                try
                {
                    var defaultJson = JObject.Parse(File.ReadAllText(presets));
                    _data.MoodlePresetMigration(defaultJson);
                    // Then update the FS.
                    _presetFileSystem.MergeWithMigratableFile(presetFS);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to migrate presets from {presets}");
                }
            }
        }
        CkGui.AttachToolTip("Migrate all presets to Loci." +
            "--SEP----COL--Must hold CTRL+SHIFT to execute.--COL--", ImGuiColors.DalamudOrange);
    }

    #region Helpers
    // Locate if we are able to migrate
    private string GetMigratableDirectoryPath()
    {
        var parentDir = Path.GetDirectoryName(FileProvider.Directory);
        if (parentDir is null)
            return string.Empty;
        return Path.Combine(parentDir, "Moodles");
    }

    private string GetMigratableFile(string fileName)
        => Path.Combine(GetMigratableDirectoryPath(), fileName);

    private bool OtherDirectoryExists()
        => Directory.Exists(GetMigratableDirectoryPath());
    #endregion Helpers
}
