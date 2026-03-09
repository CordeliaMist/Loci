using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc;
using Loci.Data;
using OtterGui.Text;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterStatusManagers : IIpcTesterGroup
{
    private ICallGateSubscriber<nint, object> _onManagerModified;

    private ICallGateSubscriber<string> _getOwnManager;
    private ICallGateSubscriber<nint, string> _getManagerByPtr;
    private ICallGateSubscriber<string, string> _getManagerByName;

    private ICallGateSubscriber<List<LociStatusInfo>> _getOwnManagerInfo;
    private ICallGateSubscriber<nint, List<LociStatusInfo>> _getManagerInfoByPtr;
    private ICallGateSubscriber<string, List<LociStatusInfo>> _getManagerInfoByName;

    private ICallGateSubscriber<string, object> _setOwnManager;
    private ICallGateSubscriber<nint, string, object> _setManagerByPtr;
    private ICallGateSubscriber<string, string, object> _setManagerByName;

    private ICallGateSubscriber<object> _clearOwnManager;
    private ICallGateSubscriber<nint, object> _clearManagerByPtr;
    private ICallGateSubscriber<string, object> _clearManagerByName;

    private nint _lastAddrModified = nint.Zero;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _actorName = string.Empty;

    private string _managerBase64 = string.Empty;
    private List<LociStatusInfo> _lastManagerInfo = new();

    private bool _lastReturnCode = false;

    private readonly LociManager _manager;
    public IpcTesterStatusManagers(LociManager manager)
    {
        _manager = manager;
        _onManagerModified = Svc.PluginInterface.GetIpcSubscriber<nint, object>("Loci.OnManagerModified");

        _getOwnManager = Svc.PluginInterface.GetIpcSubscriber<string>("Loci.GetOwnManager");
        _getManagerByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, string>("Loci.GetManagerByPtr");
        _getManagerByName = Svc.PluginInterface.GetIpcSubscriber<string, string>("Loci.GetManagerByName");

        _getOwnManagerInfo = Svc.PluginInterface.GetIpcSubscriber<List<LociStatusInfo>>("Loci.GetOwnManagerInfo");
        _getManagerInfoByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, List<LociStatusInfo>>("Loci.GetManagerInfoByPtr");
        _getManagerInfoByName = Svc.PluginInterface.GetIpcSubscriber<string, List<LociStatusInfo>>("Loci.GetManagerInfoByName");

        _setOwnManager = Svc.PluginInterface.GetIpcSubscriber<string, object>("Loci.SetOwnManager");
        _setManagerByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, string, object>("Loci.SetManagerByPtr");
        _setManagerByName = Svc.PluginInterface.GetIpcSubscriber<string, string, object>("Loci.SetManagerByName");

        _clearOwnManager = Svc.PluginInterface.GetIpcSubscriber<object>("Loci.ClearOwnManager");
        _clearManagerByPtr = Svc.PluginInterface.GetIpcSubscriber<nint, object>("Loci.ClearManagerByPtr");
        _clearManagerByName = Svc.PluginInterface.GetIpcSubscriber<string, object>("Loci.ClearManagerByName");
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onManagerModified.Subscribe(OnManagerModified);
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Status Manager IPCs.");
    }
    public void Unsubscribe()
    {
        _onManagerModified.Unsubscribe(OnManagerModified);
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Status Manager IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnManagerModified(nint addr)
        => _lastAddrModified = addr;

    public unsafe void Draw()
    {
        using var _ = ImRaii.TreeNode("LociManagers");
        if (!_) return;

        if (ImGui.InputTextWithHint("##sm-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;
        ImGui.InputTextWithHint("##sm-actor-name", "Player Name@World...", ref _actorName, 64);
        ImGui.InputTextWithHint("##sm-base64", "Manager Base64...", ref _managerBase64, 15000);
        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Manager Info", disabled: !IsSubscribed))
            _lastManagerInfo = [];
        if (_lastManagerInfo.Count is not 0)
        {
            using (CkRaii.FramedChildPaddedW("##manager-info", ImGui.GetContentRegionAvail().X, LociIcon.Size.Y, 0, LociCol.Gold.Uint(), 5f, 1f))
            {
                // Calculate the remaining height in the region.
                var savedTuples = LociData.Statuses.Select(s => s.ToTuple()).ToList();
                for (var i = 0; i < _lastManagerInfo.Count; i++)
                {
                    if (_lastManagerInfo[i].IconID is 0)
                        continue;

                    LociIcon.Draw((uint)_lastManagerInfo[i].IconID, _lastManagerInfo[i].Stacks, LociIcon.Size);
                    Utils.AttachTooltip(_lastManagerInfo[i], savedTuples, []);

                    if (i < _lastManagerInfo.Count)
                        ImUtf8.SameLineInner();
                }
            }
        }

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorTextBool(_lastReturnCode ? "Success" : "Failure", _lastReturnCode);

        IpcTesterTab.DrawIpcRowStart("Last Modified Manager Actor", $"{_lastAddrModified:X}");

        // Getters (Base64)
        IpcTesterTab.DrawIpcRowStart("Loci.GetOwnManager", "Get Own Manager");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed))
            _managerBase64 = _getOwnManager.InvokeFunc();

        IpcTesterTab.DrawIpcRowStart("Loci.GetManagerByPtr", "Get Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _managerBase64 = _getManagerByPtr.InvokeFunc(_actorAddr);

        IpcTesterTab.DrawIpcRowStart("Loci.GetManagerByName", "Get Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || _actorName.Length == 0))
            _managerBase64 = _getManagerByName.InvokeFunc(_actorName);

        // Getters (Tuples)
        IpcTesterTab.DrawIpcRowStart("Loci.GetOwnManagerInfo", "Get Own Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastManagerInfo = _getOwnManagerInfo.InvokeFunc() ?? new();

        IpcTesterTab.DrawIpcRowStart("Loci.GetManagerInfoByPtr", "Get Info by Ptr");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastManagerInfo = _getManagerInfoByPtr.InvokeFunc(_actorAddr) ?? new();

        IpcTesterTab.DrawIpcRowStart("Loci.GetManagerInfoByName", "Get Info by Name");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed || _actorName.Length == 0))
            _lastManagerInfo = _getManagerInfoByName.InvokeFunc(_actorName) ?? new();

        // Setters (Base64)
        IpcTesterTab.DrawIpcRowStart("Loci.SetOwnManager", "Set Own Manager");
        var blockOwnDataApp = LociManager.ClientSM.LockedStatuses.Count != 0;
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set", disabled: !IsSubscribed || blockOwnDataApp))
            _setOwnManager.InvokeAction(_managerBase64);
        CkGui.AttachToolTip("Cannot set while locked statuses are active.", disabled: blockOwnDataApp);

        IpcTesterTab.DrawIpcRowStart("Loci.SetManagerByPtr", "Set Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set", disabled: !IsSubscribed || _actorAddr == nint.Zero || _managerBase64.Length is 0))
            _setManagerByPtr.InvokeAction(_actorAddr, _managerBase64);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterTab.DrawIpcRowStart("Loci.SetManagerByName", "Set Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set", disabled: !IsSubscribed || _actorName.Length == 0 || _managerBase64.Length is 0))
            _setManagerByName.InvokeAction(_actorName, _managerBase64);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        // Clear
        IpcTesterTab.DrawIpcRowStart("Loci.ClearOwnManager", "Clear Own Manager");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear", disabled: !IsSubscribed || blockOwnDataApp))
            _clearOwnManager.InvokeAction();
        CkGui.AttachToolTip("Cannot clear while locked statuses are active.", disabled: blockOwnDataApp);

        IpcTesterTab.DrawIpcRowStart("Loci.ClearManagerByPtr", "Clear Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _clearManagerByPtr.InvokeAction(_actorAddr);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterTab.DrawIpcRowStart("Loci.ClearManagerByName", "Clear Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear", disabled: !IsSubscribed || _actorName.Length == 0))
            _clearManagerByName.InvokeAction(_actorName);
        CkGui.AttachToolTip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
    }
}