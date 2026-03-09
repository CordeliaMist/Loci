using Loci.Gui.Components;

namespace Loci.Services.Mediator;

public enum ToggleType { Toggle, Show, Hide }

/// <summary>
///     Basic UI Toggle
/// </summary>
public record UiToggleMessage(Type UiType, ToggleType ToggleType = ToggleType.Toggle) : MessageBase;

/// <summary>
///     Force-Opens the MainUI to a specific tab.
/// </summary>
public record OpenMainUiTab(LociUITabs.SelectedTab ToOpen) : MessageBase;

/// <summary>
///     Fired when the main UI's tabbar changes.
/// </summary>
public record TabBarChangedMessage(LociUITabs.SelectedTab NewTab) : MessageBase;
