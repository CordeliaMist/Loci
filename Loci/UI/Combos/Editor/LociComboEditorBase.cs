using CkCommons.Textures;
using Loci.Data;

namespace Loci.Combos;

public abstract class LociComboEditorBase<T> : CkFilterComboCache<T>
{
    protected readonly LociManager _manager;
    protected float IconScale;

    protected LociComboEditorBase(ILogger log, LociManager manager, float scale, Func<IReadOnlyList<T>> generator)
        : base(generator, log)
    {
        _manager = manager;
        IconScale = scale;
        Current = default;
    }

    protected virtual Vector2 IconSize => LociIcon.Size * IconScale;
}
