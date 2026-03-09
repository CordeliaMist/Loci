using CkCommons.DrawSystem;
using Dalamud.Bindings.ImGui;
using Loci.Data;
using CkCommons;

namespace Loci.DrawSystem;

public sealed class ManagerFolder : DynamicFolder<ActorSM>
{
    private Func<IReadOnlyList<ActorSM>> _generator;
    public ManagerFolder(DynamicFolderGroup<ActorSM> parent, uint id, FAI icon, string name,
        uint iconColor, Func<IReadOnlyList<ActorSM>> generator)
        : base(parent, icon, name, id)
    {
        // Can set stylizations here.
        NameColor = uint.MaxValue;
        IconColor = iconColor;
        BgColor = uint.MinValue;
        BorderColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        GradientColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        _generator = generator;
    }

    public ManagerFolder(DynamicFolderGroup<ActorSM> parent, uint id, FAI icon, string name,
        uint iconColor, Func<IReadOnlyList<ActorSM>> generator, IReadOnlyList<ISortMethod<DynamicLeaf<ActorSM>>> sortSteps)
        : base(parent, icon, name, id, new(sortSteps))
    {
        // Can set stylizations here.
        NameColor = uint.MaxValue;
        IconColor = iconColor;
        BgColor = uint.MinValue;
        BorderColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        _generator = generator;
    }

    public ManagerFolder(DynamicFolderGroup<ActorSM> parent, uint id, FAI icon, string name,
        uint iconColor, Func<IReadOnlyList<ActorSM>> generator, DynamicSorter<DynamicLeaf<ActorSM>> sorter)
        : base(parent, icon, name, id, sorter)
    {
        // Can set stylizations here.
        NameColor = uint.MaxValue;
        IconColor = iconColor;
        BgColor = uint.MinValue;
        BorderColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        _generator = generator;
    }

    protected override IReadOnlyList<ActorSM> GetAllItems() => _generator();
    protected override DynamicLeaf<ActorSM> ToLeaf(ActorSM item) => new(this, item.Identifier, item);

    public string BracketText => $"[{TotalChildren}]";
    public string BracketTooltip => $"{TotalChildren} total";

    public void ApplySorter(IReadOnlyList<ISortMethod<DynamicLeaf<ActorSM>>> sortSteps)
        => Sorter.SetSteps(sortSteps);
}