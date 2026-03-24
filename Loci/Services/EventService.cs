using CkCommons;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Loci.Data;
using Loci.Services.Mediator;
using LociApi.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TerraFX.Interop.Windows;

namespace Loci.Services;

public class EventService : DisposableMediatorSubscriberBase
{
    private const int CUSTOMIZE_LENGTH = 26;
    private record EventJobCache(Guid EventId, bool IsJob, JobFlags JobFlags, int GearsetIdx, List<LociStatus> Statuses)
        : EventCache(EventId, Statuses)
    {
        public bool IsDifferent(JobFlags job, int gearsetIdx)
            => IsJob ? !JobFlags.Has(job) : GearsetIdx != gearsetIdx;
    }

    private record EventCache(Guid EventId, List<LociStatus> Statuses);

    private readonly LociEventData _data;

    // For previous locations to reference in comparisons.
    private DateTime _delayedUpdateCheck = DateTime.Now;

    private ushort _latestTerritory = 0;
    private IntendedUseEnum _latestIntendedUse = IntendedUseEnum.UNK;
    private byte _latestOnlineStatus = 0;
    private CharaRace _latestRace = 0;
    private CharaGender _latestGender = 0;

    public EventService(ILogger<EventService> logger, LociMediator mediator, LociEventData data)
        : base(logger, mediator)
    {
        _data = data;

        Mediator.Subscribe<GearsetChangedMessage>(this, _ => OnJobGearsetChange(_.PrevGearsetIdx, _.PrevJobId, _.NewGearsetIdx, _.NewJobId));
        Mediator.Subscribe<EmotePerformedMessage>(this, _ => OnEmotePerformed(_.EmoteId, _.Caller, _.Target));

        // Listen to zone changes.
        Svc.ClientState.Login += WaitAndLoadInitialData;
        Svc.ClientState.Logout += OnLogout;
        Svc.ClientState.ZoneInit += OnZoneInit;
        Svc.Framework.Update += OnTick;

        if (Svc.ClientState.IsLoggedIn)
            WaitAndLoadInitialData();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Svc.ClientState.Login -= WaitAndLoadInitialData;
        Svc.ClientState.Logout -= OnLogout;
        Svc.ClientState.ZoneInit -= OnZoneInit;
        Svc.Framework.Update -= OnTick;
    }
    private async void WaitAndLoadInitialData()
    {
        await Utils.WaitForPlayerLoading();
        LoadInitialData();
    }

    private unsafe void LoadInitialData()
    {
        _latestTerritory = PlayerContent.TerritoryIdInstanced;
        _latestIntendedUse = PlayerContent.TerritoryIntendedUse;
        _latestOnlineStatus = PlayerData.Character->OnlineStatus;

        Logger.LogDebug($"Initial zone: {_latestTerritory} ({PlayerContent.GetTerritoryName(_latestTerritory)}) <{_latestIntendedUse}>", LoggerType.Events);
        Logger.LogDebug($"Initial OnlineStatus: {_latestOnlineStatus}", LoggerType.Events);
        //OnZoneChanged(0, IntendedUseEnum.UNK, _latestTerritory, _latestIntendedUse);
        //OnOnlineStatusChange(0, _latestOnlineStatus);
    }

    private async void OnLogout(int type, int code)
    {
        _latestTerritory = 0;
        _latestIntendedUse = IntendedUseEnum.UNK;
        _latestOnlineStatus = 0;
        _latestRace = 0;
    }

    private async void OnZoneInit(ZoneInitEventArgs args)
    {
        if (!Svc.ClientState.IsLoggedIn)
            return;
        Logger.LogTrace($"Zone initialized: {args.ToString()}", LoggerType.Processors);
        Logger.LogDebug($"Territory changed to: {args.TerritoryType.RowId} ({PlayerContent.GetTerritoryName((ushort)args.TerritoryType.RowId)})", LoggerType.Processors);
        var prevTerritory = _latestTerritory;
        var prevIntendedUse = _latestIntendedUse;

        // Await for the player to be loaded.
        await Utils.WaitForPlayerLoading();
        _latestTerritory = PlayerContent.TerritoryIdInstanced;
        _latestIntendedUse = PlayerContent.TerritoryIntendedUse;

        OnZoneChanged(prevTerritory, prevIntendedUse, _latestTerritory, _latestIntendedUse);
    }

