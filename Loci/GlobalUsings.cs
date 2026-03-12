global using Dalamud.Utility;
global using Newtonsoft.Json;
global using Newtonsoft.Json.Linq;
global using Microsoft.Extensions.Logging;
global using System.Text;
global using System.Numerics;
global using CFlags = Dalamud.Bindings.ImGui.ImGuiComboFlags;
global using WFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
global using FAI = Dalamud.Interface.FontAwesomeIcon;

// MERV! DON'T SUMMON BAGAGWA!
global using Bagagwa = System.Exception;

global using LociStatusInfo = (
    int Version,
    System.Guid GUID,
    uint IconID,
    string Title,
    string Description,
    string CustomVFXPath,                   // What VFX to show on application.
    long ExpireTicks,                       // Permanent if -1, referred to as 'NoExpire' in LociStatus
    LociApi.Enums.StatusType Type,          // Loci StatusType enum.
    int Stacks,                             // Usually 1 when no stacks are used.
    int StackSteps,                         // How many stacks to add per reapplication.
    int StackToChain,                       // Used for chaining on set stacks
    uint Modifiers,                         // What can be customized, casted to uint from Modifiers (Dalamud IPC Rules)
    System.Guid ChainedGUID,                // What status is chained to this one.
    LociApi.Enums.ChainType ChainType,      // What type of chaining is this for.
    LociApi.Enums.ChainTrigger ChainTrigger,// What triggers the chained status.
    string Applier,                         // Who applied the status.
    string Dispeller                        // When set, only this person can dispel your loci.
);

global using LociPresetInfo = (
    int Version,
    System.Guid GUID,
    System.Collections.Generic.List<System.Guid> Statuses,
    byte ApplicationType,
    string Title,
    string Description
);

// Placeholder for now, additional details in the future, as this is WIP
global using LociEventInfo = (
    int Version,
    System.Guid GUID,
    bool Enabled,
    int Priority,
    string Title,
    string Description,
    LociApi.Enums.LociEventType EventType,
    LociApi.Enums.ChainType ReactionType,
    System.Guid ChainedGUID,
    uint IndicatedID,
    short GearsetIdx,
    LociApi.Enums.KnownDirection Direction,
    byte IntendedUse,
    string WhitelistedName
);

global using LociStatusSummary = (
    System.Guid ID,
    string FSPath,
    uint IconID,
    string Title,
    string Description
);

global using LociPresetSummary = (
    System.Guid ID,
    string FSPath,
    System.Collections.Generic.List<uint> IconIDs,
    string Title,
    string Description
);

global using LociEventSummary = (
    System.Guid ID,
    string FSPath,
    bool Enabled,
    LociApi.Enums.LociEventType EventType,
    string Title,
    string Description
);
