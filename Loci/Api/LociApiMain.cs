using Loci.Data;
using Loci.Services.Mediator;
using LociApi.Api;

namespace Loci.Api;

public class LociApiMain : DisposableMediatorSubscriberBase, ILociApi
{
    private readonly MainConfig _config;
    private readonly RegistryApi _registry;
    private readonly StatusManagerApi _statusManagers;
    private readonly StatusApi _statuses;
    private readonly PresetApi _presets;
    private readonly EventApi _events;

    // Our API Version, exposed to other plugins for compatibility checking.
    public const int VERSION_MAJOR = 1;
    public const int VERSION_MINOR = 1;

    public LociApiMain(
        ILogger<LociApiMain> logger,
        LociMediator mediator,
        MainConfig config,
        RegistryApi registry,
        StatusManagerApi statusManagers,
        StatusApi statuses,
        PresetApi presets,
        EventApi events) : base(logger, mediator)
    {
        _config = config;
        _registry = registry;
        _statusManagers = statusManagers;
        _statuses = statuses;
        _presets = presets;
        _events = events;

        Mediator.Subscribe<NewEnabledStateMessage>(this, _ => EnabledStateChanged?.Invoke(_.NewState));
    }

    // ApiBase
    public (int Major, int Minor) ApiVersion => (VERSION_MAJOR, VERSION_MINOR);
    public bool IsEnabled => _config.Current.Enabled;
    public event Action<bool>? EnabledStateChanged;

    // Plugin API Components
    public ILociApiRegistry Registry => _registry;
    public ILociApiStatusManager StatusManager => _statusManagers;
    public ILociApiStatuses Statuses => _statuses;
    public ILociApiPresets Presets => _presets;
    public ILociApiEvents Events => _events;
}