    private unsafe void OnTick(IFramework framework)
    {
        if (!PlayerData.Available)
            return;

        if (DateTime.Now < _delayedUpdateCheck.AddSeconds(1))
        {
            Mediator.Publish(new DelayedFrameworkUpdateMessage());
            _delayedUpdateCheck = DateTime.Now;
        }

        if (PlayerData.Character->OnlineStatus != _latestOnlineStatus)
        {
            var prevStatus = _latestOnlineStatus;
            _latestOnlineStatus = PlayerData.Character->OnlineStatus;
            OnOnlineStatusChange(prevStatus, _latestOnlineStatus);
        }

        // Check race/sex changes
        var drawObj = PlayerData.Character->DrawObject;
        if (drawObj == null || drawObj->Object.GetObjectType() != ObjectType.CharacterBase)
            return;

        try
        {
            var human = (Human*)drawObj;
            var customize = human->Customize;
            var curRace = (CharaRace)customize.Race;
            var curSex = (CharaGender)customize.Sex;

            if (curRace != _latestRace)
            {
                var prevRace = _latestRace;
                _latestRace = curRace;
                OnRaceChange(prevRace, curRace);
            }
        }
        catch (Exception e)
        {
            Logger.LogError($"Error while checking race change: {e}");
        }
    }

    private EventCache? _lastRaceCondition;
    private void OnRaceChange(CharaRace prevRace, CharaRace newRace)
    {
        // Ignore when the gearset idx didnt change.
        if (prevRace == newRace)
            return;

        Logger.LogDebug($"RaceChange [Old: {prevRace}] -> [New: {newRace}] (Had CondEvent: {_lastRaceCondition != null})", LoggerType.Events);

        var eventInThatCond = _lastRaceCondition?.EventId ?? Guid.Empty;
        // Remove the previous condition.
        if (_lastRaceCondition is not null)
        {
            foreach (var status in _lastRaceCondition.Statuses)
                LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked);
            _lastRaceCondition = null;
        }

        // Filter out events here.
        var candidates = LociEventData.Events.Where(IsValid).OrderByDescending(e => e.Priority).ToList();
        if (candidates.Count is 0)
            return;

        // Attempt application
        Logger.LogTrace($"Found {candidates.Count} candidate events for race change, attempting application.", LoggerType.Events);
        foreach (var candidate in candidates)
        {
            if (candidate.ReactionType is ChainType.Status && TryApplyStatusEvent(candidate, out var appliedStatus))
            {
                Logger.LogDebug($"Applied race change event: {candidate.Title}, applying status {appliedStatus[0].Title}.", LoggerType.Events);
                // Set the last condition if the applied statuses had anything. Otherwise, break out.
                if (appliedStatus.Count > 0)
                    _lastRaceCondition = new EventCache(candidate.GUID, appliedStatus);
                break;
            }
            else if (candidate.ReactionType is ChainType.Preset && TryApplyPresetEvent(candidate, out var appliedStatuses))
            {
                Logger.LogDebug($"Applied race change event: {candidate.Title} with {appliedStatuses.Count} statuses applied from preset.", LoggerType.Events);
                if (appliedStatuses.Count > 0)
                    _lastRaceCondition = new EventCache(candidate.GUID, appliedStatuses);
                break;
            }
        }

