using CkCommons;
using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.Gui.Components;
using Loci.Services.Mediator;
using LociApi.Helpers;
using LociApi.Ipc;
using OtterGui.Text;

namespace Loci.Gui;

// Primary Loci UI servicing all interactions with the Loci Module.
public class IpcTesterUI : WindowMediatorSubscriberBase
{
    // Note that if you ever change this width you will need to also adjust the display width for the account page display.
    private readonly MainConfig _config;
    private readonly IpcTesterTabs _tabMenu;
    private readonly IpcTesterRegistration _registry;
    private readonly IpcTesterStatusManagers _managers;
    private readonly IpcTesterStatuses _statuses;
    private readonly IpcTesterPresets _presets;
    private readonly IpcTesterEvents _events;

    private bool _subscribed = false;
    private (nint Addr, string Host, List<LociStatusInfo> Data) _latestApply;

    private readonly EventSubscriber<nint, string, List<LociStatusInfo>> _applyToTarget;

    public IpcTesterUI(ILogger<IpcTesterUI> logger, LociMediator mediator, MainConfig config,
        IpcTesterTabs tabs, IpcTesterRegistration registry, IpcTesterStatusManagers managers, 
        IpcTesterStatuses statuses, IpcTesterPresets presets, IpcTesterEvents events)
        : base(logger, mediator, "Loci - IPC Tester###Loci_IpcTesterUI")
    {
        _config = config;
        _tabMenu = tabs;
        _registry = registry;
        _managers = managers;
        _statuses = statuses;
        _presets = presets;
        _events = events;

        this.SetBoundaries(new(600, 350), ImGui.GetIO().DisplaySize);

        _applyToTarget = ApplyToTargetSent.Subscriber(Svc.PluginInterface, OnApplyToTarget);
        UnsubscribeFromIpc();
        _tabMenu.TabSelection = _config.Current.IpcTab;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _applyToTarget.Dispose();
    }

    private void OnApplyToTarget(nint targetPtr, string tag, List<LociStatusInfo> statuses)
        => _latestApply = (targetPtr, tag, statuses);

    private void SubscribeToIpc()
    {
        _applyToTarget.Enable();
        _registry.Subscribe();
        _managers.Subscribe();
        _statuses.Subscribe();
        _presets.Subscribe();
        _events.Subscribe();
        _subscribed = true;
    }

    private void UnsubscribeFromIpc()
    {
        _applyToTarget.Disable();
        _registry.Unsubscribe();
        _managers.Unsubscribe();
        _statuses.Unsubscribe();
        _presets.Unsubscribe();
        _events.Unsubscribe();
        _subscribed = false;
    }

    protected override void PreDrawInternal()
    { }

    protected override void PostDrawInternal()
    { }

    public override void OnClose()
        => UnsubscribeFromIpc();

    protected override void DrawInternal()
    {
        var width = CkGui.GetWindowContentRegionWidth();
        // Draw the tab bar ontop
        _tabMenu.Draw(width);

        // Mid Section
        if (CkGui.IconTextButton(FAI.Plug, "Subscribe to IPC", disabled: _subscribed))
            SubscribeToIpc();
        CkGui.AttachTooltip("THIS IS FOR TESTING PURPOSES ONLY IN THE IPC TESTER TAB." +
            "--SEP--LociIpc is already currently active and running!");
        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.PowerOff, "Unsubscribe from IPC", disabled: !_subscribed))
            UnsubscribeFromIpc();
        CkGui.AttachTooltip("THIS IS FOR TESTING PURPOSES ONLY IN THE IPC TESTER TAB." +
            "--SEP--LociIpc is already currently active and running!");

        ImGui.Separator();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 10f);
        ImGui.Text("Version");
        var (major, minor) = new ApiVersion(Svc.PluginInterface).Invoke();
        CkGui.ColorTextInline($"({major}.{minor})", ImGuiColors.DalamudYellow);

        CkGui.FrameSeparatorV();
        ImGui.Text("Enabled?:");
        var isEnabled = new IsEnabled(Svc.PluginInterface).Invoke();
        CkGui.BoolIcon(isEnabled);

        LatestTargetApply();

        using var content = CkRaii.Child("selected-ipc", ImGui.GetContentRegionAvail());
        switch (_tabMenu.TabSelection)
        {
            case IpcTesterTabs.SelectedTab.Registry:
                _registry.Draw();
                break;
            case IpcTesterTabs.SelectedTab.StatusManagers:
                _managers.Draw();
                break;
            case IpcTesterTabs.SelectedTab.Statuses:
                _statuses.Draw();
                break;
            case IpcTesterTabs.SelectedTab.Presets:
                _presets.Draw();
                break;
            case IpcTesterTabs.SelectedTab.Events:
                _events.Draw();
                break;
        }
    }

    private void LatestTargetApply()
    {
        ImGui.Text("Last ApplyStatuses:");
        if (_latestApply.Addr == nint.Zero)
        {
            CkGui.ColorTextInline("None requested...", CkCol.TriStateCross.Uint());
            return;
        }

        using var ident = ImRaii.PushIndent();
        ImGui.Text("Address:");
        CkGui.ColorTextInline($"{_latestApply.Addr:X}", ImGuiColors.DalamudViolet);
        ImGui.Text("TargetHost:");
        CkGui.ColorTextInline(_latestApply.Host, ImGuiColors.DalamudViolet);
        CkGui.TextFrameAligned("Status Info:");
        ImGui.SameLine();
        using var iconGroup = ImRaii.Group();

        for (var i = 0; i < _latestApply.Data.Count; i++)
        {
            if (_latestApply.Data[i].IconID is 0)
                continue;

            LociIcon.Draw(_latestApply.Data[i].IconID, _latestApply.Data[i].Stacks, LociIcon.SizeFramed);
            Utils.AttachTooltip(_latestApply.Data[i], _latestApply.Data, []);

            if (i < _latestApply.Data.Count)
                ImUtf8.SameLineInner();
        }
    }

    internal static void DrawIpcRowStart(string label, string info)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(info);
        ImGui.TableNextColumn();
    }
}
