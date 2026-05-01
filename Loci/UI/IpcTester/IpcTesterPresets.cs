using CkCommons.Gui;
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

public class IpcTesterPresets : IIpcTesterGroup
{
    private readonly EventSubscriber<Guid, bool> _onPresetUpdated;

    private string _presetGuidString = string.Empty;
    private Guid? _presetGuid;

    private uint _lockCode = 0;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _playerName = string.Empty;
    private string _buddyName = string.Empty;

    private LociPresetInfo _lastPresetInfo;
    private List<LociPresetInfo> _allPresetInfo = [];
    private LociPresetSummary _lastPresetSummary;
    private List<LociPresetSummary> _lastBulkSummary = [];

    private LociApiEc _lastReturnCode;
    private (Guid Preset, bool WasDeleted) _lastPresetUpdated;

    private SavedPresetsCombo _ownPresetCombo;
    public IpcTesterPresets(ILogger<IpcTesterPresets> logger, LociManager loci)
    {
        _onPresetUpdated = PresetUpdated.Subscriber(Svc.PluginInterface, OnPresetUpdated);
        
        _ownPresetCombo = new SavedPresetsCombo(logger, loci, () => [.. LociData.Presets.OrderBy(s => s.Title)]);
        _ownPresetCombo.HintText = "Select a preset...";
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onPresetUpdated.Enable();
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Custom Presets IPCs.");
    }
    public void Unsubscribe()
    {
        _onPresetUpdated.Disable();
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
        var width = ImGui.GetContentRegionAvail().X / 2;
        ImGuiUtil.GuidInput("Preset GUID##preset-id", "GUID...", "", ref _presetGuid, ref _presetGuidString, width);
        var refId = _presetGuid ?? _lastPresetInfo.GUID;
        if (_ownPresetCombo.Draw("preset-selector", refId, width, 1.15f))
        {
            if (_ownPresetCombo.Current is { } valid)
            {
                _presetGuid = valid.GUID;
                _lastPresetInfo = valid.ToTuple();
            }
        }
        if (_lastPresetInfo.GUID != Guid.Empty)
        {
            ImGui.SameLine();
            ImGui.Text("Stored Tuple:");
            ImUtf8.SameLineInner();
            CkGui.ColorTextInline($"{_lastPresetInfo.Title} | {_lastPresetInfo.Description}", ImGuiColors.DalamudViolet);
        }

        // Target Types
        if (ImGui.InputTextWithHint("##presets-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple", disabled: !IsSubscribed))
            _lastPresetInfo = default;
        CkGui.AttachTooltip("Clears the cached preset tuple.");

        ImGui.InputTextWithHint("##presets-actor-name", "Player Name@World...", ref _playerName, 64);
        CkGui.AttachTooltip("Make this PlayerName@World when working with players, and PlayerName when with pets.");

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple List", disabled: !IsSubscribed))
            _allPresetInfo = [];
        CkGui.AttachTooltip("Clears the cached preset tuple list.");

        ImGui.InputTextWithHint("##presets-buddy-name", "Pet/Minion/Companion Name...", ref _buddyName, 64);

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        var isGuidValid = _presetGuid.HasValue && _presetGuid != Guid.Empty;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorText(_lastReturnCode.ToString(), ImGuiColors.DalamudYellow);

        // Event monitor
        IpcTesterUI.DrawIpcRowStart("Last Modified Preset", _lastPresetUpdated.Preset.ToString());
        ImGui.TableNextColumn();
        ImGui.Text("Was Deleted?:");
        CkGui.BoolIcon(_lastPresetUpdated.WasDeleted, true);

        // Getting Data
        IpcTesterUI.DrawIpcRowStart(GetPresetInfo.Label, "Get Preset Info");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastPresetInfo) = new GetPresetInfo(Svc.PluginInterface).Invoke(_presetGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetPresetInfoList.Label, "Get All Preset Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _allPresetInfo = new GetPresetInfoList(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(GetPresetSummary.Label, "Get Preset Summary");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastPresetSummary) = new GetPresetSummary(Svc.PluginInterface).Invoke(_presetGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetPresetSummaryList.Label, "Get All Preset Summaries");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastBulkSummary = new GetPresetSummaryList(Svc.PluginInterface).Invoke();

        // Application.
        IpcTesterUI.DrawIpcRowStart(ApplyPreset.Label, "Apply Preset (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new ApplyPreset(Svc.PluginInterface).Invoke(_presetGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(ApplyPresetInfo.Label, "Apply Tuple (Client)");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _lastPresetInfo.GUID == Guid.Empty))
            _lastReturnCode = new ApplyPresetInfo(Svc.PluginInterface).Invoke(_lastPresetInfo, _lockCode);

        IpcTesterUI.DrawIpcRowStart(ApplyPresetByPtr.Label, "Apply by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Apply", disabled: !IsSubscribed || _actorAddr == nint.Zero || !isGuidValid))
            _lastReturnCode = new ApplyPresetByPtr(Svc.PluginInterface).Invoke(_presetGuid!.Value, _actorAddr);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(ApplyPresetByName.Label, "Apply by Player Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Apply to Player", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
            _lastReturnCode = new ApplyPresetByName(Svc.PluginInterface).Invoke(_presetGuid!.Value, _playerName);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Upload, "Apply to Buddy", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
                _lastReturnCode = new ApplyPresetByName(Svc.PluginInterface).Invoke(_presetGuid!.Value, _playerName, _buddyName);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }

        // Removal
        IpcTesterUI.DrawIpcRowStart(RemovePreset.Label, "Remove (Client)");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new RemovePreset(Svc.PluginInterface).Invoke(_presetGuid!.Value, _lockCode);

        IpcTesterUI.DrawIpcRowStart(RemovePresetByPtr.Label, "Remove by Ptr");
        if (CkGui.SmallIconTextButton(FAI.Trash, "Remove", disabled: !IsSubscribed || _actorAddr == nint.Zero))
            _lastReturnCode = new RemovePresetByPtr(Svc.PluginInterface).Invoke(_presetGuid!.Value, _actorAddr);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        IpcTesterUI.DrawIpcRowStart(RemovePresetByName.Label, "Remove by Player Name");
        if (CkGui.SmallIconTextButton(FAI.Upload, "Remove from Player", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
            _lastReturnCode = new RemovePresetByName(Svc.PluginInterface).Invoke(_presetGuid!.Value, _playerName);
        CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);

        if (_buddyName.Length > 0)
        {
            ImUtf8.SameLineInner();
            if (CkGui.SmallIconTextButton(FAI.Upload, "Remove from Buddy", disabled: !IsSubscribed || _playerName.Length == 0 || !isGuidValid))
                _lastReturnCode = new RemovePresetByName(Svc.PluginInterface).Invoke(_presetGuid!.Value, _playerName, _buddyName);
            CkGui.AttachTooltip("--COL--WARNING:--COL----NL--This will desync any actors that are ephemeral! (External plugins)", ImGuiColors.DalamudRed);
        }
    }
}