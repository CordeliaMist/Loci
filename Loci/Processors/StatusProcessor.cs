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

    public int NumStatuses = 0;

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
            UpdateStatus(addon, LociManager.ClientSM, NumStatuses, true);
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

        UpdateStatus((AtkUnitBase*)args.Addon.Address, LociManager.ClientSM, NumStatuses);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (addonBase is null || !AddonHelp.IsAddonReady(addonBase) || !_config.CanLociModifyUI())
            return;
        
        NumStatuses = 0;
        for (var i = 25; i >= 1; i--)
        {
            var c = addonBase->UldManager.NodeList[i];
            if (c->IsVisible())
                NumStatuses++;
        }
    }

    public void UpdateStatus(AtkUnitBase* addon, ActorSM manager, int statusCnt, bool hideAll = false)
    {
        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        int baseCnt = 25 - statusCnt;

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
