using CkCommons.FileSystem;
using CkCommons.FileSystem.Selector;
using CkCommons.Gui;
using CkCommons.RichText;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.Services.Mediator;
using OtterGui;
using OtterGui.Text;

namespace Loci.DrawSystem;

// Continue reworking this to integrate a combined approach if we can figure out a better file management system.
public sealed class EventsSelector : CkFileSystemSelector<LociEvent, EventsSelector.State>, IMediatorSubscriber, IDisposable
{
    private readonly FavoritesConfig _favorites;
    private readonly LociEventData _data;
    public LociMediator Mediator { get; init; }

    // Remove this later please...
    public record struct State(uint Color) { }

    public new LociEventsFS.Leaf? SelectedLeaf => base.SelectedLeaf;

    public EventsSelector(LociMediator mediator, FavoritesConfig favorites, LociEventData data, LociEventsFS fs) 
        : base(fs, Svc.Logger.Logger, Svc.KeyState, "##EventsFS", true)
    {
        Mediator = mediator;
        _favorites = favorites;
        _data = data;

        Mediator.Subscribe<LociEventChanged>(this, _ => OnEventsChanged(_.Type, _.Item, _.OldString));

        // Do not subscribe to the default renamer, we only want to rename the item itself.
        UnsubscribeRightClickLeaf(RenameLeaf);

        SubscribeRightClickLeaf(CopyToClipboard);
        SubscribeRightClickLeaf(CloneEvent);
        SubscribeRightClickLeaf(DeleteEvents);
        SubscribeRightClickLeaf(RenameLeaf);
        SubscribeRightClickLeaf(RenameEvents);
    }

    public override ISortMode<LociEvent> SortMode => new EventsSorter();

    private void DeleteEvents(LociEventsFS.Leaf leaf)
    {
        using (ImRaii.Disabled(!ImGui.GetIO().KeyShift))
            if (ImGui.Selectable("Delete Event"))
                _data.DeleteEvent(leaf.Value);
        CkGui.AttachToolTip("Delete this event." +
            "--SEP----COL--Must be holding SHIFT--COL--", ImGuiColors.DalamudOrange);
    }

    private void CopyToClipboard(LociEventsFS.Leaf leaf)
    {
        if (ImGui.Selectable("Copy to clipboard", false))
        {
            var copy = leaf.Value.NewtonsoftDeepClone();
            // clear the GUID.
            copy.GUID = Guid.Empty;
            // Copy it
            var copyText = JsonConvert.SerializeObject(copy);
            ImGui.SetClipboardText(copyText);
        }
    }

    private void CloneEvent(LociEventsFS.Leaf leaf)
    {
        if (ImGui.Selectable("Clone Event", false))
            _data.CloneEvent(leaf.Value, leaf.Value.Title);
        CkGui.AttachToolTip("Create a clone of this event");
    }

    private void RenameEvents(LociEventsFS.Leaf leaf)
    {
        ImGui.Separator();
        var prevName = leaf.Value.Title;
        var currentName = leaf.Value.Title;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere(0);
        ImGui.TextUnformatted("Rename Event:");
        if (ImGui.InputText("##RenameEvent", ref currentName, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (prevName != currentName)
                _data.RenameEvent(leaf.Value, prevName);
            ImGui.CloseCurrentPopup();
        }
        CkGui.AttachToolTip("Enter a new event name..");

        CkRichText.Text(currentName, 6);
    }

    public override void Dispose()
    {
        base.Dispose();
        Mediator.UnsubscribeAll(this);
    }

    protected override bool DrawLeafInner(CkFileSystem<LociEvent>.Leaf leaf, in State _, bool selected)
    {
        var leafSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight());
        ImGui.Dummy(leafSize);
        var hovered = ImGui.IsItemHovered();
        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var bgColor = hovered ? ImGui.GetColorU32(ImGuiCol.FrameBgHovered) : CkGui.Color(new Vector4(0.25f, 0.2f, 0.2f, 0.4f));
        ImGui.GetWindowDrawList().AddRectFilled(rectMin, rectMax, bgColor, 5);

        if (selected)
        {
            ImGui.GetWindowDrawList().AddRectFilledMultiColor(rectMin, rectMin + leafSize, ColorHelpers.Darken(LociCol.Gold.Uint(), .65f), 0, 0, ColorHelpers.Darken(LociCol.Gold.Uint(), .65f));
            ImGui.GetWindowDrawList().AddRectFilled(rectMin, new Vector2(rectMin.X + ImGuiHelpers.GlobalScale * 3, rectMax.Y), LociCol.Gold.Uint(), 5);
        }

        ImGui.SetCursorScreenPos(rectMin);
        if (FavStar.Draw(_favorites, StarType.Event, leaf.Value.GUID))
            SetFilterDirty();

        CkGui.TextFrameAlignedInline(leaf.Name);
        return hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left);
    }

    private void OnEventsChanged(FSChangeType _, LociEvent __, string? ___)
        => SetFilterDirty();

    /// <summary> Add the state filter combo-button to the right of the filter box. </summary>
    protected override float CustomFiltersWidth(float width)
        => CkGui.IconButtonSize(FAI.FileImport).X +  CkGui.IconButtonSize(FAI.Plus).X +  CkGui.IconButtonSize(FAI.FolderPlus).X + ImUtf8.ItemInnerSpacing.X;

    protected override void DrawCustomFilters()
    {
        if (CkGui.IconButton(FAI.FileImport, inPopup: true))
        {
            var txt = ImGuiUtil.GetClipboardText();
            try
            {
                var imported = JsonConvert.DeserializeObject<LociEvent>(txt);
                if (imported is not LociEvent events)
                    throw new JsonException("Clipboard text was not a valid LociEvent.");
                // Otherwise, import
                events.GUID = Guid.NewGuid();
                _data.ImportEvent(events);
            }
            catch (JsonException ex)
            {
                Log.Warning($"Failed to import events from clipboard: {ex.Message}");
            }
        }
        CkGui.AttachToolTip("Import a events copied from your clipboard.");
        
        ImGui.SameLine(0, 0);
        if (CkGui.IconButton(FAI.Plus, inPopup: true))
            ImGui.OpenPopup("##NewLociEvent");
        CkGui.AttachToolTip("Create a new LociEvent");

        ImGui.SameLine(0, 0);
        DrawFolderButton();
    }

    public override void DrawPopups()
        => NewEventsPopup();

    private void NewEventsPopup()
    {
        if (!ImGuiUtil.OpenNameField("##NewLociEvent", ref _newName))
            return;

        _data.CreateEvent(_newName);
        _newName = string.Empty;
    }

    // Placeholder until we Integrate the DynamicSorter
    private struct EventsSorter : ISortMode<LociEvent>
    {
        public string Name
            => "Events Sorter";

        public string Description
            => "Sort all events by their name, with favorites first.";

        public IEnumerable<CkFileSystem<LociEvent>.IPath> GetChildren(CkFileSystem<LociEvent>.Folder folder)
            => folder.GetSubFolders().Cast<CkFileSystem<LociEvent>.IPath>()
                .Concat(folder.GetLeaves().OrderByDescending(l => FavoritesConfig.Events.Contains(l.Value.GUID)));
    }
}

