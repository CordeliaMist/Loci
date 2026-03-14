using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;
using LociApi.Enums;

namespace Loci.Processors;
public unsafe class StatusCustomProcessor : IDisposable
{
    private readonly ILogger<StatusCustomProcessor> _logger;
    private readonly MainConfig _config;
    private readonly LociManager _manager;

    public int NumStatuses0 = 0;
    public int NumStatuses1 = 0;
    public int NumStatuses2 = 0;

    int lastStatusCount = 0;
    bool statusCountLessened = false;

    public StatusCustomProcessor(ILogger<StatusCustomProcessor> logger, MainConfig config, LociManager manager)
    {
        _logger = logger;
        _config = config;
        _manager = manager;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom0", OnStatusCustom0Update);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom1", OnStatusCustom1Update);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom2", OnStatusCustom2Update);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom0", OnStatusCustom0RequestedUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom1", OnStatusCustom1RequestedUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom2", OnStatusCustom2RequestedUpdate);
        if(PlayerData.Available)
        {
            if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom0", out var addon0) && AddonHelp.IsAddonReady(addon0))
                Custom0RequestedUpdate(addon0);

            if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom1", out var addon1) && AddonHelp.IsAddonReady(addon1))
                Custom1RequestedUpdate(addon1);

            if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom2", out var addon2) && AddonHelp.IsAddonReady(addon2))
                Custom2RequestedUpdate(addon2);
        }
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom0", OnStatusCustom0Update);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom1", OnStatusCustom1Update);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom2", OnStatusCustom2Update);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom0", OnStatusCustom0RequestedUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom1", OnStatusCustom1RequestedUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom2", OnStatusCustom2RequestedUpdate);
    }

    public void HideAll()
    {
        if (!PlayerData.Available)
            return;
        
        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom0", out var addon0) && AddonHelp.IsAddonReady(addon0))
        {
            var sm = _manager.GetOrCreateSM(PlayerData.Character);
            var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Positive);
            UpdateStatusCustom(addon0, sm, validStatuses, LociProcessor.PositiveStatuses, NumStatuses0, true);
        }

        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom1", out var addon1) && AddonHelp.IsAddonReady(addon1))
        {
            var sm = _manager.GetOrCreateSM(PlayerData.Character);
            var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Negative);
            UpdateStatusCustom(addon1, sm, validStatuses, LociProcessor.NegativeStatuses, NumStatuses1, true);
        }

        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_StatusCustom2", out var addon2) && AddonHelp.IsAddonReady(addon2))
        {
            var sm = _manager.GetOrCreateSM(PlayerData.Character);
            var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Special);
            UpdateStatusCustom(addon2, sm, validStatuses, LociProcessor.SpecialStatuses, NumStatuses2, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnStatusCustom0RequestedUpdate(AddonEvent t, AddonArgs args)
        => Custom0RequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusCustom1RequestedUpdate(AddonEvent t, AddonArgs args)
        => Custom1RequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusCustom2RequestedUpdate(AddonEvent t, AddonArgs args)
        => Custom2RequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void Custom0RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses0);
        _logger.LogTrace($"StatusCustom0 Requested update: {NumStatuses0}", LoggerType.Processors);
    }

    private void Custom1RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses1);
        _logger.LogTrace($"StatusCustom1 Requested update: {NumStatuses1}", LoggerType.Processors);
    }

    private void Custom2RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses2);
        _logger.LogTrace($"StatusCustom2 Requested update: {NumStatuses2}", LoggerType.Processors);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addon, ref int StatusCnt)
    {
        if (addon is null || !AddonHelp.IsAddonReady(addon) ||!_config.CanLociModifyUI())
            return;

        StatusCnt = 0;
        for(var i = 24; i >= 5; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if(c->IsVisible())
                StatusCnt++;
        }

        if (lastStatusCount != StatusCnt)
        {
            if (StatusCnt < lastStatusCount)
                statusCountLessened = true;

            lastStatusCount = StatusCnt;
        }
    }

    //permanent
    private void OnStatusCustom2Update(AddonEvent type, AddonArgs args)
    {
        if(!PlayerData.Available)
            return;
        if(!_config.CanLociModifyUI())
            return;
        //PluginLog.Verbose($"Post1 update {args.Addon:X16}");
        var sm = _manager.GetOrCreateSM(PlayerData.Character);
        var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Special);
        UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, sm, validStatuses, LociProcessor.SpecialStatuses, NumStatuses2);
    }

    //debuffs
    private void OnStatusCustom1Update(AddonEvent type, AddonArgs args)
    {
        if (!PlayerData.Available)
            return;
        if (!_config.CanLociModifyUI())
            return;
        //PluginLog.Verbose($"Post1 update {args.Addon:X16}");
        var sm = _manager.GetOrCreateSM(PlayerData.Character);
        var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Negative);
        UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, sm, validStatuses, LociProcessor.NegativeStatuses, NumStatuses1);
    }

    //buffs
    private void OnStatusCustom0Update(AddonEvent type, AddonArgs args)
    {
        if (!PlayerData.Available)
            return;
        if (!_config.CanLociModifyUI())
            return;
        //PluginLog.Verbose($"Post0 update {args.Addon:X16}");
        var sm = _manager.GetOrCreateSM(PlayerData.Character);
        var validStatuses = sm.Statuses.Where(x => x.Type == StatusType.Positive);
        UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, sm, validStatuses, LociProcessor.PositiveStatuses, NumStatuses0);
    }

    // The common logic method with all statuses of a defined type in the player's status manager.
    public void UpdateStatusCustom(AtkUnitBase* addon, ActorSM manager, IEnumerable<LociStatus> statuses, IEnumerable<uint> userStatuses, int statusCnt, bool hideAll = false)
    {
        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        // Update the base count
        int baseCnt = 24 - statusCnt;

        // Update visibility
        for (var i = baseCnt; i >= 5; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if (c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        // If hiding all, ret early
        if (hideAll)
            return;

        // Otherwise, update
        foreach(var x in statuses)
        {
            if(baseCnt < 5)
                break;

            if (x.ExpiresAt - Utils.Time <= 0)
                continue;

            // Update
            if (statusCountLessened)
            {
                statusCountLessened = false;
                SetIcon(addon, baseCnt - LociProcessor.CancelRequests.Count, x, manager);
            }
            else
            {
                SetIcon(addon, baseCnt, x, manager);
            }

            // Dec the base count
            baseCnt--;
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, LociStatus status, ActorSM manager)
    {
        var container = addon->UldManager.NodeList[index];
        LociProcessor.SetIcon(addon, container, status, manager);
    }
}
