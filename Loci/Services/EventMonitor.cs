using CkCommons;
using Dalamud.Game.ClientState;
using Dalamud.Plugin.Services;
using Loci.Data;
using Loci.Services.Mediator;
using LociApi.Enums;

namespace Loci.Services;

public class EventMonitor : DisposableMediatorSubscriberBase
{
    private readonly LociEventData _data;
    public EventMonitor(ILogger<EventMonitor> logger, LociMediator mediator, LociEventData data)
        : base(logger, mediator)
    {
        _data = data;

        // Listen to zone changes.
        Svc.ClientState.Login += SetInitialData;
        Svc.ClientState.Logout += OnLogout;
        Svc.ClientState.ZoneInit += OnZoneInit;
        Svc.Framework.Update += OnTick;

        if (Svc.ClientState.IsLoggedIn)
            SetInitialData();

        CheckZoneEvents();
        CheckStatusEvents();
    }

    // For previous locations to reference in comparisons.
    internal static ushort          _latestTerritory = 0;
    internal static IntendedUseEnum _latestIntendedUse = IntendedUseEnum.UNK;
    private byte                    _latestOnlineStatus = 0;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Svc.ClientState.Login -= SetInitialData;
        Svc.ClientState.Logout -= OnLogout;
        Svc.ClientState.ZoneInit -= OnZoneInit;
        Svc.Framework.Update -= OnTick;
    }

    private async void OnLogout(int type, int code)
    {
        _latestTerritory = 0;
        _latestIntendedUse = IntendedUseEnum.UNK;
        _latestOnlineStatus = 0;
    }

    private async void OnZoneInit(ZoneInitEventArgs args)
    {
        // Ignore territories from login zone / title screen (if any even exist)
        if (!Svc.ClientState.IsLoggedIn)
            return;

        // Upon a zone init, we are always not loaded in. As such, await for player to continue processing.
        Logger.LogTrace($"Zone initialized: {args.ToString()}", LoggerType.Processors);
        Logger.LogDebug($"Territory changed to: {args.TerritoryType.RowId} ({PlayerContent.GetTerritoryName((ushort)args.TerritoryType.RowId)})", LoggerType.Processors);
        var prevTerritory = _latestTerritory;
        var prevIntendedUse = _latestIntendedUse;
        
        // Await for the player to be loaded.
        await Utils.WaitForPlayerLoading();
        _latestTerritory = PlayerContent.TerritoryIdInstanced;
        _latestIntendedUse = PlayerContent.TerritoryIntendedUse;

        // If no change, return, otherwise, check for events.
        if (prevTerritory == _latestTerritory && prevIntendedUse == _latestIntendedUse)
            return;
        CheckZoneEvents();
    }

    private unsafe void OnTick(IFramework framework)
    {
        if (!PlayerData.Available)
            return;

        if (PlayerData.Character->OnlineStatus != _latestOnlineStatus)
        {
            _latestOnlineStatus = PlayerData.Character->OnlineStatus;
            CheckStatusEvents();
        }
    }

    private async void SetInitialData()
    {
        await Utils.WaitForPlayerLoading();
        _latestTerritory = PlayerContent.TerritoryIdInstanced;
        _latestIntendedUse = PlayerContent.TerritoryIntendedUse;
    }

    private void CheckZoneEvents()
    {
        var zoneEvents = LociEventData.Events
            .Where(e =>
            {
                if (!e.Enabled || e.EventType is not LociEventType.ZoneBased)
                    return false;
                // Ret based on type
                if (e.IntendedUse is IntendedUseEnum.UNK)
                    return e.IndicatedID == _latestTerritory;
                else
                    return e.IntendedUse == _latestIntendedUse;
            })
            .OrderByDescending(e => e.Priority)
            .ToList();

        // Try and apply the first matching one.
        Logger.LogDebug($"Checking zone-based events for territory: {PlayerContent.GetTerritoryName(_latestTerritory)}), Content: ({_latestIntendedUse}) for {zoneEvents.Count} events.", LoggerType.Memory);
        zoneEvents.ApplyFirstMatch();
    }

    private void CheckStatusEvents()
    {
        var onlineStatusEvents = LociEventData.Events
            .Where(e => e.Enabled && e.EventType is LociEventType.OnlineStatus && e.IndicatedID == _latestOnlineStatus)
            .OrderByDescending(e => e.Priority)
            .ToList();

        Logger.LogDebug($"Online status changed to: {_latestOnlineStatus}. Checking {onlineStatusEvents.Count} events.", LoggerType.Memory);
        onlineStatusEvents.ApplyFirstMatch();
    }

}