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
public sealed class PresetSelector : CkFileSystemSelector<LociPreset, PresetSelector.State>, IMediatorSubscriber, IDisposable
{
    private readonly FavoritesConfig _favorites;
    private readonly LociData _data;
    public LociMediator Mediator { get; init; }

    // Remove this later please...
    public record struct State(uint Color) { }

    public new PresetsFS.Leaf? SelectedLeaf => base.SelectedLeaf;

    public PresetSelector(LociMediator mediator, FavoritesConfig favorites, LociData data, PresetsFS fs) 
        : base(fs, Svc.Logger.Logger, Svc.KeyState, "##PresetFS", true)
    {
        Mediator = mediator;
        _favorites = favorites;
        _data = data;

        Mediator.Subscribe<LociPresetChanged>(this, _ => OnPresetChanged(_.Type, _.Item, _.OldString));

        // Do not subscribe to the default renamer, we only want to rename the item itself.
        UnsubscribeRightClickLeaf(RenameLeaf);

        SubscribeRightClickLeaf(CopyToClipboard);
        SubscribeRightClickLeaf(DeletePreset);
        SubscribeRightClickLeaf(RenamePreset);
    }

    public override ISortMode<LociPreset> SortMode => new PresetSorter();

    private void DeletePreset(PresetsFS.Leaf leaf)
    {
        using (ImRaii.Disabled(!ImGui.GetIO().KeyShift))
            if (ImGui.Selectable("Delete Preset"))
                _data.DeletePreset(leaf.Value);
        CkGui.AttachToolTip("Delete this preset.--SEP----COL--Must be holding SHIFT--COL--", ImGuiColors.DalamudOrange);
    }

    private void CopyToClipboard(PresetsFS.Leaf leaf)
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

    private void RenamePreset(PresetsFS.Leaf leaf)
    {
        using (ImRaii.Group())
        {
            var currentName = leaf.Value.Title;
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere(0);
            ImGui.TextUnformatted("Rename Preset:");
            if (ImGui.InputText("##RenamePreset", ref currentName, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                _data.RenamePreset(leaf.Value, currentName);
                ImGui.CloseCurrentPopup();
            }
            CkGui.AttachToolTip("Enter a new preset name..");

            CkRichText.Text(currentName, 6);
        }
        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.Border), 1);
    }

    public override void Dispose()
    {
        base.Dispose();
        Mediator.UnsubscribeAll(this);
    }

    protected override bool DrawLeafInner(CkFileSystem<LociPreset>.Leaf leaf, in State _, bool selected)
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
        if (FavStar.Draw(_favorites, StarType.Preset, leaf.Value.GUID))
            SetFilterDirty();
        CkGui.TextFrameAlignedInline(leaf.Name);
        return hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left);
    }

    /// <summary> Just set the filter to dirty regardless of what happened. </summary>
    private void OnPresetChanged(FSChangeType _, LociPreset __, string? ___)
        => SetFilterDirty();

    /// <summary> Add the state filter combo-button to the right of the filter box. </summary>
    protected override float CustomFiltersWidth(float width)
        => CkGui.IconButtonSize(FAI.FileImport).X + CkGui.IconButtonSize(FAI.Plus).X + CkGui.IconButtonSize(FAI.FolderPlus).X + ImUtf8.ItemInnerSpacing.X;

    protected override void DrawCustomFilters()
    {
        if (CkGui.IconButton(FAI.FileImport, inPopup: true))
        {
            var txt = ImGuiUtil.GetClipboardText();
            try
            {
                var imported = JsonConvert.DeserializeObject<LociPreset>(txt);
                if (imported is not LociPreset preset)
                    throw new JsonException("Clipboard text was not a valid LociPreset.");
                // Otherwise, import
                preset.GUID = Guid.NewGuid();
                _data.ImportPreset(preset);
            }
            catch (JsonException ex)
            {
                Log.Warning($"Failed to import preset from clipboard: {ex.Message}");
            }
        }
        CkGui.AttachToolTip("Import a preset copied from your clipboard.");

        ImGui.SameLine(0, 1);
        if (CkGui.IconButton(FAI.Plus, inPopup: true))
            ImGui.OpenPopup("##NewPreset");
        CkGui.AttachToolTip("Create a new LociPreset.");

        ImGui.SameLine(0, 1);
        DrawFolderButton();
    }

    public override void DrawPopups()
        => NewPresetPopup();

    private void NewPresetPopup()
    {
        if (!ImGuiUtil.OpenNameField("##NewPreset", ref _newName))
            return;

        _data.CreatePreset(_newName);
        _newName = string.Empty;
    }

    // Placeholder until we Integrate the DynamicSorter
    private struct PresetSorter : ISortMode<LociPreset>
    {
        public string Name
            => "Preset Sorter";

        public string Description
            => "Sort all presets by their name, with favorites first.";

        public IEnumerable<CkFileSystem<LociPreset>.IPath> GetChildren(CkFileSystem<LociPreset>.Folder folder)
            => folder.GetSubFolders().Cast<CkFileSystem<LociPreset>.IPath>()
                .Concat(folder.GetLeaves().OrderByDescending(l => FavoritesConfig.Presets.Contains(l.Value.GUID)));
    }
}

