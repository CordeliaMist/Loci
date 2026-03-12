using CkCommons;
using LociApi.Enums;
using MemoryPack;

namespace Loci.Data;

// A very WIP structure for events.
[Serializable]
[MemoryPackable]
public partial class LociEvent
{
    internal string ID => GUID.ToString();

    // Essential
    public const int Version = 1;
    public Guid GUID = Guid.NewGuid();
    public bool Enabled = false;
    public int Priority = 0;
    public string Title = string.Empty;
    public string Description = string.Empty;

    public LociEventType EventType = LociEventType.Emote;

    // How to respond when the spesified condition is met.
    public ChainType ReactionType = ChainType.Status;
    public Guid ReactionGUID = Guid.Empty;
    // Who to apply the reactionGUID to.
    public string ReactionTarget = string.Empty;

    // The primary identifier across all EventTypes
    // Related: BuffDebuffID, EmoteID, TerritoryId, OnlineStatus
    public uint IndicatedID = 0;

    // Secondary Identifiers, special values.
    public JobFlags JobFlags = JobFlags.None;
    public short GearsetIdx = -1;
    public KnownDirection Direction = KnownDirection.Self;      // Emotes
    public IntendedUseEnum IntendedUse = IntendedUseEnum.Town;  // ZoneBased

    // Whitelisted target name, Supports "PlayerName@World" and "Player Names Pet Name"
    public string WhitelistedName = string.Empty;

    // Time based is WIP.

    public bool ShouldSerializeGUID() => GUID != Guid.Empty;
    public bool IsNull() => Description is null || Title is null;

    public bool IsZoneBased() => IntendedUse is IntendedUseEnum.UNK;

    public LociEventInfo ToTuple()
        => new LociEventInfo
        {
            Version = Version,
            GUID = GUID,
            Enabled = Enabled,
            Priority = Priority,
            Title = Title,
            Description = Description,
            EventType = EventType,
            ReactionType = ReactionType,
            ReactionGUID = ReactionGUID,
            ReactionTarget = ReactionTarget,
            IndicatedID = IndicatedID,
            JobFlags = JobFlags,
            GearsetIdx = GearsetIdx,
            Direction = Direction,
            IntendedUse = (byte)IntendedUse,
            WhitelistedName = WhitelistedName
        };

    public static LociEvent FromTuple(LociEventInfo info)
    {
        return new LociEvent
        {
            GUID = info.GUID,
            Enabled = info.Enabled,
            Title = info.Title,
            Description = info.Description,
            EventType = info.EventType,
            ReactionType = info.ReactionType,
            ReactionGUID = info.ReactionGUID,
            ReactionTarget = info.ReactionTarget,
            IndicatedID = info.IndicatedID,
            JobFlags = info.JobFlags,
            GearsetIdx = info.GearsetIdx,
            Direction = info.Direction,
            IntendedUse = (IntendedUseEnum)info.IntendedUse,
            WhitelistedName = info.WhitelistedName
        };
    }

    public string ReportString()
        => $"[LociStatus: GUID={GUID}," +
        $"\nEnabled={Enabled}" +
        $"\nTitle={Title}" +
        $"\nDescription={Description}" +
        $"\nEventType={EventType}" +
        $"\nReactionType={ReactionType}" +
        $"\nReactionGUID={ReactionGUID}" +
        $"\nReactionTarget={ReactionTarget}" +
        $"\nIndicatedID={IndicatedID}" +
        $"\nJobFlags={JobFlags}" +
        $"\nGearsetIdx={GearsetIdx}" +
        $"\nDirection={Direction}" +
        $"\nIntendedUse={IntendedUse}" +
        $"\nWhitelistedName={WhitelistedName}]";
}
