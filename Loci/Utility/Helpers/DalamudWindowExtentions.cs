using CkCommons.Gui;
using Dalamud.Interface.Windowing;
using static Dalamud.Interface.Windowing.Window;

namespace Loci.Gui;

/// <summary>
///     Reduce the boilerplate code of title bar buttons with a builder.
/// </summary>
public class TitleBarButtonBuilder
{
    private readonly List<TitleBarButton> _buttons = new();
    public TitleBarButtonBuilder Add(FAI icon, string tooltip, Action onClick)
    {
        _buttons.Add(new TitleBarButton
        {
            Icon = icon,
            Click = _ => onClick(),
            IconOffset = new Vector2(2, 1),
            ShowTooltip = () => CkGui.AttachToolTip(tooltip),
        });
        return this;
    }
    public List<TitleBarButton> Build() => _buttons;
}

/// <summary>
///     Extension methods that help simplify Dalamud window 
///     setup and operations, to reduce boilerplate code.
/// </summary>
public static class DalamudWindowExtentions
{
    public static void PinningClickthroughFalse(this Window window)
    {
        window.AllowClickthrough = false;
        window.AllowPinning = false;
    }

    public static void SetBoundaries(this Window window, Vector2 minAndMax)
    {
        window.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = minAndMax,
            MaximumSize = minAndMax
        };
    }

    public static void SetBoundaries(this Window window, Vector2 min, Vector2 max)
    {
        window.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = min,
            MaximumSize = max
        };
    }

    public static void SetCloseState(this Window window, bool allowClose)
    {
        window.ShowCloseButton = allowClose;
        window.RespectCloseHotkey = allowClose;
    }
}
