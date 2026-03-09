using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Loci.Services.Mediator;

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


// Based on state
public record EnabledStateChanged(bool NewState) : MessageBase;
public record StatusManagerChanged(IntPtr Address, ObjectKind Kind) : SameThreadMessage; // Could user over actions if we wanted.