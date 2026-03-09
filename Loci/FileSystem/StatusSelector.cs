using CkCommons.FileSystem;
using CkCommons.FileSystem.Selector;
using CkCommons.Gui;
using CkCommons.RichText;
using CkCommons.Textures;
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
public sealed class StatusSelector : CkFileSystemSelector<LociStatus, StatusSelector.State>, IMediatorSubscriber, IDisposable
{
    private readonly FavoritesConfig _favorites;
    private readonly LociData _data;
    public LociMediator Mediator { get; init; }

    // Remove this later please...
    public record struct State(uint Color) { }

    public new StatusesFS.Leaf? SelectedLeaf => base.SelectedLeaf;

    public StatusSelector(LociMediator mediator, FavoritesConfig favorites, LociData data, StatusesFS fs) 
        : base(fs, Svc.Logger.Logger, Svc.KeyState, "##StatusFS", true)
    {
        Mediator = mediator;
        _favorites = favorites;
        _data = data;

        Mediator.Subscribe<LociStatusChanged>(this, _ => OnStatusChanged(_.Type, _.Item, _.OldString));

        // Do not subscribe to the default renamer, we only want to rename the item itself.
        UnsubscribeRightClickLeaf(RenameLeaf);

        SubscribeRightClickLeaf(CopyToClipboard);
        SubscribeRightClickLeaf(DeleteStatus);
        SubscribeRightClickLeaf(RenameLeaf);
        SubscribeRightClickLeaf(RenameStatus);
    }

    public override ISortMode<LociStatus> SortMode => new StatusSorter();

    private void DeleteStatus(StatusesFS.Leaf leaf)
    {
        using (ImRaii.Disabled(!ImGui.GetIO().KeyShift))
            if (ImGui.Selectable("Delete Status"))
                _data.DeleteStatus(leaf.Value);
        CkGui.AttachToolTip("Delete this status." +
            "--SEP----COL--Must be holding SHIFT--COL--", ImGuiColors.DalamudOrange);
    }

    private void CopyToClipboard(StatusesFS.Leaf leaf)
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

    private void RenameStatus(StatusesFS.Leaf leaf)
    {
        ImGui.Separator();
        var currentName = leaf.Value.Title;
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere(0);
        ImGui.TextUnformatted("Rename Status:");
        if (ImGui.InputText("##RenameStatus", ref currentName, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _data.RenameStatus(leaf.Value, currentName);
            ImGui.CloseCurrentPopup();
        }
        CkGui.AttachToolTip("Enter a new status name..");

        CkRichText.Text(currentName, 6);
    }

    public override void Dispose()
    {
        base.Dispose();
        Mediator.UnsubscribeAll(this);
    }

    protected override bool DrawLeafInner(CkFileSystem<LociStatus>.Leaf leaf, in State _, bool selected)
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
        if (FavStar.Draw(_favorites, StarType.Status, leaf.Value.GUID))
            SetFilterDirty();

        // Skip to the end to draw out the icon?
        ImGui.SameLine((rectMax.X - rectMin.X) - ImGui.GetFrameHeightWithSpacing());
        if (LociIcon.TryGetGameIcon((uint)leaf.Value.IconID, false, out var wrap))
        {
            ImGui.Image(wrap.Handle, LociIcon.SizeFramed);
            Utils.AttachTooltip(leaf.Value);
        }
        // Go back to the beginning 
        ImGui.SameLine(ImUtf8.FrameHeight + ImUtf8.ItemInnerSpacing.X);
        CkGui.TextFrameAligned(leaf.Name);
        return hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left);
    }

    private void OnStatusChanged(FSChangeType _, LociStatus __, string? ___)
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
                var imported = JsonConvert.DeserializeObject<LociStatus>(txt);
                if (imported is not LociStatus status)
                    throw new JsonException("Clipboard text was not a valid LociStatus.");
                // Otherwise, import
                status.GUID = Guid.NewGuid();
                _data.ImportStatus(status);
            }
            catch (JsonException ex)
            {
                Log.Warning($"Failed to import status from clipboard: {ex.Message}");
            }
        }
        CkGui.AttachToolTip("Import a status copied from your clipboard.");
        
        ImGui.SameLine(0, 0);
        if (CkGui.IconButton(FAI.Plus, inPopup: true))
            ImGui.OpenPopup("##NewLociStatus");
        CkGui.AttachToolTip("Create a new LociStatus");

        ImGui.SameLine(0, 0);
        DrawFolderButton();
    }

    public override void DrawPopups()
        => NewStatusPopup();

    private void NewStatusPopup()
    {
        if (!ImGuiUtil.OpenNameField("##NewLociStatus", ref _newName))
            return;

        _data.CreateStatus(_newName);
        _newName = string.Empty;
    }

    // Placeholder until we Integrate the DynamicSorter
    private struct StatusSorter : ISortMode<LociStatus>
    {
        public string Name
            => "Status Sorter";

        public string Description
            => "Sort all statuses by their name, with favorites first.";

        public IEnumerable<CkFileSystem<LociStatus>.IPath> GetChildren(CkFileSystem<LociStatus>.Folder folder)
            => folder.GetSubFolders().Cast<CkFileSystem<LociStatus>.IPath>()
                .Concat(folder.GetLeaves().OrderByDescending(l => FavoritesConfig.Statuses.Contains(l.Value.GUID)));
    }
}

