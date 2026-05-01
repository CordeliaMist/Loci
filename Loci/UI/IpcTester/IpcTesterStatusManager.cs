using CkCommons.Gui;
using CkCommons.Raii;
using CkCommons.Textures;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using LociApi.Ipc;
using LociApi.Enums;
using LociApi.Helpers;
using OtterGui.Text;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterStatusManagers : IIpcTesterGroup
{
    private readonly EventSubscriber<nint, ManagerChangeType> _managerModified;

    public (nint Addr, ManagerChangeType ChangeType) LastManagerUpdate { get; private set; } = (nint.Zero, ManagerChangeType.NoChange);

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _playerName = string.Empty;
    private string _buddyName = string.Empty;

    private string _managerBase64 = string.Empty;
    private string _convertedBase64 = string.Empty;
    private List<LociStatusInfo> _lastManagerInfo = new();

    private LociApiEc _lastReturnCode = LociApiEc.UnkError;

    private readonly LociManager _manager;
    public IpcTesterStatusManagers(LociManager manager)
    {
        _manager = manager;

        _managerModified = ManagerChanged.Subscriber(Svc.PluginInterface, OnManagerModified);
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _managerModified.Enable();
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Status Manager IPCs.");
    }
    public void Unsubscribe()
    {
        _managerModified.Disable();
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Status Manager IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnManagerModified(nint addr, ManagerChangeType changeType)
        => LastManagerUpdate = (addr, changeType);

    public unsafe void Draw()
    {
        if (ImGui.InputTextWithHint("##sm-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;
        ImGui.InputTextWithHint("##sm-actor-name", "Player Name@World...", ref _playerName, 64);
        CkGui.AttachTooltip("Make this PlayerName@World when working with players, and PlayerName when with pets.");
        ImGui.InputTextWithHint("##sm-buddy-name", "Pet/Minion/Companion Name...", ref _buddyName, 64);

        ImGui.InputTextWithHint("##sm-base64", "Manager Base64...", ref _managerBase64, 15000);
        // Maybe convert to copiable popup.
        ImGui.InputTextWithHint("##sm-converted-data", "Converted Base64 Shows Here...", ref _convertedBase64, 15000, ImGuiInputTextFlags.ReadOnly);
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

                    LociIcon.Draw(_lastManagerInfo[i].IconID, _lastManagerInfo[i].Stacks, LociIcon.Size);
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
        CkGui.ColorText(_lastReturnCode.ToString(), ImGuiColors.DalamudYellow);

        IpcTesterUI.DrawIpcRowStart("Last Modified Manager", $"{LastManagerUpdate.Addr:X} ({LastManagerUpdate.ChangeType})");

        // Getters (Base64)
        IpcTesterUI.DrawIpcRowStart(GetManager.Label, "Get Own Manager");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed))
        {
            var r = new GetManager(Svc.PluginInterface).Invoke();
            (_lastReturnCode, _managerBase64) = (r.Item1, r.Item2 ?? string.Empty);
        }

        IpcTesterUI.DrawIpcRowStart(GetManagerByPtr.Label, "Get Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || _actorAddr == nint.Zero))
        {
            var r = new GetManagerByPtr(Svc.PluginInterface).Invoke(_actorAddr);
            (_lastReturnCode, _managerBase64) = (r.Item1, r.Item2 ?? string.Empty);
        }

        IpcTesterUI.DrawIpcRowStart(GetManagerByName.Label, "Get Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || _playerName.Length == 0))
        {
            var r = new GetManagerByName(Svc.PluginInterface).Invoke(_playerName);
            (_lastReturnCode, _managerBase64) = (r.Item1, r.Item2 ?? string.Empty);
        }

        // Getters (Tuples)
        IpcTesterUI.DrawIpcRowStart(GetManagerInfo.Label, "Get Own Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastManagerInfo = new GetManagerInfo(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(GetManagerInfoByPtr.Label, "Get Info by Ptr");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastManagerInfo = new GetManagerInfoByPtr(Svc.PluginInterface).Invoke(_actorAddr);

        IpcTesterUI.DrawIpcRowStart(GetManagerInfoByName.Label, "Get Info by Name");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed || _playerName.Length == 0))
            _lastManagerInfo = new GetManagerInfoByName(Svc.PluginInterface).Invoke(_playerName);

        // Setters (Base64)
        IpcTesterUI.DrawIpcRowStart(SetManager.Label, "Set Own Manager");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set", disabled: !IsSubscribed || _managerBase64.Length is 0))
            _lastReturnCode = new SetManager(Svc.PluginInterface).Invoke(_managerBase64);

        IpcTesterUI.DrawIpcRowStart(SetManagerByPtr.Label, "Set Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set", disabled: !IsSubscribed || _actorAddr == nint.Zero || _managerBase64.Length is 0))
            _lastReturnCode = new SetManagerByPtr(Svc.PluginInterface).Invoke(_actorAddr, _managerBase64);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(SetManagerByName.Label, "Set Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Set Player", disabled: !IsSubscribed || _playerName.Length == 0 || _managerBase64.Length is 0))
            _lastReturnCode = new SetManagerByName(Svc.PluginInterface).Invoke(_playerName, _managerBase64);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Upload, "Set Buddy", disabled: !IsSubscribed || _playerName.Length == 0 || _managerBase64.Length is 0))
                _lastReturnCode = new SetManagerByName(Svc.PluginInterface).Invoke(_playerName, _buddyName, _managerBase64);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }

        // Clear
        IpcTesterUI.DrawIpcRowStart(ClearManager.Label, "Clear Own Manager");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear", disabled: !IsSubscribed))
            _lastReturnCode = new ClearManager(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(ClearManagerByPtr.Label, "Clear Manager by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastReturnCode = new ClearManagerByPtr(Svc.PluginInterface).Invoke(_actorAddr);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(ClearManagerByName.Label, "Clear Manager by Name");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Clear Player", disabled: !IsSubscribed || _playerName.Length == 0))
            _lastReturnCode = new ClearManagerByName(Svc.PluginInterface).Invoke(_playerName);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Trash, "Clear Buddy", disabled: !IsSubscribed || _playerName.Length == 0))
                _lastReturnCode = new ClearManagerByName(Svc.PluginInterface).Invoke(_playerName, _buddyName);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }

        IpcTesterUI.DrawIpcRowStart(ConvertLegacyData.Label, "Convert Legacy ActorSM");
        if (CkGui.SmallIconTextButton(FAI.Sync, "Convert", disabled: !IsSubscribed || _managerBase64.Length is 0))
            _convertedBase64 = new ConvertLegacyData(Svc.PluginInterface).Invoke(_managerBase64);
        CkGui.AttachTooltip("Primarily for testing purposes!");
    }
}