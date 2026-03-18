using Loci.Data;
using LociApi.Enums;

namespace Loci.Services.Mediator;

public enum FSChangeType { Created, Deleted, Renamed, Modified }
public enum LociModule { Statuses, Presets, Events }

// Essential to the CharaWatcher
public record DelayedFrameworkUpdateMessage : SameThreadMessage;
public record WatchedObjectCreated(IntPtr Address) : SameThreadMessage;
public record WatchedObjectDestroyed(IntPtr Address) : SameThreadMessage;


/// <summary> If the processors for Loci should run or not. </summary>
/// <remarks> This is a SameThreadMessage as it is linked to API calls and should be accurate. </remarks>
public record EnabledStateChangeMessage(bool NewState) : SameThreadMessage;

public record CompatibilityModeChanged(bool NewState) : MessageBase;

/// <summary> Tells us when the client has changed territories or zones. </summary>
/// <remarks> This always occurs after the ClientPlayer is determined valid. </remarks>
public record TerritoryChangedMessage(ushort PrevTerritory, ushort NewTerritory) : MessageBase;

/// <summary> Informs us when the client changed gearsets and/or jobs. </summary>
public record GearsetChangedMessage(int PrevGearsetIdx, byte PrevJobId, int NewGearsetIdx, byte NewJobId) : MessageBase;

/// <summary> Occurs when an emote is performed. </summary>
/// <remarks> Ensure on samethread so that the address remains valid. </remarks>
public record EmotePerformedMessage(ushort EmoteId, IntPtr Caller, IntPtr Target) : SameThreadMessage;

// DDS
public record FolderUpdateManagers : MessageBase;

// CKFS
public record LociStatusChanged(FSChangeType Type, LociStatus Item, string? OldString = null) : MessageBase;
public record LociPresetChanged(FSChangeType Type, LociPreset Item, string? OldString = null) : MessageBase;
public record LociEventChanged(FSChangeType Type, LociEvent Item, string? OldString = null) : MessageBase;
public record ReloadCKFS(LociModule Module) : MessageBase;

// StatusManager
public record ActorSMChanged(IntPtr Address, ManagerChangeType ChangeType) : SameThreadMessage;
public record ApplyToTargetMessage(IntPtr TargetAddress, string TargetHost, List<LociStatusInfo> Data) : SameThreadMessage;
public record ChainTriggerHitMessage(IntPtr Address, Guid StatusId, ChainTrigger Trigger, ChainType ChainType, Guid ChainedId) : SameThreadMessage;