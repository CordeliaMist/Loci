using Loci.Data;
using Loci.DrawSystem;
using LociApi.Enums;
using MemoryPack;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Loci.Api;

// Commonly shared helper logic.
// Maybe make it more of a helper later
public class ApiHelpers(StatusesFS statusFS, PresetsFS presetFS, LociEventsFS eventsFS)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public string ToLociName(string charaName, string buddyName)
        => buddyName.Length > 0 ? $"{charaName}s {buddyName}" : charaName;

    public LociApiEc AddEphemeralHost(ActorSM sm, string identifier)
    {
        if (LociManager.ClientSM == sm)
            return LociApiEc.ClientForbidden;

        return sm.EphemeralHosts.Add(identifier) ? LociApiEc.Success : LociApiEc.NoChange;
    }

    public LociApiEc RemoveEphemeralHost(ActorSM sm, string identifier)
    {
        if (LociManager.ClientSM == sm)
            return LociApiEc.ClientForbidden;

        return sm.EphemeralHosts.Remove(identifier) ? LociApiEc.Success : LociApiEc.NoChange;
    }

    public int ClearActorSM(ActorSM sm)
    {
        int removed = 0;
        foreach (var s in sm.Statuses.ToList())
        {
            if (sm.LockedStatuses.ContainsKey(s.GUID))
                continue;

            if (!s.Persistent)
            {
                sm.Cancel(s);
                removed++;
            }
        }
        return removed;
    }

    public LociStatusSummary ToSavedStatusSummary(LociStatus s)
    {
        var foundPath = statusFS.FindLeaf(s, out var path) ? path.FullName() : string.Empty;
        return new LociStatusSummary(s.GUID, foundPath, s.IconID, s.Title, s.Description);
    }

    public List<LociStatusSummary> ToSavedStatusesSummary()
    {
        var ret = new List<LociStatusSummary>(LociData.Statuses.Count);
        foreach (var s in LociData.Statuses)
        {
            var foundPath = statusFS.FindLeaf(s, out var path) ? path.FullName() : string.Empty;
            ret.Add((s.GUID, foundPath, s.IconID, s.Title, s.Description));
        }
        return ret;
    }

    // More efficient to do this call in bulk than single, but still quite fast.
    public LociPresetSummary ToSavedPresetSummary(LociPreset p)
    {
        var lookup = LociData.Statuses.ToDictionary(s => s.GUID, s => s);
        var foundPath = presetFS.FindLeaf(p, out var path) ? path.FullName() : string.Empty;
        var icons = p.Statuses.Select(sid => lookup.TryGetValue(sid, out var s) ? s.IconID : 0).ToList();
        return new LociPresetSummary(p.GUID, foundPath, icons, p.Title, p.Description);
    }

    public List<LociPresetSummary> ToSavedPresetsSummary()
    {
        var lookup = LociData.Statuses.ToDictionary(s => s.GUID, s => s);
        var ret = new List<LociPresetSummary>(LociData.Presets.Count);
        foreach (var p in LociData.Presets)
        {
            var foundPath = presetFS.FindLeaf(p, out var path) ? path.FullName() : string.Empty;
            var icons = p.Statuses.Select(sid => lookup.TryGetValue(sid, out var s) ? s.IconID : 0).ToList();
            ret.Add((p.GUID, foundPath, icons, p.Title, p.Description));
        }
        return ret;
    }

    public LociEventSummary ToSavedEventSummary(LociEvent e)
    {
        var foundPath = eventsFS.FindLeaf(e, out var path) ? path.FullName() : string.Empty;
        return new LociEventSummary(e.GUID, foundPath, e.Enabled, e.EventType, e.Title, e.Description);
    }

    public List<LociEventSummary> ToSavedEventsSummary()
    {
        var ret = new List<LociEventSummary>(LociEventData.Events.Count);
        foreach (var e in LociEventData.Events)
        {
            var foundPath = eventsFS.FindLeaf(e, out var path) ? path.FullName() : string.Empty;
            ret.Add((e.GUID, foundPath, e.Enabled, e.EventType, e.Title, e.Description));
        }
        return ret;
    }

    public string ConvertLegacyData(string base64Data)
    {
        try
        {
            // Get the byte data
            var byteArr = Convert.FromBase64String(base64Data);
            // Deserialize using memoryPack into the legacy format.
            var legacySM = MemoryPackSerializer.Deserialize<List<MyStatus>>(byteArr);
            if (legacySM is null)
                throw new Bagagwa("Deserialized data was null");     
            // Convert to Loci's Format
            var newData = legacySM.Select(ConvertLegacyStatus).ToList();
            // Serialize that data.
            return ToBase64(newData);
        }
        catch (Bagagwa)
        {
            Svc.Logger.Warning("Failed to convert legacy data");
            return string.Empty;
        }
    }

    // Does a Legacy -> Loci Status conversion on a single status.
    public LociStatus ConvertLegacyStatus(MyStatus legacyStatus)
        => new LociStatus
        {
            GUID = legacyStatus.GUID,
            IconID = (uint)legacyStatus.IconID,
            Title = legacyStatus.Title,
            Description = legacyStatus.Description,
            CustomFXPath = legacyStatus.CustomFXPath,
            ExpiresAt = legacyStatus.ExpiresAt,
            Type = (StatusType)(byte)legacyStatus.Type,
            Modifiers = legacyStatus.Modifiers,
            Stacks = legacyStatus.Stacks,
            StackSteps = legacyStatus.StackSteps,
            ChainedGUID = legacyStatus.ChainedStatus,
            ChainedType = ChainType.Status,
            ChainTrigger = legacyStatus.ChainTrigger,
            Applier = legacyStatus.Applier,
            Dispeller = legacyStatus.Dispeller
        };

    // Perform an ActorSM's BinarySerialize method on a defined list of statuses.
    public byte[] BinarySerialize(List<LociStatus> statuses)
    => MemoryPackSerializer.Serialize(statuses, Utils.SerializerOptions);

    // Converts a given list of LociStatuses into the base64 format used by ActorSMs.
    public string ToBase64(List<LociStatus> statuses)
        => statuses.Count is not 0 ? Convert.ToBase64String(BinarySerialize(statuses)) : string.Empty;
}

// Format used by Legacy StatusManager
[Serializable]
[MemoryPackable]
public partial class MyStatus
{
    public Guid GUID;
    public int IconID;
    public string Title;
    public string Description;
    public string CustomFXPath;
    public long ExpiresAt;

    public int Type; // Int
    public Modifiers Modifiers; // UInt

    public int Stacks;
    public int StackSteps;

    public Guid ChainedStatus;
    public ChainTrigger ChainTrigger; // Int

    public string Applier;
    public string Dispeller;
}


