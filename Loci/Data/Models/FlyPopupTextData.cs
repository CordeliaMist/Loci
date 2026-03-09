namespace Loci.Data;
public class FlyPopupTextData
{
    public LociStatus Status;
    public bool IsAddition;
    public uint OwnerEntityId;

    public FlyPopupTextData(LociStatus status, bool isAddition, uint owner)
    {
        Status = status;
        IsAddition = isAddition;
        OwnerEntityId = owner;
    }
}
