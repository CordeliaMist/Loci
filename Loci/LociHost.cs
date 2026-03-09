using Loci.Data;
using Loci.Gui;
using Loci.Services;
using Loci.Services.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Loci;

public class LociHost : MediatorSubscriberBase, IHostedService
{
    private readonly MainConfig _config;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private IServiceScope? _lifetimeScope;
    public LociHost(ILogger<LociHost> logger, LociMediator mediator, MainConfig mainConfig,
        IServiceScopeFactory scopeFactory) : base(logger, mediator)
    {
        _config = mainConfig;
        _serviceScopeFactory = scopeFactory;
    }
    /// <summary> 
    ///     The task to run after all services have been properly constructed. <para />
    ///     This will kickstart the server and begin all operations and verifications.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        Logger.LogInformation($"Starting Loci v{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}");

        // start processing the mediator queue.
        Mediator.StartQueueProcessing();

        // Init the plugin lifetime scope.
        _lifetimeScope = _serviceScopeFactory.CreateScope();
        _lifetimeScope.ServiceProvider.GetRequiredService<UiService>();

        TryDisplayChangelog();
        if (_config.Current.OpenOnStartup)
            Mediator.Publish(new UiToggleMessage(typeof(MainUI)));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        UnsubscribeAll();
        Logger.LogDebug("Shutting down Loci");
        _lifetimeScope?.Dispose();
        return Task.CompletedTask;
    }

    private void TryDisplayChangelog()
    {
        // display changelog if we should.
        if (_config.Current.LastRunVersion != Assembly.GetExecutingAssembly().GetName().Version!)
        {
            // update the version and toggle the UI.
            Logger?.LogInformation("Version was different, displaying UI");
            _config.Current.LastRunVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            _config.Save();
            // Mediator.Publish(new UiToggleMessage(typeof(ChangelogUI)));
        }
    }
}