        bool IsValid(LociEvent e)
            => e.Enabled && e.EventType is LociEventType.Race && e.IndicatedID == (ushort)newRace;
    }


    private EventJobCache? _lastJobCondition;
    private void OnJobGearsetChange(int prevGearsetIdx, byte prevJobId, int newGearsetIdx, byte newJobId)
    {
        // Ignore when the gearset idx didnt change.
        if (prevGearsetIdx == newGearsetIdx)
            return;

        var prevJobFlag = (JobFlags)(1UL << prevJobId);
        var newJobFlag = (JobFlags)(1UL << newJobId);
        Logger.LogDebug($"GearsetChange [IDX: {prevGearsetIdx} ({prevJobFlag})] -> [IDX: {newGearsetIdx} ({newJobFlag})] (Had CondEvent: {_lastJobCondition != null})", LoggerType.Events);

        var eventInThatCond = _lastJobCondition?.EventId ?? Guid.Empty;
        // Remove the previous condition.
        if (_lastJobCondition is { } jobCond && jobCond.IsDifferent(newJobFlag, newGearsetIdx))
        {
            foreach (var status in _lastJobCondition.Statuses)
                LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked);
            _lastJobCondition = null;
        }

        // Filter out events here.
        var candidates = LociEventData.Events.Where(IsValid).OrderByDescending(e => e.Priority).ToList();
        if (candidates.Count is 0)
            return;

        // Attempt application
        Logger.LogTrace($"Found {candidates.Count} candidate events for job/gearset change, attempting application.", LoggerType.Events);
        foreach (var candidate in candidates)
        {
            if (candidate.ReactionType is ChainType.Status && TryApplyStatusEvent(candidate, out var appliedStatus))
            {
                Logger.LogDebug($"Applied job change event: {candidate.Title}, applying status {appliedStatus[0].Title}.", LoggerType.Events);
                // Set the last condition if the applied statuses had anything. Otherwise, break out.
                if (appliedStatus.Count > 0)
                    _lastJobCondition = new EventJobCache(candidate.GUID, candidate.GearsetIdx == -1, candidate.JobFlags, candidate.GearsetIdx, appliedStatus);
                break;
            }
            else if (candidate.ReactionType is ChainType.Preset && TryApplyPresetEvent(candidate, out var appliedStatuses))
            {
                Logger.LogDebug($"Applied job change event: {candidate.Title} with {appliedStatuses.Count} statuses applied from preset.", LoggerType.Events);
                if (appliedStatuses.Count > 0)
                    _lastJobCondition = new EventJobCache(candidate.GUID, candidate.GearsetIdx == -1, candidate.JobFlags, candidate.GearsetIdx, appliedStatuses);
                break;
            }
        }

        bool IsValid(LociEvent e)
        {
            if (!e.Enabled || e.EventType is not LociEventType.JobChange)
                return false;
            // prevent looping
            if (e.GUID == eventInThatCond)
                return false;
            // Ret conditional for gearset/job based
            return (e.GearsetIdx == -1) ? e.JobFlags is JobFlags.None || e.JobFlags.Has(newJobFlag) : e.GearsetIdx == (short)newGearsetIdx;
        }
    }

    private EventCache? _lastEmoteCondition;
    private unsafe void OnEmotePerformed(ushort emoteId, nint callerAddr, nint targetAddr)
    {
        var caller = (GameObject*)callerAddr;
        var target = (GameObject*)targetAddr;
        // Caller must be something, (Target can be nothing)
        if (!CharaWatcher.Rendered.Contains(callerAddr))
            return;

        // Filter based on the type.
        var isClientRendered = PlayerData.Available;
        var clientIsCaller = isClientRendered && callerAddr == PlayerData.Address;
        var clientIsTarget = isClientRendered && targetAddr == PlayerData.Address;

        var eventInThatCond = _lastEmoteCondition?.EventId ?? Guid.Empty;
        // Remove the previous condition.
        if (_lastEmoteCondition is not null)
        {
            foreach (var status in _lastEmoteCondition.Statuses)
                LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked);
            _lastEmoteCondition = null;
        }

        // Filter out events here.
        var candidates = LociEventData.Events.Where(IsValid).OrderByDescending(e => e.Priority).ToList();
        if (candidates.Count is 0)
            return;

        Logger.LogTrace($"Found {candidates.Count} candidate events for emote performed, attempting application.", LoggerType.Events);
        foreach (var candidate in candidates)
        {
            if (candidate.ReactionType is ChainType.Status && TryApplyStatusEvent(candidate, out var appliedStatus))
            {
                Logger.LogDebug($"Applied emote event: {candidate.Title}, applying status {appliedStatus[0].Title}.", LoggerType.Events);
                // Set the last condition if the applied statuses had anything. Otherwise, break out.
                if (appliedStatus.Count > 0)
                    _lastEmoteCondition = new EventCache(candidate.GUID, appliedStatus);
                break;
            }
            else if (candidate.ReactionType is ChainType.Preset && TryApplyPresetEvent(candidate, out var appliedStatuses))
            {
                Logger.LogDebug($"Applied emote event: {candidate.Title} with {appliedStatuses.Count} statuses applied from preset.", LoggerType.Events);
                if (appliedStatuses.Count > 0)
                    _lastEmoteCondition = new EventCache(candidate.GUID, appliedStatuses);
                break;
            }
        }

        bool IsValid(LociEvent ee)
        {
            if (!ee.Enabled || ee.EventType is not LociEventType.Emote || ee.IndicatedID != emoteId)
                return false;

            switch (ee.Direction)
            {
                case KnownDirection.Any:
                    return true;
                case KnownDirection.OtherToSelf:
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && !clientIsCaller && clientIsTarget)) return false;
                    return string.IsNullOrEmpty(ee.WhitelistedName) || Utils.ToLociName((Character*)callerAddr) == ee.WhitelistedName;
                case KnownDirection.Other:
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && !clientIsCaller)) return false;
                    return string.IsNullOrEmpty(ee.WhitelistedName) || Utils.ToLociName((Character*)targetAddr) == ee.WhitelistedName;
                case KnownDirection.SelfToOther:
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && clientIsCaller)) return false;
                    return string.IsNullOrEmpty(ee.WhitelistedName) || Utils.ToLociName((Character*)targetAddr) == ee.WhitelistedName;
                case KnownDirection.Self:
                    return clientIsCaller;
                default:
                    return false;
            }
        }
    }

    private EventCache? _lastZoneCondition;
    private void OnZoneChanged(ushort prevTerritory, IntendedUseEnum prevUse, ushort newTerritory, IntendedUseEnum newUse)
    {
        if (prevTerritory == newTerritory && prevUse == newUse)
            return;

        Logger.LogDebug($"ZoneChange [{PlayerContent.GetTerritoryName(prevTerritory)} ({prevTerritory}) <{prevUse}>] " +
            $"-> [{PlayerContent.GetTerritoryName(newTerritory)} ({newTerritory}) <{newUse}>] (Had CondEvent: {_lastZoneCondition != null})", LoggerType.Events);

        var eventInThatCond = _lastZoneCondition?.EventId ?? Guid.Empty;
        // Remove the previous condition.
        if (_lastZoneCondition is not null)
        {
            foreach (var status in _lastZoneCondition.Statuses)
                LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked);
            _lastZoneCondition = null;
        }

        // Filter out events here.
        var candidates = LociEventData.Events.Where(IsValid).OrderByDescending(e => e.Priority).ToList();

        Logger.LogDebug($"Found {candidates.Count} candidate events for zone change, attempting application.", LoggerType.Events);
        foreach (var candidate in candidates)
        {
            if (candidate.ReactionType is ChainType.Status && TryApplyStatusEvent(candidate, out var appliedStatus))
            {
                // Set the last condition if the applied statuses had anything. Otherwise, break out.
                if (appliedStatus.Count > 0)
                {
                    Logger.LogDebug($"Applied zone change event: {candidate.Title}, applying status {appliedStatus[0].Title}.", LoggerType.Events);
                    _lastZoneCondition = new EventCache(candidate.GUID, appliedStatus);
                }

                break;
            }
            else if (candidate.ReactionType is ChainType.Preset && TryApplyPresetEvent(candidate, out var appliedStatuses))
            {
                Logger.LogDebug($"Applied zone change event: {candidate.Title} with {appliedStatuses.Count} statuses applied from preset.", LoggerType.Events);
                if (appliedStatuses.Count > 0)
                    _lastZoneCondition = new EventCache(candidate.GUID, appliedStatuses);
                break;
            }
        }

        bool IsValid(LociEvent e)
        {
            if (!e.Enabled || e.EventType is not LociEventType.ZoneBased)
                return false;
            // Ret based on type
            return e.IntendedUse is IntendedUseEnum.UNK ? e.IndicatedID == newTerritory : e.IntendedUse == newUse;
        }
    }

    private EventCache? _lastOnlineStatusCondition;
    private void OnOnlineStatusChange(byte lastOnlineStatus, byte newOnlineStatus)
    {
        if (lastOnlineStatus == newOnlineStatus)
            return;

        Logger.LogDebug($"OnlineStatusChange [{lastOnlineStatus}] -> [{newOnlineStatus}] (Had CondEvent: {_lastOnlineStatusCondition != null})", LoggerType.Events);

        var eventInThatCond = _lastOnlineStatusCondition?.EventId ?? Guid.Empty;
        // Remove the previous condition.
        if (_lastOnlineStatusCondition is not null)
        {
            Logger.LogDebug($"Removing previous OnlineStatus condition with event {eventInThatCond} and {_lastOnlineStatusCondition.Statuses.Count} statuses.", LoggerType.Events);
            foreach (var status in _lastOnlineStatusCondition.Statuses)
                LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked);
            _lastOnlineStatusCondition = null;
        }

        // Filter out events here.
        var candidates = LociEventData.Events.Where(IsValid).OrderByDescending(e => e.Priority).ToList();
        if (candidates.Count is 0)
            return;

        // Attempt application
        Logger.LogDebug($"Found {candidates.Count} candidate events for OnlineStatus change, attempting application.", LoggerType.Events);
        foreach (var candidate in candidates)
        {
            if (candidate.ReactionType is ChainType.Status && TryApplyStatusEvent(candidate, out var appliedStatus))
            {
                Logger.LogDebug($"Applied OnlineStatus change event: {candidate.Title}, applying status {appliedStatus[0].Title}.", LoggerType.Events);
                // Set the last condition if the applied statuses had anything. Otherwise, break out.
                if (appliedStatus.Count > 0)
                {
                    Logger.LogDebug($"Setting last OnlineStatus condition with event {candidate.Title} and status {appliedStatus[0].Title}.", LoggerType.Events);
                    _lastOnlineStatusCondition = new EventCache(candidate.GUID, appliedStatus);
                }
                break;
            }
            else if (candidate.ReactionType is ChainType.Preset && TryApplyPresetEvent(candidate, out var appliedStatuses))
            {
                Logger.LogDebug($"Applied OnlineStatus change event: {candidate.Title} with {appliedStatuses.Count} statuses applied from preset.", LoggerType.Events);
                if (appliedStatuses.Count > 0)
                {
                    Logger.LogDebug($"Setting last OnlineStatus condition with event {candidate.Title} and {appliedStatuses.Count} statuses from preset.", LoggerType.Events);
                    _lastOnlineStatusCondition = new EventCache(candidate.GUID, appliedStatuses);
                }
                break;
            }
        }

        bool IsValid(LociEvent e)
            => e.Enabled && e.EventType is LociEventType.OnlineStatus && e.IndicatedID == newOnlineStatus;
    }

    // Iterates through the candidates, attempting to apply the first valid event.
    private bool TryApplyStatusEvent(LociEvent e, [NotNullWhen(true)] out List<LociStatus> applied)
    {
        applied = [];
        var flags = ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked;

        if (LociData.Statuses.FirstOrDefault(s => s.GUID == e.ReactionGUID) is not { } data)
            return false;

        var existing = LociManager.ClientSM.Statuses.FirstOrDefault(s => s.GUID == data.GUID);

        switch (e.Behavior)
        {
            case EventBehavior.Apply:
                if (existing is not null) return false;
                return LociManager.ClientSM.AddOrUpdate(data.PreApply(), flags) is not null;

            case EventBehavior.ApplyAuthorative:
                LociManager.ClientSM.Cancel(data, flags);
                return LociManager.ClientSM.AddOrUpdate(data.PreApply(), flags) is not null;

            case EventBehavior.InThatCondition:
                if (existing is not null) return true;
                if (LociManager.ClientSM.AddOrUpdate(data.PreApply(), flags) is not null)
                {
                    applied.Add(data);
                    return true;
                }
                return false;

            case EventBehavior.InThatConditionAuthorative:
                LociManager.ClientSM.Cancel(data, flags);
                if (LociManager.ClientSM.AddOrUpdate(data.PreApply(), flags) is not null)
                {
                    applied.Add(data);
                    return true;
                }
                return false;

            case EventBehavior.Remove:
                return LociManager.ClientSM.Cancel(data, flags);
        }

        return false;
    }

    private bool TryApplyPresetEvent(LociEvent e, [NotNullWhen(true)] out List<LociStatus> applied)
    {
        applied = [];
        var flags = ManagerChangeType.ApplyRemove | ManagerChangeType.EventInvoked;

        if (LociData.Presets.FirstOrDefault(p => p.GUID == e.ReactionGUID) is not { } data)
            return false;

        if (data.ApplyType is PresetApplyType.ReplaceAll)
            data.ApplyType = PresetApplyType.UpdateExisting;

        switch (e.Behavior)
        {
            case EventBehavior.Apply:
                return LociManager.ClientSM.ApplyPreset(data, flags).Count > 0;

            case EventBehavior.ApplyAuthorative:
                LociManager.ClientSM.RemovePreset(data, flags);
                return LociManager.ClientSM.ApplyPreset(data, flags).Count > 0;

            case EventBehavior.InThatCondition:
                applied = LociManager.ClientSM.ApplyPreset(data, flags);
                return applied.Count > 0;

            case EventBehavior.InThatConditionAuthorative:
                LociManager.ClientSM.RemovePreset(data, flags);
                applied = LociManager.ClientSM.ApplyPreset(data, flags);
                return applied.Count > 0;

            case EventBehavior.Remove:
                return LociManager.ClientSM.RemovePreset(data, flags) > 0;
        }

        return false;
    }
}
