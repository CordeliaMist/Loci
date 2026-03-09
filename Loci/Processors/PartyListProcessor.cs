using CkCommons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;

namespace Loci.Processors;
public unsafe class PartyListProcessor : IDisposable
{
    private readonly ILogger<PartyListProcessor> _logger;
    private readonly MainConfig _config;

    private int[] NumStatuses = [0, 0, 0, 0, 0, 0, 0, 0];
    public PartyListProcessor(ILogger<PartyListProcessor> logger, MainConfig config)
    {
        _logger = logger;
        _config = config;

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

        for (var i = 0; i < NumStatuses.Length; i++)
            NumStatuses[i] = 0;

        var index = 23;
        var storeIndex = 0;
        var visibleParty = Utils.GetVisibleParty();
        // _logger.LogTrace($"PartyList found {visibleParty.Count} members!", LoggerType.LociProcessors);
        // _logger.LogTrace($"Partylist had {visibleParty.Count(m => m != nint.Zero)} valid members", LoggerType.LociProcessors);
        foreach (nint player in Utils.GetVisibleParty())
        {
            if (player != nint.Zero)
            {
                var iconArray = AddonHelp.GetNodeIconArray(addonBase->UldManager.NodeList[index]);
                foreach (var x in iconArray)
                    if (x->IsVisible())
                        NumStatuses[storeIndex]++;
            }
            // inc regardless
            storeIndex++;
            index--;
        }
        _logger.LogTrace($"PartyList Requested update: {string.Join(", ", NumStatuses)}", LoggerType.Processors);
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
        var partyMemberNodeIndex = 23;
        var party = Utils.GetNodeOrderedVisibleParty();
        // _logger.LogDebug($"PartyMembers:\n - {string.Join("\n - ", party.Select(x => $"{x:X} - {((Character*)x)->GetNameWithWorld()}"))}");

        for (var n = 0; n < party.Count; n++)
        {
            var player = party[n];
            if (player == nint.Zero)
            {
                partyMemberNodeIndex--;
                continue;
            }

            // Get the icon node array
            var iconArray = AddonHelp.GetNodeIconArray(addon->UldManager.NodeList[partyMemberNodeIndex]);
            // _logger.LogInformation($"Icon array length for {player} is {iconArray.Length}");
            for (var i = NumStatuses[n]; i < iconArray.Length; i++)
            {
                var c = iconArray[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }

            // If we should hide all, simply dec the idx and continue.
            if (hideAll)
            {
                partyMemberNodeIndex--;
                continue;
            }

            // Otherwise, update the statuses
            var curIndex = NumStatuses[n];
            var sm = LociManager.GetFromChara((Character*)player);
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
            // dec the node index for the next member
            partyMemberNodeIndex--;
        }
    }

    private void SetIcon(AtkUnitBase* addon, AtkResNode* container, LociStatus status, ActorSM manager)
        => LociProcessor.SetIcon(addon, container, status, manager);
}
