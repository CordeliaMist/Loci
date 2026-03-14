using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;

namespace Loci.Processors;

public unsafe class TargetInfoProcessor
{
    private readonly ILogger<TargetInfoProcessor> _logger;
    private readonly MainConfig _config;
    private readonly LociManager _manager;

    public int NumStatuses = 0;
    public TargetInfoProcessor(ILogger<TargetInfoProcessor> logger, MainConfig config, LociManager manager)
    {
        _logger = logger;
        _config = config;
        _manager = manager;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "_TargetInfo", OnTargetInfoUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "_TargetInfo", OnPreRequestedUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfo", OnPostRequestedUpdate);
        if (PlayerData.Available && AddonHelp.TryGetAddonByName<AtkUnitBase>("_TargetInfo", out var addon) && AddonHelp.IsAddonReady(addon))
            PostRequestedUpdate(addon);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, "_TargetInfo", OnTargetInfoUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "_TargetInfo", OnPreRequestedUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfo", OnPostRequestedUpdate);
    }

    public unsafe void HideAll()
    {
        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_TargetInfo", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdateAddon((nint)addon, true);
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
            for (var i = 32; i >= 3; i--)
            {
                var c = addonBase->UldManager.NodeList[i];
                if (c->IsVisible())
                    c->NodeFlags ^= NodeFlags.Visible;
            }
            _logger.LogTrace($"Hid all status icons for companion target: {Utils.ToLociName((Character*)target)}", LoggerType.Processors);
        }
    }

    private unsafe void PostRequestedUpdate(AtkUnitBase* addonBase)
    {

        if (addonBase is not null && AddonHelp.IsAddonReady(addonBase))
        {
            NumStatuses = 0;
            for (var i = 32; i >= 3; i--)
            {
                // Ensure we count the number of vanilla statuses.
                var c = addonBase->UldManager.NodeList[i];
                if (c->IsVisible())
                    NumStatuses++;
            }
        }
        _logger.LogTrace($"TargetInfo Requested update: {NumStatuses}", LoggerType.Processors);
    }

    private void OnTargetInfoUpdate(AddonEvent type, AddonArgs args)
    {
        if (!PlayerData.Available)
            return;
        if (!_config.CanLociModifyUI())
            return;
        UpdateAddon(args.Addon.Address);
    }

    public unsafe void UpdateAddon(nint addonAddr, bool hideAll = false)
    {
        var addon = (AtkUnitBase*)addonAddr;
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        // Get the base count by combining the statuses from Moodles with the vanilla ones.
        var baseCnt = 32 - NumStatuses;
        for(var i = baseCnt; i >= 3; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if(c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        if (hideAll)
            return;

        var sm = _manager.GetOrCreateSM((Character*)target);
        // If a companion, force visibility
        if (target->ObjectKind is ObjectKind.Companion)
        {
            var c = addon->UldManager.NodeList[2];
            if (!c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        foreach (var x in sm.Statuses)
        {
            if (baseCnt < 3)
                break;

            if (x.ExpiresAt - Utils.Time > 0)
            {
                SetIcon(addon, baseCnt, x, sm);
                baseCnt--;
            }
        }
    }

    private unsafe void SetIcon(AtkUnitBase* addon, int index, LociStatus status, ActorSM manager)
    {
        var container = addon->UldManager.NodeList[index];
        LociProcessor.SetIcon(addon, container, status, manager);
    }


}
