using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Ipc;
using Loci.Data;
using OtterGui;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterPresets : IIpcTesterGroup
{
    private ICallGateSubscriber<Guid, bool, object> _onPresetModified;

    // Acquisition
    private ICallGateSubscriber<Guid, LociPresetInfo> _getPresetInfo;
    private ICallGateSubscriber<List<LociPresetInfo>> _getAllPresetInfo;

    // Client Application By GUID
    private ICallGateSubscriber<Guid, object> _applyPreset;
    private ICallGateSubscriber<List<Guid>, object> _applyPresets;

    // Client Application by Info
    private ICallGateSubscriber<LociPresetInfo, object> _applyPresetInfo;
    private ICallGateSubscriber<List<LociPresetInfo>, object> _applyPresetInfos;

    // Normal Application
    private ICallGateSubscriber<Guid, nint, object> _applyPresetByPtr;
    private ICallGateSubscriber<List<Guid>, nint, object> _applyPresetsByPtr;
    private ICallGateSubscriber<Guid, string, object> _applyPresetByName;
    private ICallGateSubscriber<List<Guid>, string, object> _applyPresetsByName;

    private string _presetGuidString = string.Empty;
    private Guid? _presetGuid;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _actorName = string.Empty;

    private LociPresetInfo _lastPresetInfo;
    private List<LociPresetInfo> _allPresetInfo = new();

    private bool _lastReturnCode = false; // Improve later with our own API likely.
    private (Guid Preset, bool WasDeleted) _lastPresetUpdated;

    // private SavedPresetsCombo _ownPresetCombo; Make one for presets later.
    public IpcTesterPresets(ILogger<IpcTesterPresets> logger, LociManager loci)
    {
        _onPresetModified = Svc.PluginInterface.GetIpcSubscriber<Guid, bool, object>("Loci.OnPresetModified");

        _getPresetInfo = Svc.PluginInterface.GetIpcSubscriber<Guid, LociPresetInfo>("Loci.GetPresetInfo");
        _getAllPresetInfo = Svc.PluginInterface.GetIpcSubscriber<List<LociPresetInfo>>("Loci.GetAllPresetInfo");

        _applyPreset = Svc.PluginInterface.GetIpcSubscriber<Guid, object>("Loci.ApplyPreset");
        _applyPresets = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, object>("Loci.ApplyPresets");
        _applyPresetInfo = Svc.PluginInterface.GetIpcSubscriber<LociPresetInfo, object>("Loci.ApplyPresetInfo");
        _applyPresetInfos = Svc.PluginInterface.GetIpcSubscriber<List<LociPresetInfo>, object>("Loci.ApplyPresetInfos");

        // Normal Application
        _applyPresetByPtr = Svc.PluginInterface.GetIpcSubscriber<Guid, nint, object>("Loci.ApplyPresetByPtr");
        _applyPresetsByPtr = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, nint, object>("Loci.ApplyPresetsByPtr");
        _applyPresetByName = Svc.PluginInterface.GetIpcSubscriber<Guid, string, object>("Loci.ApplyPresetByName");
        _applyPresetsByName = Svc.PluginInterface.GetIpcSubscriber<List<Guid>, string, object>("Loci.ApplyPresetsByName");

        // later...
        // _ownPresetCombo = new SavedPresetsCombo(logger, loci, () => [.. loci.SavedPresets.OrderBy(s => s.Title)]);
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onPresetModified.Subscribe(OnPresetUpdated);
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Custom Presets IPCs.");
    }
    public void Unsubscribe()
    {
        _onPresetModified.Unsubscribe(OnPresetUpdated);
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Custom Presets IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnPresetUpdated(Guid presetId, bool wasDeleted)
        => _lastPresetUpdated = (presetId, wasDeleted);

    public static void KeyInput(ref uint key)
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
        var keyI = (int)key;
        if (ImGui.InputInt("Key", ref keyI, 0, 0))
            key = (uint)keyI;
    }

    public unsafe void Draw()
    {
        using var _ = ImRaii.TreeNode("Presets");
        if (!_) return;
        var width = ImGui.GetContentRegionAvail().X / 2;
        ImGuiUtil.GuidInput("Preset GUID##preset-id", "GUID...", "", ref _presetGuid, ref _presetGuidString, width);
        //var refId = _presetGuid ?? _lastPresetInfo.GUID;
        //if (_ownPresetCombo.Draw("preset-selector", refId, width, 1.15f))
        //{
        //    if (_ownPresetCombo.Current is { } valid)
        //    {
        //        _presetGuid = valid.GUID;
        //        _lastPresetInfo = valid.ToTuple();
        //    }
        //}
        //if (_lastPresetInfo.GUID != Guid.Empty)
        //{
        //    ImGui.SameLine();
        //    ImGui.Text("Stored Tuple:");
        //    ImUtf8.SameLineInner();
        //    using (ImRaii.Group())
        //    {
        //        foreach (var status in _lastPresetInfo)
        //    }
        //    LociIcon.Draw((uint)_lastPresetInfo.IconID, _lastPresetInfo.Stacks, LociIcon.Size);
        //    Utils.AttachTooltip(_lastPresetInfo, _allPresetInfo);
        //}

        // Target Types
        if (ImGui.InputTextWithHint("##presets-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple", disabled: !IsSubscribed))
            _lastPresetInfo = default;
        CkGui.AttachToolTip("Clears the cached preset tuple.");

        ImGui.InputTextWithHint("##presets-chara-name", "Player Name@World...", ref _actorName, 64);

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple List", disabled: !IsSubscribed))
            _allPresetInfo = [];
        CkGui.AttachToolTip("Clears the cached preset tuple list.");

        // Can polish this later.
        //if (_allPresetInfo.Count is not 0)
        //{
        //    using (CkRaii.FramedChildPaddedW("##manager-info", ImGui.GetContentRegionAvail().X, LociIcon.Size.Y, 0, SundCol.Gold.Uint(), 5f, 1f))
        //    {
        //        // Calculate the remaining height in the region.
        //        for (var i = 0; i < _allPresetInfo.Count; i++)
        //        {
        //            if (_allPresetInfo[i].IconID is 0)
        //                continue;

        //            LociIcon.Draw((uint)_allPresetInfo[i].IconID, _allPresetInfo[i].Stacks, LociIcon.Size);
        //            Utils.AttachTooltip(_allPresetInfo[i], _allPresetInfo);

        //            if (i < _allPresetInfo.Count)
        //                ImUtf8.SameLineInner();
        //        }
        //    }
        //}

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        var isGuidValid = _presetGuid.HasValue && _presetGuid != Guid.Empty;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorTextBool(_lastReturnCode ? "Success" : "Failure", _lastReturnCode);

        // Event monitor
        IpcTesterTab.DrawIpcRowStart("Last Modified Preset", _lastPresetUpdated.Preset.ToString());
        ImGui.TableNextColumn();
        ImGui.Text("Was Deleted?:");
        CkGui.BoolIcon(_lastPresetUpdated.WasDeleted, true);

        // Getting Data
        IpcTesterTab.DrawIpcRowStart("Loci.GetPresetInfo", "Get Preset Info");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            _lastPresetInfo = _getPresetInfo.InvokeFunc(_presetGuid!.Value);

        IpcTesterTab.DrawIpcRowStart("Loci.GetAllPresetInfo", "Get All Presets");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _allPresetInfo = _getAllPresetInfo.InvokeFunc() ?? [];

        // Client Application
        IpcTesterTab.DrawIpcRowStart("Loci.ApplyPreset", "Apply Preset (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || !isGuidValid))
            _applyPreset.InvokeAction(_presetGuid!.Value);

        IpcTesterTab.DrawIpcRowStart("Loci.ApplyPresetInfo", "Apply Tuple (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _lastPresetInfo.GUID == Guid.Empty))
            _applyPresetInfo.InvokeAction(_lastPresetInfo);

        // Normal Application
        IpcTesterTab.DrawIpcRowStart("Loci.ApplyPresetByPtr", "Apply by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorAddr == nint.Zero || !isGuidValid))
            _applyPresetByPtr.InvokeAction(_presetGuid!.Value, _actorAddr);

        IpcTesterTab.DrawIpcRowStart("Loci.ApplyPresetByName", "Apply by Name");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorName.Length is 0 || !isGuidValid))
            _applyPresetByName.InvokeAction(_presetGuid!.Value, _actorName);
    }
}