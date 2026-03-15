using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi;
using LociApi.Api;
using LociApi.Enums;

namespace Loci.Api;
public class PresetApi : DisposableMediatorSubscriberBase, ILociApiPresets
{
    private readonly ApiHelpers _helpers;
    private readonly LociManager _manager;
    private readonly LociData _data;

    public PresetApi(ILogger<PresetApi> logger, LociMediator mediator,
        ApiHelpers helpers, LociManager manager, LociData data)
        : base(logger, mediator)
    {
        _helpers = helpers;
        _manager = manager;
        _data = data;
        Mediator.Subscribe<LociPresetChanged>(this, _ => OnPresetUpdated(_.Item.GUID, _.Type is FSChangeType.Deleted));
    }

    public (LociApiEc, LociPresetInfo) GetPresetInfo(Guid guid)
    {
        if (LociData.Presets.FirstOrDefault(s => s.GUID == guid) is not { } preset)
            return (LociApiEc.DataNotFound, default);

        return (LociApiEc.Success, preset.ToTuple());
    }

    public List<LociPresetInfo> GetPresetInfoList()
        => LociData.Presets.Select(s => s.ToTuple()).ToList();

    public (LociApiEc, LociPresetSummary) GetPresetSummary(Guid guid)
    {
        if (LociData.Presets.FirstOrDefault(s => s.GUID == guid) is not { } preset)
            return (LociApiEc.DataNotFound, default);
        // Get the summary for the preset and return it
        return (LociApiEc.Success, _helpers.ToSavedPresetSummary(preset));
    }

    public List<LociPresetSummary> GetPresetSummaryList()
        => _helpers.ToSavedPresetsSummary();

