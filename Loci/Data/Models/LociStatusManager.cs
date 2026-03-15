using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Loci.Processors;
using LociApi.Enums;
using MemoryPack;

namespace Loci.Data;

[Serializable]
public class ActorSM
{
    // Which kind of actor to grab
    public ObjectKind ActorKind = ObjectKind.None;
    // The LociKey for this actor
    public string Identifier = string.Empty;

    // Any GUID's in here are ensured to display SHE's for added statuses on next tick.
    public HashSet<Guid> AddTextShown = [];
    // Any GUID's in here are ensured to display REM's for removed statuses on next tick.
    public HashSet<Guid> RemTextShown = [];

    // The current active LociStatuses in this LociSM.
    public List<LociStatus> Statuses = [];

    // The Current Hosts managing this Manager. If any are present, it's Ephemeral.
    [NonSerialized] internal HashSet<string> EphemeralHosts = [];

    // Tells us which statuses are locked, and what keys are locking them.
    [NonSerialized] internal Dictionary<Guid, uint> LockedStatuses = [];

    // The actor currently in control of this SM.
    [NonSerialized] internal unsafe Character* Owner = null!;

    // Stores what changes occured to this manager since it's last fired event.
    [NonSerialized] internal ManagerChangeType DirtyChanges = ManagerChangeType.NoChange;

    internal unsafe nint OwnerAddress => (nint)Owner;
    internal unsafe bool OwnerValid => Owner != null;
    internal bool Ephemeral => EphemeralHosts.Count is not 0;
    internal bool NeedInvokeChangeEvent => DirtyChanges > 0;

    public bool LockedByKey(Guid id, uint key)
        => LockedStatuses.TryGetValue(id, out var k) && k == key;

    public bool AnyLockedByKey(IEnumerable<Guid> ids, uint key)
        => ids.Any(id => LockedStatuses.TryGetValue(id, out var k) && k == key);

    /// <summary>
    ///     Attempt to lock a LociStatus by ID. <para />
    ///     If this fails, the lock either already exists, or is locked by another key.
    /// </summary>
    public bool LockStatus(Guid id, uint key)
        => LockedStatuses.TryAdd(id, key);

    public (bool, List<Guid>) LockStatuses(List<Guid> ids, uint key)
    {
        var failed = new List<Guid>();
        foreach (var id in ids)
            if (!LockedStatuses.TryAdd(id, key))
                failed.Add(id);
        // If failed is less than the ids size, some worked.
        return (failed.Count < ids.Count, failed);
    }

    /// <summary>
    ///   Attempts to unlock an ID.
    /// </summary>
    /// <returns> True if already unlocked, or if unlock worked. </returns>
    public bool UnlockStatusIfNeeded(Guid id, uint key)
    {
        var isLocked = LockedStatuses.TryGetValue(id, out var k);
        return !isLocked || k == key && LockedStatuses.Remove(id);
    }

    public bool UnlockStatuses(List<Guid> ids, uint key, out List<Guid> failed)
    {
        failed = [];
        foreach (var id in ids)
        {
            if (!LockedStatuses.TryGetValue(id, out var k))
                continue;
            // Fail if the keys dont match
            if (k != key || !LockedStatuses.Remove(id))
                failed.Add(id);
        }
        // If failed is less than the ids size, some worked.
        return failed.Count < ids.Count;
    }

    /// <summary>
    ///     Unlocks all statuses matching the key. <para />
    ///     Statuses locked by other keys are not affected.
    /// </summary>
    public int ClearLocks(uint key)
    {
        int removed = 0;
        foreach (var (id, activeLock) in LockedStatuses.ToArray())
        {
            if (activeLock != key)
                continue;
            // Remove this lock.
            LockedStatuses.Remove(id);
            removed++;
        }
        return removed;
    }

    /// <summary>
    ///     Updates the status after a DataString update source requests it. <para />
    ///     This method avoids checking validity and does not trigger events. (Made for Speed)
    /// </summary>
    public void AddOrUpdateDataStringStatus(LociStatus status)
    {
        // Maybe add in a check here for locks in the case people can override it?
        if (status is null || status.IsNull())
        {
            Svc.Logger.Error($"Status not added because it is null");
            return;
        }

        // Add or update the status stored in the LociSM.
        for (var i = 0; i < Statuses.Count; i++)
        {
            // We are updating one, so we will ultimately return early.
            if (Statuses[i].GUID == status.GUID)
            {
                // Ensure we remove the update from addTextShown for the DataString.
                if (status.Modifiers.Has(Modifiers.StacksIncrease) && Statuses[i].Stacks != status.Stacks)
                    AddTextShown.Remove(status.GUID);
                // If we want to persist time on reapplication, do so.
                if (Statuses[i].Modifiers.Has(Modifiers.PersistExpireTime))
                    status.ExpiresAt = Statuses[i].ExpiresAt;
                // Update the status.
                Statuses[i] = status;
                return;
            }
        }
        // it was new, so add it
        Statuses.Add(status);
    }

