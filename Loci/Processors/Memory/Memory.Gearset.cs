using CkCommons;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Loci.Data;
using LociApi.Enums;

namespace Loci.Processors;

// Gearset handling
public unsafe partial class LociMemory
{
    public delegate nint GearsetChangedDelegate(RaptureGearsetModule* module, uint gearsetId, byte glamourPlateId);
    internal static Hook<GearsetChangedDelegate> ProcessGearsetChangeHook = null!;

    private nint GearsetChangedDetour(RaptureGearsetModule* module, uint gearsetId, byte glamourPlateId)
    {
        // Store previous, then perform the original to process the change.
        var prevGearsetIdx = module->CurrentGearsetIndex;
        var ret = ProcessGearsetChangeHook.Original(module, gearsetId, glamourPlateId);
        // Then get the set gearsetIdx
        var newGearsetEntry = module->GetGearset((int)gearsetId);
        var newJobId = newGearsetEntry->ClassJob;

        _logger.LogDebug($"Gearset changed from {prevGearsetIdx} to {gearsetId} (ClassJob: {(JobType)newJobId})", LoggerType.Memory);
        var gearsetEvents = LociEventData.Events
            .Where(e =>
            {
                if (!e.Enabled || e.EventType is not LociEventType.JobChange)
                    return false;
                if (prevGearsetIdx == gearsetId)
                    return false;
                // Ensure correct logic
                return (e.GearsetIdx == -1) 
                    ? e.JobFlags is JobFlags.None || e.JobFlags.Has((JobFlags)(1UL << newJobId)) 
                    : e.GearsetIdx == (short)gearsetId;
            })
            .OrderByDescending(e => e.Priority)
            .ToList();

        _logger.LogTrace($"Found {gearsetEvents.Count} entries to iterate", LoggerType.Memory);
        gearsetEvents.ApplyFirstMatch();

        return ret;
    }
}

