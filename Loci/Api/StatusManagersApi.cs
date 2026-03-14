using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi;
using LociApi.Api;
using LociApi.Enums;

namespace Loci.Api;

public class StatusManagerApi : DisposableMediatorSubscriberBase, ILociApiStatusManager
{
    private readonly ApiHelpers _helpers;
    private readonly LociManager _manager;

    // Internal static events to call across classes in static methods where needed.

    public StatusManagerApi(ILogger<StatusManagerApi> logger, LociMediator mediator,
        ApiHelpers helpers, LociManager manager) 
        : base(logger, mediator)
    {
        _helpers = helpers;
        _manager = manager;

        Mediator.Subscribe<ActorSMOwnerChanged>(this, _ => OnManagerOwnerChanged(_.Address));
        Mediator.Subscribe<ActorSMChanged>(this, _ => OnManagerChanged(_.Address));
        Mediator.Subscribe<ActorSMStatusesChanged>(this, _ => OnManagerStatusesChanged(_.Address, _.StatusId, _.Change));
        Mediator.Subscribe<ApplyToTargetMessage>(this, _ => OnApplyToTarget(_.TargetAddress, _.TargetHost, _.Data));
    }

    // Gets the ClientPlayers Manager. This will always be valid.
    // Can return Success, TargetNotFound, TargetInvalid, DataNotFound
    public (LociApiEc, string?) GetManager()
        => LociManager.ClientSM is null ? (LociApiEc.DataNotFound, null) : (LociApiEc.Success, LociManager.ClientSM.ToBase64());

    public (LociApiEc, string?) GetManagerByPtr(nint address)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return (LociApiEc.TargetInvalid, null);

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return (LociApiEc.TargetNotFound, null);

        return (LociApiEc.Success, actorSM.ToBase64());
    }

    public (LociApiEc, string?) GetManagerByName(string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return (LociApiEc.TargetNotFound, null);

        return (LociApiEc.Success, actorSM.ToBase64());
    }

    // Tuple format. Always returns an empty list on failure.
    // (Can change in the future maybe?)
    public List<LociStatusInfo> GetManagerInfo()
        => LociManager.ClientSM is null ? [] : LociManager.ClientSM.GetStatusInfoList();

    public List<LociStatusInfo> GetManagerInfoByPtr(nint ptr)
        => LociManager.Rendered.TryGetValue(ptr, out var actorSM) ? actorSM.GetStatusInfoList() : [];

    public List<LociStatusInfo> GetManagerInfoByName(string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        return LociManager.Managers.TryGetValue(name, out var actorSM) ? actorSM.GetStatusInfoList() : [];
    }

    // Attempts to set an actors status manager. Informs with return code how that went.
    // Returns Success, NoChange, TargetNotFound, TargetInvalid, DataNotFound, DataInvalid.
    // (Fail if the client and locks are present)
    public LociApiEc SetManager(string base64Data)
    {
        if (LociManager.ClientSM is null)
            return LociApiEc.TargetNotFound;

        if (LociManager.ClientSM.LockedStatuses.Count > 0)
            return LociApiEc.ItemLocked;

        LociManager.ClientSM.Apply(base64Data);
        return LociApiEc.Success;
    }

    public LociApiEc SetManagerByPtr(nint address, string base64Data)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return LociApiEc.TargetNotFound;
        
        actorSM.Apply(base64Data);
        return LociApiEc.Success;
    }

    public LociApiEc SetManagerByName(string charaName, string buddyName, string base64Data)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        
        actorSM.Apply(base64Data);
        return LociApiEc.Success;
    }

    // Same rules as above, but for clearing.
    // For clearing, if the client, do not clear locked statuses, but allow method?
    public LociApiEc ClearManager()
    {
        var removed = 0;
        foreach (var s in LociManager.ClientSM.Statuses.ToList())
        {
            if (LociManager.ClientSM.LockedStatuses.ContainsKey(s.GUID))
                continue;

            if (!s.Persistent)
            {
                LociManager.ClientSM.Cancel(s);
                removed++;
            }
        }

        return removed > 0 ? LociApiEc.Success : LociApiEc.NoChange;
    }

    public LociApiEc ClearManagerByPtr(nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(ptr, out var actorSM))
            return LociApiEc.TargetNotFound;

        var removed = 0;
        foreach (var s in actorSM.Statuses.ToList())
        {
            if (!s.Persistent)
            {
                actorSM.Cancel(s);
                removed++;
            }
        }
        return removed > 0 ? LociApiEc.Success : LociApiEc.NoChange;
    }

    public LociApiEc ClearManagerByName(string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        
        var removed = 0;
        foreach (var s in actorSM.Statuses.ToList())
        {
            if (!s.Persistent)
            {
                actorSM.Cancel(s);
                removed++;
            }
        }
        return removed > 0 ? LociApiEc.Success : LociApiEc.NoChange;
    }

    public string ConvertLegacyData(string base64Data)
        => _helpers.ConvertLegacyData(base64Data);

    private void OnManagerOwnerChanged(nint address)
        => ManagerOwnerChanged?.Invoke(address);

    private void OnManagerChanged(nint address)
        => ManagerChanged?.Invoke(address);

    private void OnManagerStatusesChanged(nint address, Guid statusId, StatusChangeType changeType)
        => ManagerStatusesChanged?.Invoke(address, statusId, changeType);

    private void OnApplyToTarget(nint targetAddr, string targetHost, List<LociStatusInfo> data)
        => ApplyToTargetSent?.Invoke(targetAddr, targetHost, data);


    public event Action<nint> ManagerOwnerChanged;

    public event Action<nint> ManagerChanged;

    public event ManagerStatusesChangedDelegate? ManagerStatusesChanged;

    public event ApplyToTargetDelegate? ApplyToTargetSent;
}