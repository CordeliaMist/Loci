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

// Used for Tuple-Based IPC calls and associated data transfers.
global using LociStatusInfo = (
    int Version,
    System.Guid GUID,
    int IconID,
    string Title,
    string Description,
    string CustomVFXPath,           // What VFX to show on application.
    long ExpireTicks,               // Permanent if -1, referred to as 'NoExpire' in LociStatus
    Loci.StatusType Type,           // Loci StatusType enum.
    int Stacks,                     // Usually 1 when no stacks are used.
    int StackSteps,                 // How many stacks to add per reapplication.
    int StackToChain,               // Used for chaining on set stacks
    uint Modifiers,                 // What can be customized, casted to uint from Modifiers (Dalamud IPC Rules)
    System.Guid ChainedGUID,        // What status is chained to this one.
    Loci.ChainType ChainType,       // What type of chaining is this for.
    Loci.ChainTrigger ChainTrigger, // What triggers the chained status.
    string Applier,                 // Who applied the status.
    string Dispeller                // When set, only this person can dispel your loci.
);

global using LociPresetInfo = (
    System.Guid GUID,
    System.Collections.Generic.List<System.Guid> Statuses,
    byte ApplicationType,
    string Title,
    string Description
);
