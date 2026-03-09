namespace Loci.Data;

[Serializable]
public class LociPreset
{
    internal string ID => GUID.ToString();
    public Guid GUID = Guid.NewGuid();
    
    public List<Guid> Statuses = [];
    public PresetApplyType ApplyType = PresetApplyType.UpdateExisting;
    public string Title = "";
    public string Description = "";

    public bool ShouldSerializeGUID()
        => GUID != Guid.Empty;

    public LociPresetInfo ToTuple()
        => (GUID, Statuses, (byte)ApplyType, Title, Description);
}
