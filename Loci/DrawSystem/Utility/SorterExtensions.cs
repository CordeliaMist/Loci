using CkCommons.DrawSystem;
using Loci.Data;

namespace Loci.DrawSystem;

public static class SorterExtensions
{
    public static readonly ISortMethod<DynamicLeaf<ActorSM>> ByName = new ActorName();

    public struct ActorName : ISortMethod<DynamicLeaf<ActorSM>>
    {
        public string Name => "Name";
        public FAI Icon => FAI.SortAlphaDown; // Maybe change.
        public string Tooltip => "Sort by name.";
        public Func<DynamicLeaf<ActorSM>, IComparable?> KeySelector => l => l.Data.Identifier;
    }
}

