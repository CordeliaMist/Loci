using Dalamud.Interface.Windowing;
using Loci.Gui;
using Loci.Services.Mediator;

namespace Loci.Services;

public sealed class UiService : DisposableMediatorSubscriberBase
{
    private readonly WindowSystem _windowSystem;

    // Never directly called yet, but we can process it via the Hoster using GetServices<WindowMediatorSubscriberBase>() to load all windows.
    public UiService(ILogger<UiService> logger, LociMediator mediator,
        WindowSystem windowSystem, IEnumerable<WindowMediatorSubscriberBase> windows)
        : base(logger, mediator)
    {
        _windowSystem = windowSystem;

        Svc.PluginInterface.UiBuilder.DisableGposeUiHide = true;
        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

        // for each window in the collection of window mediator subscribers
        foreach (var window in windows)
            _windowSystem.AddWindow(window);
    }

    public void OpenMainUi() 
        => Mediator.Publish(new UiToggleMessage(typeof(MainUI)));
    
    // IDealy make this open to a spesific tab, idk.
    public void OpenConfigUi() 
        => Mediator.Publish(new UiToggleMessage(typeof(MainUI)));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Logger.LogTrace($"Disposing {GetType().Name}");
        _windowSystem.RemoveAllWindows();
        // unsubscribe from the draw, open config UI, and main UI
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
    }

    private void Draw()
        => _windowSystem.Draw();
}
