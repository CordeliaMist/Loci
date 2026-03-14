using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;
using LociApi.Enums;

namespace Loci.Processors;
public unsafe class FocusTargetInfoProcessor
{
    private readonly ILogger<FocusTargetInfoProcessor> _logger;
    private readonly MainConfig _config;
    private readonly LociMemory _memory;
    private readonly LociManager _manager;

    private int NumStatuses = 0;

    public FocusTargetInfoProcessor(ILogger<FocusTargetInfoProcessor> logger, MainConfig config,
        LociMemory memory, LociManager manager)
    {
        _logger = logger;
        _config = config;
        _memory = memory;
        _manager = manager;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_FocusTargetInfo", OnFocusTargetInfoUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "_FocusTargetInfo", OnPreRequestedUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_FocusTargetInfo", OnPostRequestedUpdate);
        if (PlayerData.Available && AddonHelp.TryGetAddonByName<AtkUnitBase>("_FocusTargetInfo", out var addon) && AddonHelp.IsAddonReady(addon))
            PostRequestedUpdate(addon);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_FocusTargetInfo", OnFocusTargetInfoUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "_FocusTargetInfo", OnPreRequestedUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_FocusTargetInfo", OnPostRequestedUpdate);
    }

    public void HideAll()
    {
        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_FocusTargetInfo", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdateAddon(addon, true);
    }

    private unsafe void OnPreRequestedUpdate(AddonEvent t, AddonArgs args)
        => PreAddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private unsafe void OnPostRequestedUpdate(AddonEvent t, AddonArgs args)
        => PostRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private unsafe void PreAddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        // Get the target so we can handle the case of companions. For these guys, we want to set all statuses back to invisible.
        var target = TargetSystem.Instance()->FocusTarget;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not ObjectKind.Companion)
            return;
        // Clear visibility of all subnodes.
        if (addonBase is not null && AddonHelp.IsAddonReady(addonBase))
        {
            for (var i = 8; i >= 4; i--)
            {
                var c = addonBase->UldManager.NodeList[i];
                if (c->IsVisible())
                    c->NodeFlags ^= NodeFlags.Visible;
            }
            _logger.LogTrace($"Hid all status icons for companion target: {Utils.ToLociName((Character*)target)}", LoggerType.Processors);
        }
    }

    private void PostRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (addonBase is not null && AddonHelp.IsAddonReady(addonBase))
        {
            NumStatuses = 0;
            for (var i = 8; i >= 4; i--)
            {
                var c = addonBase->UldManager.NodeList[i];
                if (c->IsVisible())
                    NumStatuses++;
            }
        }
        _logger.LogTrace($"FocusTarget Requested update: {NumStatuses}", LoggerType.Processors);
    }

    private void OnFocusTargetInfoUpdate(AddonEvent type, AddonArgs args)
    {
        if (!PlayerData.Available) return;
        if (_config.CanLociModifyUI())
            UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    public unsafe void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        var target = TargetSystem.Instance()->FocusTarget;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;
        
        // Determine the base count by combining the Moodles statuses with the statuses from the base game.
        var baseCnt = 8 - NumStatuses;
        for (var i = baseCnt; i >= 4; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if(c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        if (hideAll)
            return;

        // Update the displays.
        var sm = _manager.GetOrCreateSM((Character*)target);
        // If a companion, force visibility
        if (target->ObjectKind is ObjectKind.Companion)
        {
            var c = addon->UldManager.NodeList[3];
            if (!c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        foreach (var x in sm.Statuses)
        {
            if (x.Type is StatusType.Special)
                continue;

            if (baseCnt < 4)
                break;
                    
            var rem = x.ExpiresAt - Utils.Time;
            if (rem > 0)
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