    /// <summary>
    ///     Adds the LociStatus, or updates the existing. <para />
    ///     Ignored if locked and a matching key is not present. <para />
    ///     If not locked, and a matching key is present, locks the status.
    /// </summary>
    public LociStatus? AddOrUpdate(LociStatus status, ManagerChangeType changeType, bool check = true, uint key = 0)
    {
        if (status is null || status.IsNull())
        {
            Svc.Logger.Warning($"Status was not added because it is null");
            return null;
        }
        // Do not add statuses with invalid data
        if (check)
        {
            if (!status.IsValid(out var error))
            {
                Svc.Logger.Warning($"Status {status.Title} was not added because it is invalid: {error}");
                return null;
            }
        }

        // Add or update the status stored in the LociSM.
        for (var i = 0; i < Statuses.Count; i++)
        {
            // We are updating one, so we will ultimately return early.
            if (Statuses[i].GUID == status.GUID)
            {
                // If this status is locked, prevent updates and just return the current.
                if (LockedStatuses.TryGetValue(status.GUID, out var curKey) && (key is 0 || curKey != key))
                    return null;

                // If we are increasing stacks, handle the stack update logic.
                if (status.Modifiers.Has(Modifiers.StacksIncrease))
                    if (LociProcessor.IconStackCounts.TryGetValue((uint)status.IconID, out var max))
                        UpdateStackLogic(status, Statuses[i], (int)max);

                // If we want to persist time on reapplication, do so.
                if (Statuses[i].Modifiers.Has(Modifiers.PersistExpireTime))
                    status.ExpiresAt = Statuses[i].ExpiresAt;

                // Update the status.
                Statuses[i] = status;

                // fire trigger if needed and then early return.
                DirtyChanges |= changeType;               
                return Statuses[i];
            }
        }

        // if it was new, fire event if needed and add it.
        DirtyChanges |= changeType;
        // Was new, so add it in!
        Statuses.Add(status);
        // If a lock was set, lock the status with the provided key.
        if (key is not 0)
            LockedStatuses.TryAdd(status.GUID, key);
        return status;
    }

    /// <summary>
    ///     Update stacks on the incoming status. Also handles any chain triggering
    ///     from max stacks, and any roll-over logic if set.
    /// </summary>
    private void UpdateStackLogic(LociStatus ns, LociStatus cur, int max)
    {
        var curStacks = cur.Stacks;
        var hasChainId = cur.ChainedGUID != Guid.Empty;
        // Current + Increase < max, so Just add.
        if (curStacks + ns.StackSteps < max)
        {
            // Update stacks, ensure text will be shown.
            ns.Stacks = curStacks + ns.StackSteps;
            // Condition to fire the trigger for chaining on SetStack.
            if (hasChainId && cur.ChainTrigger is ChainTrigger.HitSetStacks && curStacks < cur.StackToChain && ns.Stacks >= cur.StackToChain)
                ns.ApplyChain = true;
            // Ensure add text is shown for the update.
            AddTextShown.Remove(ns.GUID);
        }
        // Current stacks are not max, but adding it will go over.
        else if (curStacks != max && curStacks + ns.StackSteps >= max)
        {
            if (hasChainId && cur.ChainTrigger is (ChainTrigger.HitMaxStacks or ChainTrigger.HitSetStacks))
            {
                if (cur.ChainTrigger is ChainTrigger.HitMaxStacks)
                {
                    ns.ApplyChain = true;
                    ns.Stacks = curStacks;
                }
                else if (cur.ChainTrigger is ChainTrigger.HitSetStacks && curStacks < cur.StackToChain && curStacks + ns.StackSteps >= cur.StackToChain)
                {
                    ns.ApplyChain = true;
                    ns.Stacks = curStacks;
                }
            }
            else
            {
                ns.Stacks = cur.Modifiers.Has(Modifiers.StacksRollOver)
                    ? Math.Clamp((curStacks + ns.StackSteps) - max, 1, max) : max;
                AddTextShown.Remove(ns.GUID);
            }
        }
        // We are already at max stacks, so do nothing.
        else
        {
            ns.Stacks = max;
        }
    }

    /// <summary>
    ///     Only controlled by the CommonProcessor and can bypass lock checks.
    /// </summary>
    public void Remove(LociStatus status, ManagerChangeType changeType)
    {
        if (!Statuses.Remove(status))
            return;
        AddTextShown.Remove(status.GUID);
        RemTextShown.Remove(status.GUID);
        // Prepare a invokable action on the next tick.
        DirtyChanges |= changeType;
    }

    // Effectively 'remove'
    public bool Cancel(Guid id, ManagerChangeType changeType, uint key = 0)
    {
        // Prevent removal if this ID is locked.
        // (This assumes this is not called for natural falloff)
        if (!UnlockStatusIfNeeded(id, key))
            return false;
        // Cancel that status.
        if (Statuses.FirstOrDefault(s => s.GUID == id) is { } status)
        {
            status.ExpiresAt = 0;
            changeType |= changeType;
            return true;
        }
        return false;
    }

