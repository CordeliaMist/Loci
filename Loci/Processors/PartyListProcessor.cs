using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;
using LociApi.Enums;

namespace Loci.Processors;

public unsafe class PartyListProcessor : IDisposable
{
    private readonly ILogger<PartyListProcessor> _logger;
    private readonly MainConfig _config;
    private readonly LociManager _manager;

    private readonly int[] _numStatuses = new int[8];

    private const byte PartyNodeOffset = 17;

    public PartyListProcessor(ILogger<PartyListProcessor> logger, MainConfig config, LociManager manager)
    {
        _logger = logger;
        _config = config;
        _manager = manager;

        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_PartyList", OnPartyListUpdate);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnAlcPartyListRequestedUpdate);
        if (PlayerData.Available && AddonHelp.TryGetAddonByName<AtkUnitBase>("_PartyList", out var addon) && AddonHelp.IsAddonReady(addon))
            AddonRequestedUpdate(addon);
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_PartyList", OnPartyListUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnAlcPartyListRequestedUpdate);
    }

    public void HideAll()
    {
        if (AddonHelp.TryGetAddonByName<AtkUnitBase>("_PartyList", out var addon) && AddonHelp.IsAddonReady(addon))
            UpdatePartyList(addon, true);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnAlcPartyListRequestedUpdate(AddonEvent t, AddonArgs args)
        => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void OnPartyListUpdate(AddonEvent type, AddonArgs args)
        => UpdatePartyList((AtkUnitBase*)args.Addon.Address);

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!PlayerData.Available)
            return;

        if (addonBase is null || !AddonHelp.IsAddonReady(addonBase) || !_config.CanLociModifyUI())
            return;

        for (var i = 0; i < _numStatuses.Length; i++)
            _numStatuses[i] = 0;

        var storeIndex = 0;
        // _logger.LogTrace($"PartyList found {visibleParty.Count} members!", LoggerType.LociProcessors);
        // _logger.LogTrace($"Partylist had {visibleParty.Count(m => m != nint.Zero)} valid members", LoggerType.LociProcessors);
        foreach (var player in Utils.GetVisibleParty())
        {
            if (player != nint.Zero)
            {
                var nodeIndex = _numStatuses.Length - 1 - storeIndex + PartyNodeOffset;
                var iconArray = AddonHelp.GetNodeIconArray(addonBase->UldManager.NodeList[nodeIndex]);
                foreach (var x in iconArray)
                    if (x->IsVisible())
                        _numStatuses[storeIndex]++;
            }

            // inc regardless
            storeIndex++;
        }

        _logger.LogTrace($"PartyList Requested update: {string.Join(", ", _numStatuses)}", LoggerType.Processors);
    }

    public void UpdatePartyList(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!PlayerData.Available)
            return;
        if (!_config.CanLociModifyUI())
            return;

        if (addon is null || !AddonHelp.IsAddonReady(addon))
            return;

        // We can update, so update
        //var partyMemberNodeIndex = 24;
        var party = Utils.GetNodeOrderedVisibleParty();
        // _logger.LogDebug($"PartyMembers:\n - {string.Join("\n - ", party.Select(x => $"{x:X} - {((Character*)x)->GetNameWithWorld()}"))}");

        for (var n = 0; n < party.Count; n++)
        {
            var player = party[n];
            if (player == nint.Zero) continue;

            // Get the icon node array
            var nodeIndex = _numStatuses.Length - 1 - n + PartyNodeOffset;
            var iconArray = AddonHelp.GetNodeIconArray(addon->UldManager.NodeList[nodeIndex]);

            // _logger.LogInformation($"Icon array length for {player} is {iconArray.Length}");
            for (var i = _numStatuses[n]; i < iconArray.Length; i++)
            {
                var c = iconArray[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }

            // If we should hide all, simply dec the idx and continue.
            if (hideAll) continue;

            // Otherwise, update the statuses
            var curIndex = _numStatuses[n];
            var sm = _manager.GetOrCreateSM((Character*)player);
            // _logger.LogTrace($"Found SM for idx {curIndex}, with iconArray length of {iconArray.Length} with {sm.Statuses.Count} statuses.");
            foreach (var status in sm.Statuses)
            {
                if (status.Type == StatusType.Special)
                    continue;
                if (curIndex >= iconArray.Length)
                    break;

                var rem = status.ExpiresAt - Utils.Time;
                if (rem > 0)
                {
                    SetIcon(addon, iconArray[curIndex], status, sm);
                    curIndex++;
                }
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, AtkResNode* container, LociStatus status, ActorSM manager)
        => LociProcessor.SetIcon(addon, container, status, manager);
}
