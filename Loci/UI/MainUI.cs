using CkCommons.Gui;
using CkCommons.Raii;
using Dalamud.Bindings.ImGui;
using Loci.Data;
using Loci.Gui.Components;
using Loci.Services.Mediator;

namespace Loci.Gui;

// Primary Loci UI servicing all interactions with the Loci Module.
public class MainUI : WindowMediatorSubscriberBase
{
    // Note that if you ever change this width you will need to also adjust the display width for the account page display.
    public const float LOCI_UI_WIDTH = 600f;

    private readonly LociUITabs _tabMenu;
    private readonly MainConfig _config;
    private readonly StatusesTab _statusTab;
    private readonly PresetsTab _presetTab;
    private readonly ManagersTab _managersTab;
    private readonly SettingsTab _settingsTab;
    private readonly DebugTab _debugTab;
    private readonly IpcTesterTab _ipcTab;

    public MainUI(ILogger<MainUI> logger, LociMediator mediator, LociUITabs tabs,
        MainConfig config, StatusesTab statusTab, PresetsTab presetTab,
        ManagersTab managersTab, SettingsTab settingsTab, DebugTab debug, IpcTesterTab ipcTab)
        : base(logger, mediator, "Loci - Custom Status Control###Loci_LociUI")
    {
        _tabMenu = tabs;
        _config = config;
        _statusTab = statusTab;
        _presetTab = presetTab;
        _managersTab = managersTab;
        _settingsTab = settingsTab;
        _debugTab = debug;
        _ipcTab = ipcTab;


        this.PinningClickthroughFalse();
        this.SetBoundaries(new(800, 450), ImGui.GetIO().DisplaySize);        
        // Update the tab menu selection.
        _tabMenu.TabSelection = _config.Current.CurrentTab;
    }

    protected override void PreDrawInternal()
    { }

    protected override void PostDrawInternal()
    { }

    protected override void DrawInternal()
    {
        var width = CkGui.GetWindowContentRegionWidth();
        // Draw the tab bar ontop
        _tabMenu.Draw(width);

        using var _ = CkRaii.Child("selected", ImGui.GetContentRegionAvail());
        switch (_tabMenu.TabSelection)
        {
            case LociUITabs.SelectedTab.Statuses:
                _statusTab.DrawSection(_.InnerRegion);
                break;
            case LociUITabs.SelectedTab.Presets:
                _presetTab.DrawSection(_.InnerRegion);
                break;
            case LociUITabs.SelectedTab.Managers:
                _managersTab.DrawSection(_.InnerRegion);
                break;
            case LociUITabs.SelectedTab.Settings:
                _settingsTab.DrawSettings();
                break;
            case LociUITabs.SelectedTab.Logging:
                _debugTab.DrawLoggers();
                break;
            case LociUITabs.SelectedTab.IpcTester:
                _ipcTab.DrawSection();
                break;
        }
    }
}
