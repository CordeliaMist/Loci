using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;

namespace Loci.Processors;
public unsafe class TargetInfoBuffDebuffProcessor
{
    private readonly ILogger<TargetInfoBuffDebuffProcessor> _logger;
    private readonly MainConfig _config;
    private readonly LociManager _manager;

    public int NumStatuses = 0;
    public TargetInfoBuffDebuffProcessor(ILogger<TargetInfoBuffDebuffProcessor> logger, MainConfig config, LociManager manager)
    {
        _logger = logger;
        _config = config;
        _manager = manager;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "_TargetInfoBuffDebuff", OnPreRequestedUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnPostRequestedUpdate);
        if(PlayerData.Available && AddonHelp.TryGetAddonByName<AtkUnitBase>("_TargetInfoBuffDebuff", out var addon) && AddonHelp.IsAddonReady(addon))
            PostRequestedUpdate(addon);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "_TargetInfoBuffDebuff", OnPreRequestedUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnPostRequestedUpdate);
    }

    public void HideAll()
    {
        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_TargetInfoBuffDebuff", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdateAddon(addon, true);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private unsafe void OnPreRequestedUpdate(AddonEvent t, AddonArgs args)
        => PreAddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private unsafe void OnPostRequestedUpdate(AddonEvent t, AddonArgs args)
        => PostRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private unsafe void PreAddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        // Get the target so we can handle the case of companions. For these guys, we want to set all statuses back to invisible.
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not ObjectKind.Companion)
            return;
        // Clear visibility of all subnodes.
        if (addonBase is not null && AddonHelp.IsAddonReady(addonBase))
        {
            for (var i = 3u; i <= 32; i++)
            {
                var c = addonBase->UldManager.SearchNodeById(i);
                if (c->IsVisible())
                    c->NodeFlags ^= NodeFlags.Visible;
            }
            _logger.LogTrace($"Hid all status icons for companion target: {Utils.ToLociName((Character*)target)}", LoggerType.Processors);
        }
    }

    private void PostRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (addonBase is null || !AddonHelp.IsAddonReady(addonBase))
            return;

        NumStatuses = 0;
        for (var i = 3u; i <= 32; i++)
        {
            var c = addonBase->UldManager.SearchNodeById(i);
            if (c->IsVisible())
                NumStatuses++;
        }
    }

    private void OnTargetInfoBuffDebuffUpdate(AddonEvent type, AddonArgs args)
    {
        if(!PlayerData.Available)
            return;
        if(!_config.CanLociModifyUI())
            return;
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    // Didn't really know how to transfer to get the DalamudStatusList from here, so had to use IPlayerCharacter.
    public unsafe void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        var baseCnt = 3 + NumStatuses;
        for (var i = baseCnt; i <= 32; i++)
        {
            var c = addon->UldManager.SearchNodeById((uint)i);
            if (c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        if (hideAll)
            return;

        // Update the statuses
        var sm = _manager.GetOrCreateSM((Character*)target);
        // If a companion, force visibility
        if (target->ObjectKind is ObjectKind.Companion)
        {
            var c = addon->UldManager.SearchNodeById(2);
            if (!c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        foreach (var x in sm.Statuses)
        {
            if (baseCnt > 32)
                break;

            if (x.ExpiresAt - Utils.Time > 0)
            {
                SetIcon(addon, baseCnt, x, sm);
                baseCnt++;
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, int id, LociStatus status, ActorSM manager)
    {
        var container = addon->UldManager.SearchNodeById((uint)id);
        LociProcessor.SetIcon(addon, container, status, manager);
    }
}
