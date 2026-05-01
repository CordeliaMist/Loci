using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Combos;
using Loci.Data;
using LociApi.Enums;
using LociApi.Helpers;
using LociApi.Ipc;
using OtterGui;
using OtterGui.Text;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterStatuses : IIpcTesterGroup
{
    private readonly EventSubscriber<Guid, bool> _onStatusUpdated;
    private readonly EventSubscriber<nint, Guid, ChainTrigger, ChainType, Guid> _onChainTriggerHit;

    private string _statusGuidString = string.Empty;
    private Guid? _statusGuid;

    private uint _lockCode = 0;
    private bool _lastCanLock = false;
    private int _lastUnlockAllRes = 0;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _playerName = string.Empty;
    private string _buddyName = string.Empty;

    private LociStatusInfo _lastStatusInfo;
    private List<LociStatusInfo> _allStatusInfo = new();
    private LociStatusSummary _lastStatusSummary;
    private List<LociStatusSummary> _lastBulkSummary = [];

    private LociApiEc _lastReturnCode;
    private (Guid Status, bool WasDeleted) _lastStatusUpdated;
    private (nint Address, Guid StatusId, ChainTrigger Condition, ChainType ChainType, Guid ChainedId) _lastChainHit;

    private SavedStatusesCombo _ownStatusCombo;
    public IpcTesterStatuses(ILogger<IpcTesterStatuses> logger, LociManager loci)
    {
        _onStatusUpdated = StatusUpdated.Subscriber(Svc.PluginInterface, OnStatusUpdated);
        _onChainTriggerHit = ChainTriggerHit.Subscriber(Svc.PluginInterface, OnChainTrigger);
        _ownStatusCombo = new SavedStatusesCombo(logger, loci, () => [.. LociData.Statuses.OrderBy(s => s.Title)]);
        _ownStatusCombo.HintText = "Select Status...";
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onStatusUpdated.Enable();
        _onChainTriggerHit.Enable();
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Custom Statuses IPCs.");
    }
    public void Unsubscribe()
    {
        _onStatusUpdated.Disable();
        _onChainTriggerHit.Disable();
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Custom Statuses IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnStatusUpdated(Guid statusId, bool wasDeleted)
        => _lastStatusUpdated = (statusId, wasDeleted);

    private void OnChainTrigger(nint address, Guid statusId, ChainTrigger condition, ChainType chainType, Guid chainedId)
        => _lastChainHit = (address, statusId, condition, chainType, chainedId);
    public static void KeyInput(ref uint key)
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
        var keyI = (int)key;
        if (ImGui.InputInt("Key", ref keyI, 0, 0))
            key = (uint)keyI;
    }
    public unsafe void Draw()
    {
        var width = ImGui.GetContentRegionAvail().X / 2;
        ImGuiUtil.GuidInput("Status GUID##status-id", "GUID...", "", ref _statusGuid, ref _statusGuidString, width);
        var refId = _statusGuid ?? _lastStatusInfo.GUID;
        if (_ownStatusCombo.Draw("status-selector", refId, width, 1.15f))
        {
            if (_ownStatusCombo.Current is { } valid)
            {
                _statusGuid = valid.GUID;
                _lastStatusInfo = valid.ToTuple();
            }
        }
        if (_lastStatusInfo.GUID != Guid.Empty)
        {
            ImGui.SameLine();
            ImGui.Text("Stored Tuple:");
            ImUtf8.SameLineInner();
            LociIcon.Draw(_lastStatusInfo.IconID, _lastStatusInfo.Stacks, LociIcon.Size);
            Utils.AttachTooltip(_lastStatusInfo, _allStatusInfo, []);
        }
        // Key area
        KeyInput(ref _lockCode);

        // Target Types
        if (ImGui.InputTextWithHint("##statuses-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;
        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple", disabled: !IsSubscribed))
            _lastStatusInfo = default;
        CkGui.AttachTooltip("Clears the cached preset tuple.");

        ImGui.InputTextWithHint("##statuses-actor-name", "Player Name@World...", ref _playerName, 64);
        CkGui.AttachTooltip("Make this PlayerName@World when working with players, and PlayerName when with pets.");

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple List", disabled: !IsSubscribed))
            _allStatusInfo = [];
        CkGui.AttachTooltip("Clears the full list info cache.");

        ImGui.InputTextWithHint("##statuses-buddy-name", "Pet/Minion/Companion Name...", ref _buddyName, 64);

        if (_allStatusInfo.Count is not 0)
        {
            using (CkRaii.FramedChildPaddedW("##statuses-info", ImGui.GetContentRegionAvail().X, LociIcon.Size.Y, 0, LociCol.Gold.Uint(), 5f, 1f))
            {
                // Calculate the remaining height in the region.
                for (var i = 0; i < _allStatusInfo.Count; i++)
                {
                    if (_allStatusInfo[i].IconID is 0)
                        continue;

                    LociIcon.Draw(_allStatusInfo[i].IconID, _allStatusInfo[i].Stacks, LociIcon.Size);
                    Utils.AttachTooltip(_allStatusInfo[i], _allStatusInfo, []);

                    if (i < _allStatusInfo.Count)
                        ImUtf8.SameLineInner();
                }
            }
        }

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        var isGuidValid = _statusGuid.HasValue && _statusGuid != Guid.Empty;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorText(_lastReturnCode.ToString(), ImGuiColors.DalamudYellow);

        // Event monitor
        IpcTesterUI.DrawIpcRowStart("Last Modified Status", _lastStatusUpdated.Status.ToString());
        ImGui.TableNextColumn();
        ImGui.Text("Was Deleted?:");
        CkGui.BoolIcon(_lastStatusUpdated.WasDeleted, true);

        // Getting Data
        IpcTesterUI.DrawIpcRowStart(GetStatusInfo.Label, "Get Status Info");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastStatusInfo) = new GetStatusInfo(Svc.PluginInterface).Invoke(_statusGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetStatusInfoList.Label, "Get All Status Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _allStatusInfo = new GetStatusInfoList(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(GetStatusSummary.Label, "Get Status Summary");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastStatusSummary) = new GetStatusSummary(Svc.PluginInterface).Invoke(_statusGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetStatusSummaryList.Label, "Get All Status Summaries");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastBulkSummary = new GetStatusSummaryList(Svc.PluginInterface).Invoke();

        // Application.
        IpcTesterUI.DrawIpcRowStart(ApplyStatus.Label, "Apply Status (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new ApplyStatus(Svc.PluginInterface).Invoke(_statusGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(ApplyStatusInfo.Label, "Apply Tuple (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _lastStatusInfo.GUID == Guid.Empty))
            _lastReturnCode = new ApplyStatusInfo(Svc.PluginInterface).Invoke(_lastStatusInfo, _lockCode);

        IpcTesterUI.DrawIpcRowStart(ApplyStatusByPtr.Label, "Apply by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorAddr == nint.Zero || !isGuidValid))
            _lastReturnCode = new ApplyStatusByPtr(Svc.PluginInterface).Invoke(_statusGuid!.Value, _actorAddr);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(ApplyStatusByName.Label, "Apply by Player Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Apply to Player", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
            _lastReturnCode = new ApplyStatusByName(Svc.PluginInterface).Invoke(_statusGuid!.Value, _playerName);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Upload, "Apply to Buddy", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
                _lastReturnCode = new ApplyStatusByName(Svc.PluginInterface).Invoke(_statusGuid!.Value, _playerName, _buddyName);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }

        // Removal
        IpcTesterUI.DrawIpcRowStart(RemoveStatus.Label, "Remove (Client)");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new RemoveStatus(Svc.PluginInterface).Invoke(_statusGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(RemoveStatusByPtr.Label, "Remove by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastReturnCode = new RemoveStatusByPtr(Svc.PluginInterface).Invoke(_statusGuid!.Value, _actorAddr);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(RemoveStatusByName.Label, "Remove by Player Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Remove from Player", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
            _lastReturnCode = new RemoveStatusByName(Svc.PluginInterface).Invoke(_statusGuid!.Value, _playerName);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Upload, "Remove from Buddy", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
                _lastReturnCode = new RemoveStatusByName(Svc.PluginInterface).Invoke(_statusGuid!.Value, _playerName, _buddyName);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }

        // Locking
        IpcTesterUI.DrawIpcRowStart(CanLock.Label, "Can Lock cur GUID");
        if (CkGui.SmallIconTextButton(FAI.QuestionCircle, "Can Unlock", disabled: !IsSubscribed || !isGuidValid))
            _lastCanLock = new CanLock(Svc.PluginInterface).Invoke(_statusGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(LockStatus.Label, "Lock Status");
        if (CkGui.SmallIconTextButton(FAI.Lock, "Lock", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new LockStatus(Svc.PluginInterface).Invoke(_statusGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(UnlockStatus.Label, "Unlock Status");
        if (CkGui.SmallIconTextButton(FAI.Unlock, "Unlock", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new UnlockStatus(Svc.PluginInterface).Invoke(_statusGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(UnlockAll.Label, "Clear Locks for key");
        if (CkGui.SmallIconTextButton(FAI.Broom, "Clear", disabled: !IsSubscribed))
            _lastUnlockAllRes = new UnlockAll(Svc.PluginInterface).Invoke(_lockCode);

    }
}