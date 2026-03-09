using CkCommons;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Loci.Commands;
using Loci.Data;
using Loci.DrawSystem;
using Loci.Gui;
using Loci.Gui.Components;
using Loci.Interop;
using Loci.Processors;
using Loci.Services;
using Loci.Services.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loci;

public sealed class Loci : IDalamudPlugin
{
    private readonly IHost _host;  // the host builder for the plugin instance. (What makes everything work)
    public Loci(IDalamudPluginInterface pi)
    {
        pi.Create<Svc>();
        // init GameData storages for the client language.
        GameDataSvc.Init(pi);
        // init the CkCommons.
        CkCommonsHost.Init(pi, this, CkLogFilter.None);
        // create the host builder for the plugin
        _host = ConstructHostBuilder(pi);
        // start up the host
        _ = _host.StartAsync();
        _ = Fonts.InitializeFonts().ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Stop the host.
        _host.StopAsync().GetAwaiter().GetResult();
        // Dispose of CkCommons.
        CkCommonsHost.Dispose();
        // Dispose cleanup of GameDataSvc.
        GameDataSvc.Dispose();
        // Dispose the Host.
        _host.Dispose();
        // Dispose of fonts.
        Fonts.Dispose();
    }

    // Method that creates the host builder for the Loci plugin
    public IHost ConstructHostBuilder(IDalamudPluginInterface pi)
    => new HostBuilder()
        // Get the content root for our plugin
        .UseContentRoot(pi.ConfigDirectory.FullName)
        // Configure the logging for the plugin
        .ConfigureLogging((hostContext, loggingBuilder) => GetPluginLogConfiguration(loggingBuilder))
        // Get the plugin service collection for our plugin
        .ConfigureServices((hostContext, serviceCollection) => GetPluginServices(serviceCollection))
        .Build();

    private void GetPluginLogConfiguration(ILoggingBuilder logBuilder)
    => logBuilder
        .ClearProviders()
        .AddDalamudLogging()
        .SetMinimumLevel(LogLevel.Trace);

    public IServiceCollection GetPluginServices(IServiceCollection collection)
    => collection
        .AddSingleton(new WindowSystem("Loci"))
        .AddGeneric()
        .AddIPC()
        .AddConfigs()
        .AddScoped()
        .AddHosted();
}

public static class LociServiceExtensions
{
    #region GenericServices
    public static IServiceCollection AddGeneric(this IServiceCollection services)
    => services
        // Necessary Services
        .AddSingleton<ILoggerProvider, Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>()
        .AddSingleton<LociHost>()
        // Draw Systems
        .AddSingleton<SMDrawer>()
        .AddSingleton<PresetSelector>()
        .AddSingleton<StatusSelector>()
        // Loci
        .AddSingleton<LociMemory>()
        .AddSingleton<LociProcessor>()
        .AddSingleton<FlyPopupTextProcessor>()
        .AddSingleton<FocusTargetInfoProcessor>()
        .AddSingleton<PartyListProcessor>()
        .AddSingleton<StatusProcessor>()
        .AddSingleton<StatusCustomProcessor>()
        .AddSingleton<TargetInfoProcessor>()
        .AddSingleton<TargetInfoBuffDebuffProcessor>()
        // Services
        .AddSingleton<CharaWatcher>()
        .AddSingleton<LociMediator>()
        // UI
        .AddSingleton<LociUITabs>()
        // Ipc Provider
        .AddSingleton<IpcProvider>();
    #endregion GenericServices

    public static IServiceCollection AddIPC(this IServiceCollection services)
    => services
        .AddSingleton<IpcProvider>();
    public static IServiceCollection AddConfigs(this IServiceCollection services)
    => services
        // Purely Client
        .AddSingleton<MainConfig>()
        .AddSingleton<LociData>()
        .AddSingleton<FavoritesConfig>()
        .AddSingleton<LociManager>()
        // DDS & CKFS
        .AddSingleton<SMDrawSystem>()
        .AddSingleton<StatusesFS>() 
        .AddSingleton<PresetsFS>()
        // Managers / Savers
        .AddSingleton<FileProvider>()
        .AddSingleton<SaveService>();

    #region ScopedServices
    public static IServiceCollection AddScoped(this IServiceCollection services)
    => services
        .AddScoped<WindowMediatorSubscriberBase, MainUI>()
        .AddScoped<StatusesTab>()
        .AddScoped<PresetsTab>()
        .AddScoped<ManagersTab>()
        .AddScoped<SettingsTab>()
        .AddScoped<DebugTab>()
        .AddScoped<IpcTesterTab>()
        .AddScoped<IpcTesterRegistration>()
        .AddScoped<IpcTesterStatusManagers>()
        .AddScoped<IpcTesterStatuses>()
        .AddScoped<IpcTesterPresets>()       
        .AddScoped<DDSDebugger>()
        .AddScoped<CommandManager>()
        .AddScoped<UiService>();
    #endregion ScopedServices

    public static IServiceCollection AddHosted(this IServiceCollection services)
    => services
        .AddHostedService(p => p.GetRequiredService<SaveService>())
        .AddHostedService(p => p.GetRequiredService<LociMediator>())
        .AddHostedService(p => p.GetRequiredService<CharaWatcher>())
        .AddHostedService(p => p.GetRequiredService<IpcProvider>())
        .AddHostedService(p => p.GetRequiredService<LociMemory>())
        .AddHostedService(p => p.GetRequiredService<LociProcessor>())
        .AddHostedService(p => p.GetRequiredService<LociHost>());
}