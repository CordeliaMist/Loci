using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi;
using LociApi.Api;
using LociApi.Enums;

namespace Loci.Api;
public class StatusApi : DisposableMediatorSubscriberBase, ILociApiStatuses
{
    private readonly ApiHelpers _helpers;
    private readonly LociManager _manager;
    private readonly LociData _data;

    public StatusApi(ILogger<StatusApi> logger, LociMediator mediator,
        ApiHelpers helpers, LociManager manager, LociData data) 
        : base(logger, mediator)
    {
        _helpers = helpers;
        _manager = manager;
        _data = data;
        Mediator.Subscribe<LociStatusChanged>(this, _ => OnStatusUpdated(_.Item.GUID, _.Type is FSChangeType.Deleted));
        Mediator.Subscribe<ChainTriggerHitMessage>(this, OnChainTriggerHit);
    }

    public (LociApiEc, LociStatusInfo) GetStatusInfo(Guid guid)
    {
        if (LociData.Statuses.FirstOrDefault(s => s.GUID == guid) is not { } status)
            return (LociApiEc.DataNotFound, default);

        return (LociApiEc.Success, status.ToTuple());
    }

    public List<LociStatusInfo> GetStatusInfoList()
        => LociData.Statuses.Select(s => s.ToTuple()).ToList();

    public (LociApiEc, LociStatusSummary) GetStatusSummary(Guid guid)
    {
        if (LociData.Statuses.FirstOrDefault(s => s.GUID == guid) is not { } status)
            return (LociApiEc.DataNotFound, default);
        // Get the summary for the status and return it
        return (LociApiEc.Success, _helpers.ToSavedStatusSummary(status));
    }

    public List<LociStatusSummary> GetStatusSummaryList()
        => _helpers.ToSavedStatusesSummary();

