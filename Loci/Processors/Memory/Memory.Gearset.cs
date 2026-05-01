using CkCommons;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Loci.Data;
using Loci.Services.Mediator;
using LociApi.Enums;

namespace Loci.Processors;

// Gearset handling
public unsafe partial class LociMemory
{
    public delegate int GearsetChangedDelegate(RaptureGearsetModule* module, int gearsetId, byte glamourPlateId);
    internal static Hook<GearsetChangedDelegate> ProcessGearsetChangeHook = null!;

    private int GearsetChangedDetour(RaptureGearsetModule* module, int gearsetId, byte glamourPlateId)
    {
        // Store previous, then perform the original to process the change.
        var prevGearsetIdx = module->CurrentGearsetIndex;
        var prevJob = module->GetGearset(prevGearsetIdx)->ClassJob;
        var ret = ProcessGearsetChangeHook.Original(module, gearsetId, glamourPlateId);
        // Then get the set gearsetIdx
        var newGearsetEntry = module->GetGearset(gearsetId);
        var newJobId = newGearsetEntry->ClassJob;

        _mediator.Publish(new GearsetChangedMessage(prevGearsetIdx, prevJob, gearsetId, newJobId));
        return ret;
    }
}

