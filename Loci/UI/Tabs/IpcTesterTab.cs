using CkCommons;
using CkCommons.Gui;
using CkCommons.RichText;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc;
using Loci.Data;
using Loci.Interop;
using Loci.Services.Mediator;
using OtterGui.Text;

namespace Loci.Gui;

public class IpcTesterTab
{
    private readonly ILogger<IpcTesterTab> _logger;
    private readonly LociMediator _mediator;
    private readonly IpcProvider _ipc;
    private readonly LociManager _manager;
    private readonly IpcTesterRegistration _testerRegistration;
    private readonly IpcTesterStatusManagers _testerManagers;
    private readonly IpcTesterStatuses _testerStatuses;
    private readonly IpcTesterPresets _testerPresets;

    // Common calls.
    private static ICallGateSubscriber<int> _apiVersion;
    private static ICallGateSubscriber<nint, string, LociStatusInfo, object> _onTargetApplyStatus;
    private static ICallGateSubscriber<nint, string, List<LociStatusInfo>, object> _onTargetApplyStatuses;

    public IpcTesterTab(ILogger<IpcTesterTab> logger, LociMediator mediator,
        IpcProvider ipc, LociManager manager, IpcTesterRegistration testerRegistration, 
        IpcTesterStatusManagers testerManagers, IpcTesterStatuses testerStatuses, 
        IpcTesterPresets testerPresets)
    {
        _logger = logger;
        _mediator = mediator;
        _ipc = ipc;
        _manager = manager;
        _testerRegistration = testerRegistration;
        _testerManagers = testerManagers;
        _testerStatuses = testerStatuses;
        _testerPresets = testerPresets;

        _apiVersion = Svc.PluginInterface.GetIpcSubscriber<int>("Loci.GetApiVersion");
        _onTargetApplyStatus = Svc.PluginInterface.GetIpcSubscriber<nint, string, LociStatusInfo, object>("Loci.OnTargetApplyStatus");
        _onTargetApplyStatuses = Svc.PluginInterface.GetIpcSubscriber<nint, string, List<LociStatusInfo>, object>("Loci.OnTargetApplyStatuses");
    }

    private bool _subscribed = false;
    private (nint Addr, string RequestedHost, LociStatusInfo Data) _lastSingleRequest = (nint.Zero, string.Empty, default);
    private (nint Addr, string RequestedHost, List<LociStatusInfo> Data) _lastBulkRequest = (nint.Zero, string.Empty, []);

    private void SubscribeToIpc()
    {
        _onTargetApplyStatus.Subscribe(OnApplyStatusRequest);
        _onTargetApplyStatuses.Subscribe(OnApplyStatusesRequest);
        _testerRegistration.Subscribe();
        _testerManagers.Subscribe();
        _testerStatuses.Subscribe();
        _testerPresets.Subscribe();
        _subscribed = true;
    }

    private void UnsubscribeFromIpc()
    {
        _onTargetApplyStatus.Unsubscribe(OnApplyStatusRequest);
        _onTargetApplyStatuses.Unsubscribe(OnApplyStatusesRequest);
        _testerRegistration.Unsubscribe();
        _testerManagers.Unsubscribe();
        _testerStatuses.Unsubscribe();
        _testerPresets.Unsubscribe();
        _subscribed = false;
    }

    private void OnApplyStatusRequest(nint targetPtr, string tag, LociStatusInfo status)
        => _lastSingleRequest = (targetPtr, tag, status);

    private void OnApplyStatusesRequest(nint targetPtr, string tag, List<LociStatusInfo> statuses)
        => _lastBulkRequest = (targetPtr, tag, statuses);

    public void DrawSection()
    {
        if (CkGui.IconTextButton(FAI.Plug, "Subscribe to IPC", disabled: _subscribed))
            SubscribeToIpc();
        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.PowerOff, "Unsubscribe from IPC", disabled: !_subscribed))
            UnsubscribeFromIpc();

        ImGui.Separator();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 10f);
        using var _ = ImRaii.Child("ipc-contents", ImGui.GetContentRegionAvail());
        ImGui.Text("Version");
        CkGui.ColorTextInline($"{_apiVersion.InvokeFunc()}", ImGuiColors.DalamudYellow);
        LatestTargetApply();
        LatestTargetApplyBulk();

        _testerRegistration.Draw();
        _testerManagers.Draw();
        _testerStatuses.Draw();
        _testerPresets.Draw();

        if (ImGui.CollapsingHeader("Status Managers"))
        {
            foreach (var (name, manager) in LociManager.Managers)
                DrawActorSM(name, manager);
        }
    }

    private void LatestTargetApply()
    {
        ImGui.Text("Last ApplyStatus:");
        if (_lastSingleRequest.Addr == nint.Zero)
        {
            CkGui.ColorTextInline("None requested...", CkCol.TriStateCross.Uint());
            return;
        }

        using var ident = ImRaii.PushIndent();
        ImGui.Text("Address:");
        CkGui.ColorTextInline($"{_lastSingleRequest.Addr:X}", ImGuiColors.DalamudViolet);
        ImGui.Text("TargetHost:");
        CkGui.ColorTextInline(_lastSingleRequest.RequestedHost, ImGuiColors.DalamudViolet);
        CkGui.TextFrameAligned("Status Info:");
        ImGui.SameLine();
        LociIcon.Draw((uint)_lastSingleRequest.Data.IconID, _lastSingleRequest.Data.Stacks, LociIcon.SizeFramed);
        Utils.AttachTooltip(_lastSingleRequest.Data, [], []);
    }

    private void LatestTargetApplyBulk()
    {
        ImGui.Text("Last ApplyStatuses:");
        if (_lastBulkRequest.Addr == nint.Zero)
        {
            CkGui.ColorTextInline("None requested...", CkCol.TriStateCross.Uint());
            return;
        }

        using var ident = ImRaii.PushIndent();
        ImGui.Text("Address:");
        CkGui.ColorTextInline($"{_lastBulkRequest.Addr:X}", ImGuiColors.DalamudViolet);
        ImGui.Text("TargetHost:");
        CkGui.ColorTextInline(_lastBulkRequest.RequestedHost, ImGuiColors.DalamudViolet);
        CkGui.TextFrameAligned("Status Info:");
        ImGui.SameLine();
        using var iconGroup = ImRaii.Group();

        for (var i = 0; i < _lastBulkRequest.Data.Count; i++)
        {
            if (_lastBulkRequest.Data[i].IconID is 0)
                continue;

            LociIcon.Draw((uint)_lastBulkRequest.Data[i].IconID, _lastBulkRequest.Data[i].Stacks, LociIcon.SizeFramed);
            Utils.AttachTooltip(_lastBulkRequest.Data[i], _lastBulkRequest.Data, []);

            if (i < _lastBulkRequest.Data.Count)
                ImUtf8.SameLineInner();
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
            CkGui.AttachToolTip(status.ID);
            ImGui.TableNextColumn();
            if (LociIcon.TryGetGameIcon((uint)status.IconID, false, out var wrap))
            {
                ImGui.Image(wrap.Handle, LociIcon.SizeFramed);
                CkGui.AttachToolTip($"{status.IconID}");
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