    // Need to do a firstordefault since the appied status could have a different state.
    public bool Cancel(LociStatus myStatus, ManagerChangeType changeType, uint key = 0)
        => Cancel(myStatus.GUID, changeType, key);

    /// <summary>
    ///     Applies a LociPreset to this SM. <para />
    ///     Application behavior is determined by the Preset's ApplicationType.
    /// </summary>
    /// <remarks> Determine a good way to identify partial success indication later. </remarks>
    public void ApplyPreset(LociPreset preset, ManagerChangeType changeType, uint key = 0)
    {
        var ignore = new HashSet<Guid>(Statuses.Where(s => s.Persistent).Select(s => s.GUID));

        if (preset.ApplyType is PresetApplyType.IgnoreExisting)
            foreach (var s in Statuses)
                ignore.Add(s.GUID);

        // Locked statuses wont be canceled by this
        if (preset.ApplyType is PresetApplyType.ReplaceAll)
            foreach (var s in Statuses)
                if (!ignore.Contains(s.GUID) && !preset.Statuses.Contains(s.GUID))
                    Cancel(s, changeType);

        foreach (var id in preset.Statuses)
            if (LociData.Statuses.FirstOrDefault(x => x.GUID == id) is { } s && !ignore.Contains(s.GUID))
                AddOrUpdate(s.PreApply(), changeType, key: key);
    }

    /// <summary>
    ///   Removes a preset from the status manager, and can be provided an unlock key.
    /// </summary>
    /// <remarks> Determine a good way to identify partial success indication later. </remarks>
    public void RemovePreset(LociPreset p, ManagerChangeType changeType, uint key = 0)
    {
        foreach (var x in p.Statuses)
            Cancel(x, changeType, key: key);
    }

    public byte[] BinarySerialize()
        => MemoryPackSerializer.Serialize(Statuses, Utils.SerializerOptions);

    public string ToBase64()
        => Statuses.Count is not 0 ? Convert.ToBase64String(BinarySerialize()) : string.Empty;

    public List<LociStatusInfo> GetStatusInfoList()
        => Statuses.Count is not 0 ? Statuses.Select(x => x.ToTuple()).ToList() : [];

    /// <summary>
    ///     Apply a MemoryPackSerialized byte array of data to the StatusManager.
    /// </summary>
    public void Apply(byte[] data)
    {
        try
        {
            // Attempt to deserialize into the current format. If it fails, warn of old formatting.
            var statuses = MemoryPackSerializer.Deserialize<List<LociStatus>>(data, Utils.SerializerOptions);
            if (statuses is not null)
                UpdateStatusesFromDataString(statuses);
            else
                throw new Bagagwa("Deserialized statuses were null");
        }
        catch (Bagagwa)
        {
            Svc.Logger.Warning("A datastring was passed in with an old MyStatus format. Ignoring.");
        }
    }

    /// <summary>
    ///     Apply the base64 serialized string of data to this SM. <br />
    ///     <b>Commonly called by IPC Events</b>
    /// </summary>
    public void Apply(string base64string)
    {
        if (string.IsNullOrEmpty(base64string))
            UpdateStatusesFromDataString(Array.Empty<LociStatus>());
        else
            Apply(Convert.FromBase64String(base64string));
    }

    public void UpdateStatusesFromDataString(IEnumerable<LociStatus> newStatusList)
    {
        try
        {
            var updated = 0;
            foreach (var x in Statuses)
            {
                if (!newStatusList.Any(n => n.GUID == x.GUID))
                {
                    x.ExpiresAt = 0;
                    updated++;
                }
            }
            // Update the rest of the statuses.
            foreach (var x in newStatusList)
            {
                if (x.ExpiresAt > Utils.Time)
                {
                    AddOrUpdateDataStringStatus(x);
                    updated++;
                }
            }
            // If any updated, mark as dirty
            if (updated > 0)
                DirtyChanges |= ManagerChangeType.DataString;
        }
        catch (Bagagwa e)
        {
            Svc.Logger.Warning($"Error applying statuses as ephemeral: {e.Message}");
        }
    }

    public static string HexDump(ReadOnlySpan<byte> data, int bytesPerLine = 16)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += bytesPerLine)
        {
            sb.Append($"{i:X6}: ");

            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < data.Length)
                    sb.Append($"{data[i + j]:X2} ");
                else
                    sb.Append("   ");
            }

            sb.Append(" | ");

            for (int j = 0; j < bytesPerLine && i + j < data.Length; j++)
            {
                var b = data[i + j];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }

            sb.AppendLine();
        }
        return sb.ToString();
    }


    public bool ContainsStatus(LociStatus status)
        => ContainsStatus(status.GUID);

    public bool ContainsStatus(Guid status)
        => Statuses.Any(s => s.GUID == status);

    public bool ContainsPreset(LociPreset preset)
    {
        var statusCount = preset.Statuses.Count;
        for (var i = 0; i < statusCount; i++)
        {
            var statusGUID = preset.Statuses[i];
            if (!ContainsStatus(statusGUID))
                return false;
        }
        // Does contain the preset, along with all its statuses.
        return true;
    }
}
