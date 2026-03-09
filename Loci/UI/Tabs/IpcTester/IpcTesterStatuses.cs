using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc;
using Loci.Combos;
using Loci.Data;
using OtterGui;
using OtterGui.Text;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterStatuses : IIpcTesterGroup
{
    private ICallGateSubscriber<Guid, bool, object> _onStatusUpdated;

    // Acquisition
    private ICallGateSubscriber<Guid, LociStatusInfo> _getStatusInfo;
    private ICallGateSubscriber<List<LociStatusInfo>> _getAllStatusInfo;

    // Client Application By GUID
    private ICallGateSubscriber<Guid, object> _applyStatus;
    private ICallGateSubscriber<Guid, uint, bool> _applyLockedStatus;
    private ICallGateSubscriber<List<Guid>, object> _applyStatuses;
    private ICallGateSubscriber<List<Guid>, uint, bool> _applyLockedStatuses;

    // Client Application by Info
    private ICallGateSubscriber<LociStatusInfo, object> _applyStatusInfo;
    private ICallGateSubscriber<LociStatusInfo, uint, bool> _applyLockedStatusInfo;
    private ICallGateSubscriber<List<LociStatusInfo>, object> _applyStatusInfos;
    private ICallGateSubscriber<List<LociStatusInfo>, uint, bool> _applyLockedStatusInfos;

    // Application
    private ICallGateSubscriber<Guid, nint, object> _applyStatusByPtr;
    private ICallGateSubscriber<List<Guid>, nint, object> _applyStatusesByPtr;
    private ICallGateSubscriber<Guid, string, object> _applyStatusByName;
    private ICallGateSubscriber<List<Guid>, string, object> _applyStatusesByName;

    // Locking
    private ICallGateSubscriber<Guid, uint, bool> _lockStatus;
    private ICallGateSubscriber<List<Guid>, uint, (bool, List<Guid>)> _lockStatuses;
    private ICallGateSubscriber<Guid, uint, bool> _unlockStatus;
    private ICallGateSubscriber<List<Guid>, uint, (bool, List<Guid>)> _unlockStatuses;
    private ICallGateSubscriber<uint, bool> _clearLocks;

    // Removal
    private ICallGateSubscriber<Guid, bool> _removeStatus;
    private ICallGateSubscriber<List<Guid>, object> _removeStatuses;
    private ICallGateSubscriber<Guid, nint, bool> _removeStatusByPtr;
    private ICallGateSubscriber<List<Guid>, nint, object> _removeStatusesByPtr;
    private ICallGateSubscriber<Guid, string, bool> _removeStatusByName;
    private ICallGateSubscriber<List<Guid>, string, object> _removeStatusesByName;

    private string _statusGuidString = string.Empty;
    private Guid? _statusGuid;

    private uint _lockCode = 0;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _actorName = string.Empty;

    private LociStatusInfo _lastStatusInfo;
    private List<LociStatusInfo> _allStatusInfo = new();

    private List<Guid> _lockFailures = new();

    private bool _lastReturnCode;
    private (Guid Status, bool WasDeleted) _lastStatusUpdated;

    private SavedStatusesCombo _ownStatusCombo;
    public IpcTesterStatuses(ILogger<IpcTesterStatuses> logger, LociManager loci)
    {
        _onStatusUpdated = Svc.PluginInterface.GetIpcSubscriber<Guid, bool, object>("Loci.OnStatusUpdated");

        _getStatusInfo = Svc.PluginInterface.GetIpcSubscriber<Guid, LociStatusInfo>("Loci.GetStatusInfo");
        _getAllStatusInfo = Svc.PluginInterface.GetIpcSubscriber<List<LociStatusInfo>>("Loci.GetAllStatusInfo");

        _applyStatus = Svc.PluginInterface.GetIpcSubscriber<Guid, object>("Loci.ApplyStatus");
        _applyLockedStatus = Svc.PluginInterface.GetIpcSubscriber<Guid, uint, bool>("Loci.ApplyLockedStatus");
        _applyStatuses = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, object>("Loci.ApplyStatuses");
        _applyLockedStatuses = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, uint, bool>("Loci.ApplyLockedStatuses");

        _applyStatusInfo = Svc.PluginInterface.GetIpcSubscriber<LociStatusInfo, object>("Loci.ApplyStatusInfo");
        _applyLockedStatusInfo = Svc.PluginInterface.GetIpcSubscriber<LociStatusInfo, uint, bool>("Loci.ApplyLockedStatusInfo");
        _applyStatusInfos = Svc.PluginInterface.GetIpcSubscriber<List<LociStatusInfo>, object>("Loci.ApplyStatusInfos");
        _applyLockedStatusInfos = Svc.PluginInterface.GetIpcSubscriber<List<LociStatusInfo>, uint, bool>("Loci.ApplyLockedStatusInfos");

        _applyStatusByPtr = Svc.PluginInterface.GetIpcSubscriber<Guid, nint, object>("Loci.ApplyStatusByPtr");
        _applyStatusesByPtr = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, nint, object>("Loci.ApplyStatusesByPtr");
        _applyStatusByName = Svc.PluginInterface.GetIpcSubscriber<Guid, string, object>("Loci.ApplyStatusByName");
        _applyStatusesByName = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, string, object>("Loci.ApplyStatusesByName");

        _lockStatus = Svc.PluginInterface.GetIpcSubscriber<Guid, uint, bool>("Loci.LockStatus");
        _lockStatuses = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, uint, (bool, List<Guid>)>("Loci.LockStatuses");
        _unlockStatus = Svc.PluginInterface.GetIpcSubscriber<Guid, uint, bool>("Loci.UnlockStatus");
        _unlockStatuses = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, uint, (bool, List<Guid>)>("Loci.UnlockStatuses");
        _clearLocks = Svc.PluginInterface.GetIpcSubscriber<uint, bool>("Loci.ClearLocks");

        _removeStatus = Svc.PluginInterface.GetIpcSubscriber<Guid, bool>("Loci.RemoveStatus");
        _removeStatuses = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, object>("Loci.RemoveStatuses");
        _removeStatusByPtr = Svc.PluginInterface.GetIpcSubscriber<Guid, nint, bool>("Loci.RemoveStatusByPtr");
        _removeStatusesByPtr = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, nint, object>("Loci.RemoveStatusesByPtr");
        _removeStatusByName = Svc.PluginInterface.GetIpcSubscriber<Guid, string, bool>("Loci.RemoveStatusByName");
        _removeStatusesByName = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, string, object>("Loci.RemoveStatusesByName");

        _ownStatusCombo = new SavedStatusesCombo(logger, loci, () => [.. LociData.Statuses.OrderBy(s => s.Title)]);
        _ownStatusCombo.HintText = "Select Status...";
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onStatusUpdated.Subscribe(OnStatusUpdated);
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Custom Statuses IPCs.");
    }
    public void Unsubscribe()
    {
        _onStatusUpdated.Unsubscribe(OnStatusUpdated);
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Custom Statuses IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnStatusUpdated(Guid statusId, bool wasDeleted)
        => _lastStatusUpdated = (statusId, wasDeleted);
    public static void KeyInput(ref uint key)
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
        var keyI = (int)key;
        if (ImGui.InputInt("Key", ref keyI, 0, 0))
            key = (uint)keyI;
    }
    public unsafe void Draw()
    {
        using var _ = ImRaii.TreeNode("Statuses");
        if (!_) return;
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
            LociIcon.Draw((uint)_lastStatusInfo.IconID, _lastStatusInfo.Stacks, LociIcon.Size);
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
        CkGui.AttachToolTip("Clears the cached preset tuple.");

        ImGui.InputTextWithHint("##statuses-chara-name", "Player Name@World...", ref _actorName, 64);
        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple List", disabled: !IsSubscribed))
            _allStatusInfo = [];
        CkGui.AttachToolTip("Clears the full list info cache.");

        if (_allStatusInfo.Count is not 0)
        {
            using (CkRaii.FramedChildPaddedW("##manager-info", ImGui.GetContentRegionAvail().X, LociIcon.Size.Y, 0, LociCol.Gold.Uint(), 5f, 1f))
            {
                // Calculate the remaining height in the region.
                for (var i = 0; i < _allStatusInfo.Count; i++)
                {
                    if (_allStatusInfo[i].IconID is 0)
                        continue;

                    LociIcon.Draw((uint)_allStatusInfo[i].IconID, _allStatusInfo[i].Stacks, LociIcon.Size);
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
        CkGui.ColorTextBool(_lastReturnCode ? "Success" : "Failure", _lastReturnCode);

        // Event monitor
        IpcTesterTab.DrawIpcRowStart("Last Modified Status", _lastStatusUpdated.Status.ToString());
        ImGui.TableNextColumn();
        ImGui.Text("Was Deleted?:");
        CkGui.BoolIcon(_lastStatusUpdated.WasDeleted, true);

        // Getting Data
        IpcTesterTab.DrawIpcRowStart("Loci.GetStatusInfo", "Get Status Info");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            _lastStatusInfo = _getStatusInfo.InvokeFunc(_statusGuid!.Value);

        IpcTesterTab.DrawIpcRowStart("Loci.GetAllStatusInfo", "Get All Status Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _allStatusInfo = _getAllStatusInfo.InvokeFunc() ?? [];

        // Client-Based application
        IpcTesterTab.DrawIpcRowStart("Loci.ApplyStatus", "Apply Status (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || !isGuidValid))
            _applyStatus.InvokeAction(_statusGuid!.Value);

        IpcTesterTab.DrawIpcRowStart("Loci.ApplyLockedStatus", "Apply Locked (Client)");
        if (CkGui.SmallIconTextButton(FAI.Lock, "Apply", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = _applyLockedStatus.InvokeFunc(_statusGuid!.Value, _lockCode);

        // Info-based application
        IpcTesterTab.DrawIpcRowStart("Loci.ApplyStatusInfo", "Apply Tuple (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _lastStatusInfo.GUID == Guid.Empty))
            _applyStatusInfo.InvokeAction(_lastStatusInfo);

        IpcTesterTab.DrawIpcRowStart("Loci.ApplyLockedStatusInfo", "Apply Locked Tuple");
        if (CkGui.SmallIconTextButton(FAI.Lock, "Apply", disabled: !IsSubscribed || _lastStatusInfo.GUID == Guid.Empty))
            _lastReturnCode = _applyLockedStatusInfo.InvokeFunc(_lastStatusInfo, _lockCode);

        // Client Locks
        IpcTesterTab.DrawIpcRowStart("Loci.LockStatus", "Lock Status");
        if (CkGui.SmallIconTextButton(FAI.Lock, "Lock", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = _lockStatus.InvokeFunc(_statusGuid!.Value, _lockCode);

        IpcTesterTab.DrawIpcRowStart("Loci.UnlockStatus", "Unlock Status");
        if (CkGui.SmallIconTextButton(FAI.Unlock, "Unlock", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = _unlockStatus.InvokeFunc(_statusGuid!.Value, _lockCode);

        IpcTesterTab.DrawIpcRowStart("Loci.ClearLocks", "Clear Locks");
        if (CkGui.SmallIconTextButton(FAI.Broom, "Clear", disabled: !IsSubscribed))
            _lastReturnCode = _clearLocks.InvokeFunc(_lockCode);

        // Application
        IpcTesterTab.DrawIpcRowStart("Loci.ApplyStatusByPtr", "Apply by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorAddr == nint.Zero || !isGuidValid))
            _applyStatusByPtr.InvokeAction(_statusGuid!.Value, _actorAddr);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterTab.DrawIpcRowStart("Loci.ApplyStatusByName", "Apply by Name");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorName.Length is 0 || !isGuidValid))
            _applyStatusByName.InvokeAction(_statusGuid!.Value, _actorName);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        // Removal
        IpcTesterTab.DrawIpcRowStart("Loci.RemoveStatus", "Remove (Client)");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = _removeStatus.InvokeFunc(_statusGuid!.Value);

        IpcTesterTab.DrawIpcRowStart("Loci.RemoveStatusByPtr", "Remove by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastReturnCode = _removeStatusByPtr.InvokeFunc(_statusGuid!.Value, _actorAddr);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterTab.DrawIpcRowStart("Loci.RemoveStatusByName", "Remove by Name");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || _actorName.Length == 0))
            _lastReturnCode = _removeStatusByName.InvokeFunc(_statusGuid!.Value, _actorName);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
    }
}