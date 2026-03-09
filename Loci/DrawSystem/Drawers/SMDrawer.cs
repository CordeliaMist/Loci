using CkCommons.DrawSystem;
using CkCommons.DrawSystem.Selector;
using CkCommons.Gui;
using CkCommons.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Loci.Data;
using Loci.Services.Mediator;
using OtterGui.Text;

namespace Loci.DrawSystem;

public sealed class SMDrawer : DynamicDrawer<ActorSM>
{
    private readonly LociMediator _mediator;
    private readonly MainConfig _config;
    private readonly FavoritesConfig _favorites;
    private readonly LociManager _manager;
    private readonly SMDrawSystem _drawSystem;

    private SMDrawCache _cache => (SMDrawCache)FilterCache;

    public SMDrawer(LociMediator mediator, MainConfig config,
        FavoritesConfig favorites, LociManager manager, SMDrawSystem ds)
        : base("##ManagerDrawer", Svc.Logger.Logger, ds, new SMDrawCache(ds))
    {
        _mediator = mediator;
        _config = config;
        _favorites = favorites;
        _manager = manager;
        _drawSystem = ds;
    }

    public ActorSM? Selected => Selector.SelectedLeaf?.Data;

    public void DrawFoldersOnly(float width, DynamicFlags flags = DynamicFlags.None)
    {
        HandleMainContext();
        FilterCache.UpdateCache();
        DrawFolderGroupChildren(FilterCache.RootCache, ImUtf8.FrameHeight * .65f, ImUtf8.FrameHeight + ImUtf8.ItemInnerSpacing.X, flags);
        PostDraw();
    }

    //protected override void DrawSearchBar(float width, int length)
    //{
    //    var tmp = FilterCache.Filter;
    //    // Update the search bar if things change, like normal.
    //    if (FancySearchBar.Draw("Filter", width, ref tmp, "filter..", length, CkGui.IconTextButtonSize(FAI.Cog, "Settings"), DrawButtons))
    //        FilterCache.Filter = tmp;

    //    // If the config is expanded, draw that.
    //    if (_cache.FilterConfigOpen)
    //        DrawFilterConfig(width);

    //    void DrawButtons()
    //    {
    //        if (CkGui.IconTextButton(FAI.Cog, "Settings", isInPopup: !_cache.FilterConfigOpen))
    //            _cache.FilterConfigOpen = !_cache.FilterConfigOpen;
    //        CkGui.AttachToolTip("Configure preferences for draw folders.");
    //    }
    //}

    // Draws the grey line around the filtered content when expanded and stuff.
    //protected override void PostSearchBar()
    //{
    //    if (_cache.FilterConfigOpen)
    //        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.Button), 5f);
    //}

    protected override void DrawFolderBannerInner(IDynamicFolder<ActorSM> folder, Vector2 region, DynamicFlags flags)
        => DrawFolderInner((ManagerFolder)folder, region, flags);

    private void DrawFolderInner(ManagerFolder folder, Vector2 region, DynamicFlags flags)
    {
        var pos = ImGui.GetCursorPos();
        CkGui.FramedIconText(folder.IsOpen ? FAI.CaretDown : FAI.CaretRight);
        CkGui.ColorTextFrameAlignedInline(folder.Name, folder.NameColor);
        CkGui.ColorTextFrameAlignedInline($"[{folder.TotalChildren}]", ImGuiColors.DalamudGrey2);
        CkGui.AttachToolTip(folder.BracketTooltip);

        var endX = ImGui.GetWindowContentRegionMin().X + CkGui.GetWindowContentRegionWidth();
        endX -= CkGui.IconButtonSize(FAI.EyeSlash).X;
        ImGui.SameLine(endX);
        if (CkGui.IconButton(_cache.IncognitoFolders.Contains(folder.Name) ? FAI.Eye : FAI.EyeSlash, inPopup: true))
        {
            if (!_cache.IncognitoFolders.Remove(folder.Name))
                _cache.IncognitoFolders.Add(folder.Name);
        }
        CkGui.AttachToolTip("Toggles Anonymous View");

        ImGui.SameLine(pos.X);
        if (ImGui.InvisibleButton($"{Label}_node_{folder.ID}", new(endX - pos.X, region.Y)))
            HandleLeftClick(folder, flags);
        HandleDetections(folder, flags);
    }

    protected override void DrawLeaf(IDynamicLeaf<ActorSM> leaf, DynamicFlags flags, bool selected)
    {
        var cursorPos = ImGui.GetCursorPos();
        var size = new Vector2(CkGui.GetWindowContentRegionWidth() - cursorPos.X, ImUtf8.FrameHeight);
        var bgCol = selected ? ImGui.GetColorU32(ImGuiCol.FrameBgHovered) : 0;
        using (var _ = CkRaii.Child(Label + leaf.Name, size, bgCol, 5f))
            DrawSMLeaf(leaf, _.InnerRegion, flags);
    }

    private void DrawSMLeaf(IDynamicLeaf<ActorSM> leaf, Vector2 region, DynamicFlags flags)
    {
        ImUtf8.SameLineInner();
        // Store current position, then draw the right side.
        var posX = ImGui.GetCursorPosX();
        if (ImGui.InvisibleButton($"{leaf.FullPath}-name-area", region))
            HandleLeftClick(leaf, flags);
        HandleDetections(leaf, flags);

        // Then return to the start position and draw out the text.
        ImGui.SameLine(posX);
        var txt = _cache.IncognitoFolders.Contains(leaf.Parent.Name)
            ? string.Join(" ", leaf.Data.Identifier.Split(" ").Select(x => $"{x[0]}."))
            : leaf.Data.Identifier;
        CkGui.TextFrameAligned(txt);
    }

    private float DrawRightButtons(IDynamicLeaf<ActorSM> leaf, DynamicFlags flags)
    {
        var endX = ImGui.GetWindowContentRegionMin().X + CkGui.GetWindowContentRegionWidth();
        //endX -= ImUtf8.FrameHeight;

        //ImGui.SameLine(endX);
        //FavStar.Draw(_favorites, leaf.Data.Identifier, true);
        return endX;
    }
}

