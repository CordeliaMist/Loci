using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using LociApi.Enums;
using LociApi.Helpers;
using LociApi.Ipc;
using OtterGui;
using System.Globalization;

namespace Loci.Gui;

public class IpcTesterEvents : IIpcTesterGroup
{
    private readonly EventSubscriber<Guid, bool> _onEventUpdated;
    private readonly EventSubscriber<Guid, string, string> _onEventPathMoved;

    private string _lociEventGuidString = string.Empty;
    private Guid? _lociEventGuid;

    private uint _lockCode = 0;

    private string _actorAddrString = string.Empty;
    private nint _actorAddr = nint.Zero;
    private string _playerName = string.Empty;
    private string _buddyName = string.Empty;
    public string _eventName = string.Empty;

    private Dictionary<Guid, string> _lastEventList = [];
    private LociEventInfo _lastEventInfo;
    private List<LociEventInfo> _allEventInfo = [];
    private LociEventSummary _lastEventSummary;
    private List<LociEventSummary> _lastBulkSummary = [];

    private LociApiEc _lastReturnCode;
    private (Guid Event, bool WasDeleted) _lastEventUpdated;
    private (Guid Event, string OldPath, string NewPath) _lastEventPathMove;

    // Add event combo here later...
    public IpcTesterEvents(ILogger<IpcTesterEvents> logger, LociManager loci)
    {
        _onEventUpdated = EventUpdated.Subscriber(Svc.PluginInterface, OnEventUpdated);
        _onEventPathMoved = EventPathMoved.Subscriber(Svc.PluginInterface, OnEventPathMoved);

        //_ownEventCombo = new SavedEventsCombo(logger, loci, () => [.. LociData.Events.OrderBy(s => s.Title)]);
        //_ownEventCombo.HintText = "Select a lociEvent...";
    }

    public bool IsSubscribed { get; private set; }

    public void Subscribe()
    {
        _onEventUpdated.Enable();
        _onEventPathMoved.Enable();
        IsSubscribed = true;
        Svc.Logger.Information("Subscribed to Custom Events IPCs.");
    }
    public void Unsubscribe()
    {
        _onEventUpdated.Disable();
        _onEventPathMoved.Disable();
        IsSubscribed = false;
        Svc.Logger.Information("Unsubscribed from Custom Events IPCs.");
    }

    public void Dispose()
        => Unsubscribe();

    private void OnEventUpdated(Guid lociEventId, bool wasDeleted)
        => _lastEventUpdated = (lociEventId, wasDeleted);

    private void OnEventPathMoved(Guid lociEventId, string oldPath, string newPath)
        => _lastEventPathMove = (lociEventId, oldPath, newPath);

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
        ImGuiUtil.GuidInput("Event GUID##lociEvent-id", "GUID...", "", ref _lociEventGuid, ref _lociEventGuidString, width);
        var refId = _lociEventGuid ?? _lastEventInfo.GUID;

        // Target Types
        if (ImGui.InputTextWithHint("##lociEvents-chara-addr", "Player Address..", ref _actorAddrString, 16, ImGuiInputTextFlags.CharsHexadecimal))
            _actorAddr = nint.TryParse(_actorAddrString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tmp) ? tmp : nint.Zero;

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple", disabled: !IsSubscribed))
            _lastEventInfo = default;
        CkGui.AttachTooltip("Clears the cached lociEvent tuple.");

        ImGui.InputTextWithHint("##lociEvents-actor-name", "Player Name@World...", ref _playerName, 64);
        CkGui.AttachTooltip("Make this PlayerName@World when working with players, and PlayerName when with pets.");

        ImGui.SameLine();
        if (CkGui.IconTextButton(FAI.Times, "Clear Cached Tuple List", disabled: !IsSubscribed))
            _allEventInfo = [];
        CkGui.AttachTooltip("Clears the cached lociEvent tuple list.");

        ImGui.InputTextWithHint("##lociEvents-buddy-name", "Pet/Minion/Companion Name...", ref _buddyName, 64);

        ImGui.InputTextWithHint("##new-event-name", "New Event Name...", ref _eventName, 64);

        using var table = ImRaii.Table(string.Empty, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        var isGuidValid = _lociEventGuid.HasValue && _lociEventGuid != Guid.Empty;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Last Return Code");
        ImGui.TableNextColumn();
        CkGui.ColorText(_lastReturnCode.ToString(), ImGuiColors.DalamudYellow);

        // Event monitor
        IpcTesterUI.DrawIpcRowStart("Last Modified Event", _lastEventUpdated.Event.ToString());
        ImGui.TableNextColumn();
        ImGui.Text("Was Deleted?:");
        CkGui.BoolIcon(_lastEventUpdated.WasDeleted, true);

        // Getting Data
        IpcTesterUI.DrawIpcRowStart(GetEventList.Label, "Get Event List");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastEventList = new GetEventList(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(GetEventInfo.Label, "Get Event Info");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastEventInfo) = new GetEventInfo(Svc.PluginInterface).Invoke(_lociEventGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetEventInfoList.Label, "Get All Event Info");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _allEventInfo = new GetEventInfoList(Svc.PluginInterface).Invoke();

        IpcTesterUI.DrawIpcRowStart(GetEventSummary.Label, "Get Event Summary");
        if (CkGui.SmallIconTextButton(FAI.Search, "Get", disabled: !IsSubscribed || !isGuidValid))
            (_lastReturnCode, _lastEventSummary) = new GetEventSummary(Svc.PluginInterface).Invoke(_lociEventGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(GetEventSummaryList.Label, "Get All Event Summaries");
        if (CkGui.SmallIconTextButton(FAI.List, "Get", disabled: !IsSubscribed))
            _lastBulkSummary = new GetEventSummaryList(Svc.PluginInterface).Invoke();

        // Event Handling
        IpcTesterUI.DrawIpcRowStart(CreateEvent.Label, "Create Event");
        if (CkGui.SmallIconTextButton(FAI.Plus, "Create", disabled: !IsSubscribed || _eventName.Length is 0))
            _lociEventGuid = new CreateEvent(Svc.PluginInterface).Invoke(_eventName, string.Empty, LociEventType.JobChange);

        IpcTesterUI.DrawIpcRowStart(DeleteEvent.Label, "Delete Event");
        if (CkGui.SmallIconTextButton(FAI.Times, "Delete", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new DeleteEvent(Svc.PluginInterface).Invoke(_lociEventGuid!.Value);

        IpcTesterUI.DrawIpcRowStart(SetEventState.Label, "Set Enable State");
        if (CkGui.SmallIconTextButton(FAI.ToggleOn, "Enable", disabled: !IsSubscribed || !isGuidValid))
            _lastReturnCode = new SetEventState(Svc.PluginInterface).Invoke(_lociEventGuid!.Value, true);
        CkGui.AttachTooltip("Only sets to on right now");
    }
}