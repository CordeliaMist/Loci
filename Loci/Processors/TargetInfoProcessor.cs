using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Statuses;
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

    public int NumVanillaStatuses = 0;
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

    public void HideAll()
    {
        if(AddonHelp.TryGetAddonByName<AtkUnitBase>("_TargetInfo", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdateAddon(addon, true);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnPreRequestedUpdate(AddonEvent t, AddonArgs args)
        => PreAddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnPostRequestedUpdate(AddonEvent t, AddonArgs args)
        => PostRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void PreAddonRequestedUpdate(AtkUnitBase* addonBase)
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
        if (addonBase is null || !AddonHelp.IsAddonReady(addonBase))
            return;

        if (_config.Current.MoodlesSupport)
        {
            NumVanillaStatuses = 0;

            var ts = TargetSystem.Instance();
            var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
            if (target is null || !target->IsCharacter() || target->ObjectKind is not ObjectKind.Pc)
                return;

            var chara = (Character*)target;
            if (StatusList.CreateStatusListReference((nint)chara->GetStatusManager()) is { } statusList)
                NumVanillaStatuses = statusList.Count(s => s.GameData.Value.Icon != 0 || s.GameData.Value.Flags != 47);
        }
        else
        {
            NumVanillaStatuses = 0;
            for (var i = 32; i >= 3; i--)
            {
                // Ensure we count the number of vanilla statuses.
                var c = addonBase->UldManager.NodeList[i];
                if (c->IsVisible())
                    NumVanillaStatuses++;
            }
        }
        _logger.LogTrace($"TargetInfo Requested update: {NumVanillaStatuses}", LoggerType.Processors);
    }

    private void OnTargetInfoUpdate(AddonEvent type, AddonArgs args)
    {
        if (!PlayerData.Available)
            return;
        if (!_config.CanLociModifyUI())
            return;
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    public unsafe void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        var ts = TargetSystem.Instance();
        var target = ts->SoftTarget is not null ? ts->SoftTarget : ts->Target;
        if (target is null || !target->IsCharacter() || target->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        if (_config.Current.MoodlesSupport)
            UpdateAddonWithMoodles(addon, (Character*)target, hideAll);
        else
            UpdateAddonNormal(addon, (Character*)target, hideAll);
    }

    private unsafe void UpdateAddonNormal(AtkUnitBase* addon, Character* target, bool hideAll)
    {
        // Get the base count by combining the statuses from Moodles with the vanilla ones.
        var baseCnt = 32 - NumVanillaStatuses;
        for (var i = baseCnt; i >= 3; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if (c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        if (hideAll)
            return;

        var sm = _manager.GetOrCreateSM(target);
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

    private unsafe void UpdateAddonWithMoodles(AtkUnitBase* addon, Character* target, bool hideAll)
    {
        var sm = _manager.GetOrCreateSM(target);
        var baseCnt = 32 - NumVanillaStatuses;
        // If moodles is available, subtract also the moodle status count.
        if (MoodlesWatcher.APIAvailable)
            baseCnt -= MoodlesWatcher.Offsets.TryGetValue((nint)target, out var dat) ? dat.TotalCnt : 0;
        // Calc the endCount.
        var endCnt = Math.Max(baseCnt - sm.Statuses.Count - LociProcessor.RemovedThisTick + 1, 3);

        for(var i = baseCnt; i >= endCnt; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if(c->IsVisible())
                c->NodeFlags ^= NodeFlags.Visible;
        }

        if (hideAll)
            return;

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
