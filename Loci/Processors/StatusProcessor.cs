using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;

namespace Loci.Processors;
public unsafe class StatusProcessor : IDisposable
{
    private readonly ILogger<StatusProcessor> _logger;
    private readonly MainConfig _config;

    private int _numStatuses;
    private int _firstStatusIdx;


    public StatusProcessor(ILogger<StatusProcessor> logger, MainConfig config)
    {
        _logger = logger;
        _config = config;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_Status", OnStatusUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_Status", OnAlcStatusRequestedUpdate);
        if(PlayerData.Available && AddonHelp.TryGetAddonByName<AtkUnitBase>("_Status", out var addon) && AddonHelp.IsAddonReady(addon))
            AddonRequestedUpdate(addon);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_Status", OnStatusUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_Status", OnAlcStatusRequestedUpdate);
    }

    public void HideAll()
    {
        if(!PlayerData.Available)
            return;

        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_Status", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdateStatus(addon, LociManager.ClientSM, _numStatuses, true);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnAlcStatusRequestedUpdate(AddonEvent t, AddonArgs args)
        => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    
    private void OnStatusUpdate(AddonEvent type, AddonArgs args)
    {
        if(!PlayerData.Available)
            return;
        if(!_config.CanLociModifyUI())
            return;

        UpdateStatus((AtkUnitBase*)args.Addon.Address, LociManager.ClientSM, _numStatuses);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (addonBase is null || !AddonHelp.IsAddonReady(addonBase) || !_config.CanLociModifyUI())
            return;
        
        // reset our offset values
        _numStatuses = 0;
        _firstStatusIdx = 0;

        // Nodelist counts backwards, and has a length of 31.
        // Nodes are read from 30 to 1, displayed left to right.
        // The length includes the root node ref, so the actual node size is 30.
        var nodeList = addonBase->UldManager.NodeList;
        var dispCapacity = addonBase->UldManager.NodeListCount - 1;

        // The Game typically only displayed 25 down to 1, leaving the first 5 slots empty.
        // However, if assigned enough statuses by the base game, such as in an alliance raid,
        // these extra 5 slots are occupied, and the node list shifts accordingly to account for this.

        // As such this logic should handle the full range of values to account for such shifts and offsets.
        // This will likely need further logic in UpdateStatus down the line, but now this should be sufficient to handle all current known cases.
        for (var i = dispCapacity; i >= 1; i--)
        {
            if (!nodeList[i]->IsVisible()) continue;
            _numStatuses++;
            if (_firstStatusIdx == 0) _firstStatusIdx = i;
        }
    }

    public void UpdateStatus(AtkUnitBase* addon, ActorSM manager, int statusCnt, bool hideAll = false)
    {
        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;
        
        // TODO: Where we start and place status here needs to be fixed for single bar mode
        // in Left-Justified, we start counting from 30 regardless of type.
        // in Standard sort, buffs are inset 5 from the end on the left, and debuffs 5 from the end on the right.
        //   buffs grow left, debuffs grow right.
        int baseCnt = _firstStatusIdx - statusCnt;

        // Update visibility
        for (var i = baseCnt; i >= 1; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if (c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        // if we are to hide all, keep hidden.
        if (hideAll)
            return;

        // Otherwise, update icons
        foreach(var x in manager.Statuses)
        {
            if(baseCnt < 1) break;
            var rem = x.ExpiresAt - Utils.Time;
            if(rem > 0)
            {
                SetIcon(addon, baseCnt, x, manager);
                baseCnt--;
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, LociStatus status, ActorSM manager)
    {
        var container = addon->UldManager.NodeList[index];
        LociProcessor.SetIcon(addon, container, status, manager);
    }
}
