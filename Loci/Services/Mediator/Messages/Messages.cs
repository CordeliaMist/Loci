using Loci.Data;
using LociApi.Enums;

namespace Loci.Services.Mediator;

public enum FSChangeType { Created, Deleted, Renamed, Modified }
public enum LociModule { Statuses, Presets, Events }

/// <summary>
///     Tells us when the client has changed territories or zones. Occurs after the player is valid and loaded. <para />
///     Can make this SameThreadMessage if we want..
/// </summary>
public record TerritoryChanged(ushort PrevTerritory, ushort NewTerritory) : MessageBase;

/// <summary>
///     Whenever a watched object is created.
/// </summary>
public record WatchedObjectCreated(IntPtr Address) : SameThreadMessage;

/// <summary>
///     Whenever a watched object is destroyed or unloaded.
/// </summary>
public record WatchedObjectDestroyed(IntPtr Address) : SameThreadMessage;


// DDS
public record FolderUpdateManagers : MessageBase;

// CKFS
public record LociStatusChanged(FSChangeType Type, LociStatus Item, string? OldString = null) : MessageBase;
public record LociPresetChanged(FSChangeType Type, LociPreset Item, string? OldString = null) : MessageBase;
public record LociEventChanged(FSChangeType Type, LociEvent Item, string? OldString = null) : MessageBase;
public record ReloadCKFS(LociModule Module) : MessageBase;

// Enable State
public record NewEnabledStateMessage(bool NewState) : SameThreadMessage;

// StatusManager
public record ActorSMChanged(IntPtr Address) : SameThreadMessage;
public record ActorSMStatusesChanged(IntPtr Address, Guid StatusId, StatusChangeType Change) : SameThreadMessage;
public record ApplyToTargetMessage(IntPtr TargetAddress, string TargetHost, List<LociStatusInfo> Data) : SameThreadMessage;

// LociApiStatuses
public record ChainTriggerHitMessage(IntPtr Address, Guid StatusId, ChainTrigger Trigger, ChainType ChainType, Guid ChainedId) : SameThreadMessage;

// LociApiEvents
public record EventPathMoved(Guid EventId, string OldPath, string NewPath) : SameThreadMessage;