    public LociApiEc ApplyPreset(Guid presetId, uint key)
    {
        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        // Modify the preset manager
        LociManager.ClientSM.ApplyPreset(preset, ManagerChangeType.ApplyRemove, key);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyPresets(List<Guid> ids, uint key, out List<Guid> failed)
    {
        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var id in ids)
        {
            // Fail if not a valid preset
            if (!lookup.TryGetValue(id, out var preset))
            {
                failed.Add(id);
                continue;
            }
            // Update the manager
            LociManager.ClientSM.ApplyPreset(preset, ManagerChangeType.ApplyRemove, key);
        }

        return failed.Count == ids.Count 
            ? LociApiEc.DataInvalid : failed.Count > 0 
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc ApplyPresetInfo(LociPresetInfo presetInfo, uint key)
    {
        // If this null, we can imply it failed by either being invalid or a lock.
        LociManager.ClientSM.ApplyPreset(presetInfo.ToSavedPreset(), ManagerChangeType.ApplyRemove, key);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyPresetInfos(List<LociPresetInfo> presetInfos, uint key)
    {
        // No way to tell if this fails or not in any way right now, so callback is not trustworthy.
        foreach (var presetInfo in presetInfos)
            LociManager.ClientSM.ApplyPreset(presetInfo.ToSavedPreset(), ManagerChangeType.ApplyRemove, key);

        return LociApiEc.Success;
    }

    public LociApiEc ApplyPresetByPtr(Guid presetId, nint address)
    {
        if (!CharaWatcher.Rendered.Contains(address))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
            return LociApiEc.TargetNotFound;

        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        actorSM.ApplyPreset(preset, ManagerChangeType.ApplyRemove);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyPresetsByPtr(List<Guid> presetIds, nint address, out List<Guid> failed)
    {
        if (!CharaWatcher.Rendered.Contains(address))
        {
            failed = presetIds;
            return LociApiEc.TargetInvalid;
        }

        if (!LociManager.Rendered.TryGetValue(address, out var actorSM))
        {
            failed = presetIds;
            return LociApiEc.TargetNotFound;
        }

        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var presetId in presetIds)
        {
            if (!lookup.TryGetValue(presetId, out var preset))
            {
                failed.Add(presetId);
                continue;
            }

            ApplyPreset(presetId, 0);
        }

        return failed.Count == presetIds.Count
            ? LociApiEc.DataInvalid : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc ApplyPresetByName(Guid presetId, string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        actorSM.ApplyPreset(preset, 0);
        return LociApiEc.Success;
    }

    public LociApiEc ApplyPresetsByName(List<Guid> presetIds, string charaName, string buddyName, out List<Guid> failed)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
        {
            failed = presetIds;
            return LociApiEc.TargetNotFound;
        }
        else if (!actorSM.OwnerValid)
        {
            failed = presetIds;
            return LociApiEc.TargetInvalid;
        }

        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var presetId in presetIds)
        {
            if (!lookup.TryGetValue(presetId, out var preset))
            {
                failed.Add(presetId);
                continue;
            }

            ApplyPreset(presetId, 0);
        }

        return failed.Count == presetIds.Count
            ? LociApiEc.DataInvalid : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    public LociApiEc RemovePreset(Guid presetId, uint key)
    {
        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        // No way to know if this was a success or failure, so don't assume correct callback yet.
        LociManager.ClientSM.RemovePreset(preset, ManagerChangeType.ApplyRemove, key);
        return LociApiEc.Success;
    }

    // Callback from this is inreliable for accuracy at the moment.
    public LociApiEc RemovePresets(List<Guid> presetIds, uint key, out List<Guid> failed)
    {
        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var id in presetIds)
        {
            if (!lookup.TryGetValue(id, out var preset))
                failed.Add(id);
            else
                RemovePreset(id, key);
        }

        // Ret the failed count.
        return failed.Count == presetIds.Count
            ? LociApiEc.InvalidKey : failed.Count > 0
                ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    // Only functions on presets owned by the client.
    // Also callback is not reliable to full, partial, or no success on completion.
    public LociApiEc RemovePresetByPtr(Guid presetId, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(ptr, out var actorSM))
            return LociApiEc.TargetNotFound;

        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        // Cannot assume success or failure here, even partial.
        actorSM.RemovePreset(preset, 0);
        return LociApiEc.Success;
    }

    // Only functions on presets owned by the client.
    // Also callback is not reliable to full, partial, or no success on completion.
    public LociApiEc RemovePresetsByPtr(List<Guid> presetIds, nint ptr, out List<Guid> failed)
    {
        failed = presetIds;
        if (!CharaWatcher.Rendered.Contains(ptr))
            return LociApiEc.TargetInvalid;

        if (!LociManager.Rendered.TryGetValue(ptr, out var actorSM))
            return LociApiEc.TargetNotFound;

        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var id in presetIds)
        {
            if (!lookup.TryGetValue(id, out var preset))
                failed.Add(id);
            else
                RemovePreset(id, 0);
        }

        // Based on fail count.
        return failed.Count == presetIds.Count
           ? LociApiEc.ItemLocked : failed.Count > 0
               ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    // Only functions on presets owned by the client.
    // Also callback is not reliable to full, partial, or no success on completion.
    public LociApiEc RemovePresetByName(Guid presetId, string charaName, string buddyName)
    {
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        if (LociData.Presets.FirstOrDefault(s => s.GUID == presetId) is not { } preset)
            return LociApiEc.DataNotFound;

        // Cannot assume success or failure here, even partial.
        actorSM.RemovePreset(preset, 0);
        return LociApiEc.Success;
    }

    // Only functions on presets owned by the client.
    // Also callback is not reliable to full, partial, or no success on completion.
    public LociApiEc RemovePresetsByName(List<Guid> presetIds, string charaName, string buddyName, out List<Guid> failed)
    {
        failed = presetIds;
        var name = _helpers.ToLociName(charaName, buddyName);
        if (!LociManager.Managers.TryGetValue(name, out var actorSM))
            return LociApiEc.TargetNotFound;
        else if (!actorSM.OwnerValid)
            return LociApiEc.TargetInvalid;

        failed = [];
        var lookup = LociData.Presets.ToDictionary(s => s.GUID);
        foreach (var id in presetIds)
        {
            if (!lookup.TryGetValue(id, out var preset))
                failed.Add(id);
            else
                RemovePreset(id, 0);
        }

        // Based on fail count.
        return failed.Count == presetIds.Count
           ? LociApiEc.ItemLocked : failed.Count > 0
               ? LociApiEc.PartialSuccess : LociApiEc.Success;
    }

    private void OnPresetUpdated(Guid id, bool wasDeleted)
        => PresetUpdated?.Invoke(id, wasDeleted);

    public event PresetUpdatedDelegate? PresetUpdated;
}