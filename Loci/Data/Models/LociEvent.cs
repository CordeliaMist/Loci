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
    public Guid ChainedGUID = Guid.Empty;

    // The primary identifier across all EventTypes
    // Related: JobID, BuffDebuffID, EmoteID, TerritoryId, OnlineStatus
    public uint IndicatedID = 0;

    // Secondary Identifiers, special values.
    public short GearsetIdx = -1;
    public KnownDirection Direction = KnownDirection.Self;      // Emotes
    public IntendedUseEnum IntendedUse = IntendedUseEnum.Town;  // ZoneBased

    // Whitelisted target name, Supports "PlayerName@World" and "Player Names Pet Name"
    public string WhitelistedName = string.Empty;

    // Time based is WIP.

    public bool ShouldSerializeGUID() => GUID != Guid.Empty;
    public bool IsNull() => Description is null || Title is null;

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
            ChainedGUID = ChainedGUID,
            IndicatedID = IndicatedID,
            GearsetIdx = GearsetIdx,
            Direction = Direction,
            IntendedUse = (byte)IntendedUse,
            WhitelistedName = WhitelistedName
        };

    public static LociEvent FromTuple(LociEventInfo eventInfo)
    {
        return new LociEvent
        {
            GUID = eventInfo.GUID,
            Enabled = eventInfo.Enabled,
            Title = eventInfo.Title,
            Description = eventInfo.Description,
            EventType = eventInfo.EventType,
            ReactionType = eventInfo.ReactionType,
            ChainedGUID = eventInfo.ChainedGUID,
            IndicatedID = eventInfo.IndicatedID,
            GearsetIdx = eventInfo.GearsetIdx,
            Direction = eventInfo.Direction,
            IntendedUse = (IntendedUseEnum)eventInfo.IntendedUse,
            WhitelistedName = eventInfo.WhitelistedName
        };
    }

    public string ReportString()
        => $"[LociStatus: GUID={GUID}," +
        $"\nEnabled={Enabled}" +
        $"\nTitle={Title}" +
        $"\nDescription={Description}" +
        $"\nEventType={EventType}" +
        $"\nReactionType={ReactionType}" +
        $"\nChainedGUID={ChainedGUID}" +
        $"\nIndicatedID={IndicatedID}" +
        $"\nGearsetIdx={GearsetIdx}" +
        $"\nDirection={Direction}" +
        $"\nIntendedUse={IntendedUse}" +
        $"\nWhitelistedName={WhitelistedName}]";
}