    public LociApiEc ApplyStatus(Guid statusId, uint key)
    {
        if (LociData.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        // Modify the status manager
        LociManager.ClientSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove, true, key);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyStatuses(List<Guid> ids, uint key, out List<Guid> failed)
    {
        failed = [];
        var lookup = LociData.Statuses.ToDictionary(s => s.GUID);
        foreach (var id in ids)
        {
            // Fail if this is locked and we do not have a matching key
            if (LociManager.ClientSM.LockedStatuses.TryGetValue(id, out var lockKey) && lockKey != key)
            {
                failed.Add(id);
                continue;
            }
            // Fail if not a valid status
            if (!lookup.TryGetValue(id, out var status))
            {
                failed.Add(id);
                continue;
            }
            // Update the manager
            LociManager.ClientSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove, true, key);
        }

        return failed.Count == ids.Count ? LociApiEc.DataInvalid : failed.Count > 0 ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc ApplyStatusInfo(LociStatusInfo statusInfo, uint key)
    {
        // If this null, we can imply it failed by either being invalid or a lock.
        if (LociManager.ClientSM.AddOrUpdate(statusInfo.ToSavedStatus().PreApply(), ManagerChangeType.ApplyRemove, true, key) is null)
            return LociManager.ClientSM.LockedStatuses.ContainsKey(statusInfo.GUID) ? LociApiEc.ItemLocked : LociApiEc.DataInvalid;

        // Otherwise it was a valid application, so we can return success.
        return LociApiEc.Success;
    }

    public LociApiEc ApplyStatusInfos(List<LociStatusInfo> statusInfos, uint key)
    {
        var failed = 0;
        foreach (var statusInfo in statusInfos)
        {
            // If this null, we can imply it failed by either being invalid or a lock.
            if (LociManager.ClientSM.AddOrUpdate(statusInfo.ToSavedStatus().PreApply(), ManagerChangeType.ApplyRemove, true, key) is null)
                failed++;
        }

        // There is some ambiguity here on if the data being invalid caused the error or the key, perhaps improve this later.
        return failed == statusInfos.Count ? LociApiEc.DataInvalid : failed > 0 ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc ApplyStatusByPtr(Guid statusId, nint address)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return LociApiEc.TargetNotFound;

        if (LociData.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        actorSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyStatusesByPtr(List<Guid> statusIds, nint address, out List<Guid> failed)
    {
        if (!CharaWatcher.Rendered.Contains(address))
        {
            failed = statusIds;
            return LociApiEc.TargetInvalid;
        }

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
        {
            failed = statusIds;
            return LociApiEc.TargetNotFound;
        }

        failed = [];
        var lookup = LociData.Statuses.ToDictionary(s => s.GUID);
        foreach (var statusId in statusIds)
        {
            if (!lookup.TryGetValue(statusId, out var status))
            {
                failed.Add(statusId);
                continue;
            }
            if (actorSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove) is null)
            {
                failed.Add(statusId);
                continue;
            }
        }

        return failed.Count == statusIds.Count ? LociApiEc.DataInvalid : failed.Count > 0 ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc ApplyStatusByName(Guid statusId, string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        if (LociData.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        actorSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyStatusesByName(List<Guid> statusIds, string charaName, string buddyName, out List<Guid> failed)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
        {
            failed = statusIds;
            return LociApiEc.TargetNotFound;
        }
        else if (!actorSM.OwnerValid)
        {
            failed = statusIds;
            return LociApiEc.TargetInvalid;
        }

        failed = [];
        var lookup = LociData.Statuses.ToDictionary(s => s.GUID);
        foreach (var statusId in statusIds)
        {
            if (!lookup.TryGetValue(statusId, out var status))
            {
                failed.Add(statusId);
                continue;
            }
            if (actorSM.AddOrUpdate(status.PreApply(), ManagerChangeType.ApplyRemove) is null)
            {
                failed.Add(statusId);
                continue;
            }
        }
        return failed.Count == statusIds.Count ? LociApiEc.DataInvalid : failed.Count > 0 ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc RemoveStatus(Guid statusId, uint key)
    {
        if (LociManager.ClientSM.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        var res = LociManager.ClientSM.Cancel(status, ManagerChangeType.ApplyRemove, key);
        return res ? LociApiEc.Success : LociApiEc.InvalidKey;
    }

    public LociApiEc RemoveStatuses(List<Guid> statusIds, uint key, out List<Guid> failed)
    {
        failed = [];
        var lookup = LociManager.ClientSM.Statuses.ToDictionary(s => s.GUID);
        foreach (var id in statusIds)
        {
            // Fail if not present, persistent, or an invalid cancel occured.
            if (!lookup.TryGetValue(id, out var status) || status.Persistent || !LociManager.ClientSM.Cancel(id, ManagerChangeType.ApplyRemove, key))
                failed.Add(id);
        }
        // Ret the failed count.
        return failed.Count == statusIds.Count ? LociApiEc.InvalidKey : failed.Count > 0 ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc RemoveStatusByPtr(Guid statusId, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(ptr, out var actorSM))
            return LociApiEc.TargetNotFound;
        
        if (actorSM.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        if (status.Persistent)
            return LociApiEc.ItemIsPersistent;

        return actorSM.Cancel(status, ManagerChangeType.ApplyRemove) ? LociApiEc.Success : LociApiEc.ItemLocked;
    }

    public LociApiEc RemoveStatusesByPtr(List<Guid> statusIds, nint ptr, out List<Guid> failed)
    {
        failed = statusIds;
        if (!CharaWatcher.Rendered.Contains(ptr))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(ptr, out var actorSM))
            return LociApiEc.TargetNotFound;

        failed = [];
        var lookup = actorSM.Statuses.ToDictionary(s => s.GUID);
        foreach (var id in statusIds)
        {
            // Fail if not present, persistent, or an invalid cancel occured.
            if (!lookup.TryGetValue(id, out var status) || status.Persistent || !actorSM.Cancel(id, ManagerChangeType.ApplyRemove))
                failed.Add(id);
         }

         // Based on fail count.
         return failed.Count == statusIds.Count
            ? LociApiEc.ItemLocked : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc RemoveStatusByName(Guid statusId, string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        if (actorSM.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;

        if (status.Persistent)
            return LociApiEc.ItemIsPersistent;

        return actorSM.Cancel(status, ManagerChangeType.ApplyRemove) ? LociApiEc.Success : LociApiEc.ItemLocked;
    }

    public LociApiEc RemoveStatusesByName(List<Guid> statusIds, string charaName, string buddyName, out List<Guid> failed)
    {
        failed = statusIds;
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        failed = [];
        var lookup = actorSM.Statuses.ToDictionary(s => s.GUID);
        foreach (var id in statusIds)
        {
            // Fail if not present, persistent, or an invalid cancel occured.
            if (!lookup.TryGetValue(id, out var status) || status.Persistent || !actorSM.Cancel(id, ManagerChangeType.ApplyRemove))
                failed.Add(id);
         }
         // Based on fail count.
         return failed.Count == statusIds.Count
            ? LociApiEc.ItemLocked : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public bool CanLock(Guid statusId)
        => LociManager.ClientSM.LockedStatuses.ContainsKey(statusId);

    public LociApiEc LockStatus(Guid statusId, uint key)
    {
        if (LociManager.ClientSM.Statuses.FirstOrDefault(s => s.GUID == statusId) is not { } status)
            return LociApiEc.DataNotFound;
        // Return if we locked it
        return LociManager.ClientSM.LockStatus(statusId, key) ? LociApiEc.Success : LociApiEc.ItemLocked;
    }

    public LociApiEc LockStatuses(List<Guid> statusIds, uint key, out List<Guid> failed)
    {
        failed = [];
        var lookup = LociManager.ClientSM.Statuses.Select(s => s.GUID).ToHashSet();
        foreach (var id in statusIds)
        {
            // Fail if not present, or an invalid lock occured.
            if (!lookup.Contains(id) || !LociManager.ClientSM.LockStatus(id, key))
                failed.Add(id);
         }
         // Based on fail count.
         return failed.Count == statusIds.Count 
            ? LociApiEc.ItemLocked : failed.Count > 0 
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc UnlockStatus(Guid statusId, uint key)
    {
        if (!LociManager.ClientSM.LockedStatuses.ContainsKey(statusId))
            return LociApiEc.NoChange;

        // The key exists, so return the result.
        return LociManager.ClientSM.UnlockStatusIfNeeded(statusId, key) ? LociApiEc.Success : LociApiEc.InvalidKey;
    }

    public LociApiEc UnlockStatuses(List<Guid> statuses, uint key, out List<Guid> failed)
    {
        LociManager.ClientSM.UnlockStatuses(statuses, key, out failed);
        // Based on fail count.
        return failed.Count == statuses.Count
            ? LociApiEc.InvalidKey : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public int UnlockAll(uint key)
        => LociManager.ClientSM.ClearLocks(key);

    private void OnStatusUpdated(Guid id, bool wasDeleted)
        => StatusUpdated?.Invoke(id, wasDeleted);

    private void OnChainTriggerHit(ChainTriggerHitMessage msg)
        => ChainTriggerHit?.Invoke(msg.Address, msg.StatusId, msg.Trigger, msg.ChainType, msg.ChainedId);

    public event StatusUpdatedDelegate? StatusUpdated;

    public event ChainTriggerHitDelegate? ChainTriggerHit;
}