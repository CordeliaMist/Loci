using CkCommons.DrawSystem;
using CkCommons.DrawSystem.Selector;
using Loci.Data;

namespace Loci.DrawSystem;
#pragma warning disable CS9107

public class SMDrawCache(DynamicDrawSystem<ActorSM> dds) : DynamicFilterCache<ActorSM>(dds)
{
    /// <summary>
    ///     If the config options under the filter bar should show.
    /// </summary>
    public bool FilterConfigOpen = false;

    /// <summary>
    ///     Which folders to show the hidden names for when drawing it's children.
    /// </summary>
    public HashSet<string> IncognitoFolders = new(StringComparer.Ordinal);

    protected override bool IsVisible(IDynamicNode<ActorSM> node)
    {
        if (Filter.Length is 0)
            return true;

        // If a folder, sort by name, but also run through a second kind of
        // filter if show preferred folders is active or something.

        if (node is DynamicLeaf<ActorSM> leaf)
            return leaf.Data.Identifier.Contains(Filter, StringComparison.OrdinalIgnoreCase);

        return base.IsVisible(node);
    }
}
#pragma warning restore CS9107 