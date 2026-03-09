using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;

namespace Loci.Services;

/// <summary>
///     Manages Custom fonts during plugin lifetime. <para />
/// </summary>
public static class Fonts
{
    public static IFontHandle IconFont => Svc.PluginInterface.UiBuilder.IconFontFixedWidthHandle;
    public static IFontHandle UidFont { get; private set; }
    public static IFontHandle Default150Percent { get; private set; }
    public static ImFontPtr Default150PercentPtr { get; private set; }

    public static async Task InitializeFonts()
    {
        UidFont = Svc.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(tk =>
        {
            tk.OnPreBuild(tk => tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansJpMedium, new() { SizePx = 35 }));
        });

        Default150Percent = Svc.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(tk =>
        {
            tk.OnPreBuild(tk =>
            {
                Default150PercentPtr = tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 1.5f);
            });
        });

        // Wait for them to be valid.
        await UidFont.WaitAsync().ConfigureAwait(false);
        await Default150Percent.WaitAsync().ConfigureAwait(false);
        await Svc.PluginInterface.UiBuilder.FontAtlas.BuildFontsAsync().ConfigureAwait(false);
        Svc.Logger.Information("Fonts: Initialized Necessary fonts.");
    }

    public static void Dispose()
    {
        Svc.Logger.Information("Disposing Fonts.");
        UidFont?.Dispose();
        Default150Percent?.Dispose();
    }
}
