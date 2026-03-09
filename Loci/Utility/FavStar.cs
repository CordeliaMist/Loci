using CkCommons.Gui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Loci.Data;

namespace Loci;
public static class FavStar
{
    public static bool Draw(FavoritesConfig config, StarType type, Guid id, bool framed = true)
    {
        var isFavorite = type switch
        {
            StarType.Status => FavoritesConfig.Statuses.Contains(id),
            StarType.Preset => FavoritesConfig.Presets.Contains(id),
            StarType.Event => FavoritesConfig.Events.Contains(id),
            _ => false
        };
        var pos = ImGui.GetCursorScreenPos();
        var hovering = ImGui.IsMouseHoveringRect(pos, pos + new Vector2(ImGui.GetTextLineHeight()));
        var col = hovering ? ImGuiColors.DalamudGrey2 : isFavorite ? ImGuiColors.ParsedGold : ImGuiColors.ParsedGrey;

        if (framed)
            CkGui.FramedIconText(FAI.Star, col);
        else
            CkGui.IconText(FAI.Star, col);
        CkGui.AttachToolTip((isFavorite ? "Remove" : "Add") + " from Favorites.");

        if (hovering && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (isFavorite) config.Unfavorite(type, id);
            else config.Favorite(type, id);
            return true;
        }
        return false;
    }

    public static bool Draw(this FavoritesConfig config, uint iconId, bool framed)
    {
        var isFavorite = FavoritesConfig.IconIDs.Contains(iconId);
        var pos = ImGui.GetCursorScreenPos();
        var hovering = ImGui.IsMouseHoveringRect(pos, pos + new Vector2(ImGui.GetTextLineHeight()));
        var col = hovering ? ImGuiColors.DalamudGrey2 : isFavorite ? ImGuiColors.ParsedGold : ImGuiColors.ParsedGrey;

        if (framed)
            CkGui.FramedIconText(FAI.Star, col);
        else
            CkGui.IconText(FAI.Star, col);
        CkGui.AttachToolTip((isFavorite ? "Remove" : "Add") + " from Favorites.");

        if (hovering && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (isFavorite) config.Unfavorite(iconId);
            else config.Favorite(iconId);
            return true;
        }
        return false;
    }
}
