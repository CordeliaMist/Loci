using Dalamud.Plugin;
using LociApi.Api;
using LociApi.Helpers;
using LociApi.Ipc;

namespace Loci.Api;

public class IpcProviders : IDisposable
{
    private readonly List<IDisposable> _providers;

    private readonly EventProvider _disposedProvider;
    private readonly EventProvider _initializedProvider;

    public IpcProviders(ILociApi api)
    {
        // Init the independant providers
        _initializedProvider = Ready.Provider(Svc.PluginInterface);
        _disposedProvider = Disposed.Provider(Svc.PluginInterface);

        // The the rest of the providers..
        _providers =
        [
            // IpcBase
            ApiVersion.Provider(Svc.PluginInterface, api),
            IsEnabled.Provider(Svc.PluginInterface, api),
            EnabledStateChanged.Provider(Svc.PluginInterface, api),

            // IpcRegistry
            RegisterByPtr.Provider(Svc.PluginInterface, api.Registry),
            RegisterByName.Provider(Svc.PluginInterface, api.Registry),
            UnregisterByPtr.Provider(Svc.PluginInterface, api.Registry),
            UnregisterByName.Provider(Svc.PluginInterface, api.Registry),
            UnregisterAll.Provider(Svc.PluginInterface, api.Registry),
            GetHostsByPtr.Provider(Svc.PluginInterface, api.Registry),
            GetHostsByName.Provider(Svc.PluginInterface, api.Registry),
            GetHostActorCount.Provider(Svc.PluginInterface, api.Registry),
            ActorHostsChanged.Provider(Svc.PluginInterface, api.Registry),

            // IpcStatusManager
            GetManager.Provider(Svc.PluginInterface, api.StatusManager),
            GetManagerByPtr.Provider(Svc.PluginInterface, api.StatusManager),
            GetManagerByName.Provider(Svc.PluginInterface, api.StatusManager),
            GetManagerInfo.Provider(Svc.PluginInterface, api.StatusManager),
            GetManagerInfoByPtr.Provider(Svc.PluginInterface, api.StatusManager),
            GetManagerInfoByName.Provider(Svc.PluginInterface, api.StatusManager),
            SetManager.Provider(Svc.PluginInterface, api.StatusManager),
            SetManagerByPtr.Provider(Svc.PluginInterface, api.StatusManager),
            SetManagerByName.Provider(Svc.PluginInterface, api.StatusManager),
            ClearManager.Provider(Svc.PluginInterface, api.StatusManager),
            ClearManagerByPtr.Provider(Svc.PluginInterface, api.StatusManager),
            ClearManagerByName.Provider(Svc.PluginInterface, api.StatusManager),
            ConvertLegacyData.Provider(Svc.PluginInterface, api.StatusManager),

            ManagerOwnerChanged.Provider(Svc.PluginInterface, api.StatusManager),
            ManagerChanged.Provider(Svc.PluginInterface, api.StatusManager),
            ManagerStatusesChanged.Provider(Svc.PluginInterface, api.StatusManager),
            ApplyToTargetSent.Provider(Svc.PluginInterface, api.StatusManager),

            // IpcStatuses
            GetStatusInfo.Provider(Svc.PluginInterface, api.Statuses),
            GetStatusInfoList.Provider(Svc.PluginInterface, api.Statuses),
            GetStatusSummary.Provider(Svc.PluginInterface, api.Statuses),
            GetStatusSummaryList.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatus.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatuses.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusInfo.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusInfos.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusByPtr.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusesByPtr.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusByName.Provider(Svc.PluginInterface, api.Statuses),
            ApplyStatusesByName.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatus.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatuses.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatusByPtr.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatusesByPtr.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatusByName.Provider(Svc.PluginInterface, api.Statuses),
            RemoveStatusesByName.Provider(Svc.PluginInterface, api.Statuses),
            CanLock.Provider(Svc.PluginInterface, api.Statuses),
            LockStatus.Provider(Svc.PluginInterface, api.Statuses),
            LockStatuses.Provider(Svc.PluginInterface, api.Statuses),
            UnlockStatus.Provider(Svc.PluginInterface, api.Statuses),
            UnlockStatuses.Provider(Svc.PluginInterface, api.Statuses),
            UnlockAll.Provider(Svc.PluginInterface, api.Statuses),

            StatusUpdated.Provider(Svc.PluginInterface, api.Statuses),
            ChainTriggerHit.Provider(Svc.PluginInterface, api.Statuses),

            // IpcPresets
            GetPresetInfo.Provider(Svc.PluginInterface, api.Presets),
            GetPresetInfoList.Provider(Svc.PluginInterface, api.Presets),
            GetPresetSummary.Provider(Svc.PluginInterface, api.Presets),
            GetPresetSummaryList.Provider(Svc.PluginInterface, api.Presets),
            ApplyPreset.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresets.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetInfo.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetInfos.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetByPtr.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetsByPtr.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetByName.Provider(Svc.PluginInterface, api.Presets),
            ApplyPresetsByName.Provider(Svc.PluginInterface, api.Presets),
            RemovePreset.Provider(Svc.PluginInterface, api.Presets),
            RemovePresets.Provider(Svc.PluginInterface, api.Presets),
            RemovePresetByPtr.Provider(Svc.PluginInterface, api.Presets),
            RemovePresetsByPtr.Provider(Svc.PluginInterface, api.Presets),
            RemovePresetByName.Provider(Svc.PluginInterface, api.Presets),
            RemovePresetsByName.Provider(Svc.PluginInterface, api.Presets),

            PresetUpdated.Provider(Svc.PluginInterface, api.Presets),

            // IpcEvents
            GetEventList.Provider(Svc.PluginInterface, api.Events),
            GetEventInfo.Provider(Svc.PluginInterface, api.Events),
            GetEventInfoList.Provider(Svc.PluginInterface, api.Events),
            GetEventSummary.Provider(Svc.PluginInterface, api.Events),
            GetEventSummaryList.Provider(Svc.PluginInterface, api.Events),
            CreateEvent.Provider(Svc.PluginInterface, api.Events),
            DeleteEvent.Provider(Svc.PluginInterface, api.Events),
            SetEventState.Provider(Svc.PluginInterface, api.Events),
            SetEventStates.Provider(Svc.PluginInterface, api.Events),

            EventUpdated.Provider(Svc.PluginInterface, api.Events),
            EventPathMoved.Provider(Svc.PluginInterface, api.Events),
        ];
        // Indicate Loci is now ready
        _initializedProvider.Invoke();
    }

    public void Dispose()
    {
        // Dispose all providers
        foreach (var provider in _providers)
            provider.Dispose();
        _providers.Clear();
        // Dispose the initalized provider
        _initializedProvider.Dispose();
        // Let external plugins know we're disposed, then dispose the disposed provider.
        _disposedProvider.Invoke();
        _disposedProvider.Dispose();
    }
}