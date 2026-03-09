using MemoryPack;

namespace Loci.Data;

// Updated MyStatus for desired new features holding updated structure.
[Serializable]
[MemoryPackable]
public partial class LociStatus
{
    internal string ID => GUID.ToString();

    // Essential
    public const int Version = 3;
    public Guid GUID = Guid.NewGuid();
    public int IconID;
    public string Title = "";
    public string Description = "";
    public string CustomFXPath = "";
    public long ExpiresAt;

    // Attributes
    public StatusType Type;
    public Modifiers Modifiers; // What can be customized with this loci.
     
    public int Stacks = 1;
    public int StackSteps = 0; // How many stacks to add per reapplication.
    public int StackToChain = 0; // Only applicable when ChainTrigger is related to StackCount.
    
    // Chaining Status (Applies when ChainTrigger condition is met)
    public Guid ChainedGUID = Guid.Empty;
    public ChainType ChainedType = ChainType.Status;
    public ChainTrigger ChainTrigger;

    // Additional Behavior added overtime.
    public string Applier = "";
    public string Dispeller = ""; // Person who must be the one to dispel you.

    // Anything else that wants to be added here later that cant fit
    // into Modifiers or ChainTrigger can fit below cleanly.


    #region Conditional Serialization/Deserialization
    // No longer needed, unless im missing something
    [MemoryPackIgnore] public bool Persistent = false;

    // Internals used to track data in the common processors.
    [NonSerialized] internal bool ApplyChain = false; // Informs processor to apply chain.
    [NonSerialized] internal bool ClickedOff = false; // Set when the status is right clicked off.
    [NonSerialized] internal int TooltipShown = -1;

    [MemoryPackIgnore] public int Days = 0;
    [MemoryPackIgnore] public int Hours = 0;
    [MemoryPackIgnore] public int Minutes = 0;
    [MemoryPackIgnore] public int Seconds = 0;
    [MemoryPackIgnore] public bool NoExpire = false;

    public bool ShouldSerializeGUID() => GUID != Guid.Empty;
    public bool ShouldSerializePersistent() => ShouldSerializeGUID();
    public bool ShouldSerializeExpiresAt() => ShouldSerializeGUID();

    #endregion Conditional Serialization/Deserialization

    internal uint AdjustedIconID => (uint)(IconID + Stacks - 1);
    internal long TotalMilliseconds => Seconds * 1000L + Minutes * 1000L * 60 + Hours * 1000L * 60 * 60 + Days * 1000L * 60 * 60 * 24;

    public bool ShouldExpireOnChain()
        => ApplyChain && !Modifiers.Has(Modifiers.PersistAfterTrigger);

    public bool HadNaturalTimerFalloff()
        => ExpiresAt - Utils.Time <= 0 && !ApplyChain && !ClickedOff;

    public bool IsNull()
        => Applier is null || Description is null || Title is null;

    // Revise this, it is messy.
    public bool IsValid(out string error)
    {
        if(IconID is 0 or < 100000)
        {
            error = ("Invalid Icon");
            return false;
        }
        else if (Title.Length == 0)
        {
            error = ("Title is not set");
            return false;
        }
        else if (TotalMilliseconds < 1 && !NoExpire)
        {
            error = ("Duration is not set");
            return false;
        }
        // Otherwise, run a check on the title and description.
        var title = Utils.ParseBBSeString(Title, out bool hadError);
        if (hadError)
        {
            error = $"Syntax error in title: {title.TextValue}";
            return false;
        }
        var desc = Utils.ParseBBSeString(Description, out hadError);
        if (hadError)
        {
            error = $"Syntax error in description: {desc.TextValue}";
            return false;
        }
        error = null!;
        return true;
    }

    public LociStatusInfo ToTuple()
        => new LociStatusInfo
        {
            Version = Version,
            GUID = GUID,
            IconID = IconID,
            Title = Title,
            Description = Description,
            CustomVFXPath = CustomFXPath,
            ExpireTicks = NoExpire ? -1 : TotalMilliseconds,
            Type = Type,
            Stacks = Stacks,
            StackSteps = StackSteps,
            StackToChain = StackToChain,
            Modifiers = (uint)Modifiers,
            ChainedGUID = ChainedGUID,
            ChainType = ChainedType,
            ChainTrigger = ChainTrigger,
            Applier = Applier,
            Dispeller = Dispeller,
        };

    public static LociStatus FromTuple(LociStatusInfo statusInfo)
    {
        var totalTime = statusInfo.ExpireTicks == -1 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(statusInfo.ExpireTicks);
        return new LociStatus
        {
            GUID = statusInfo.GUID,
            IconID = statusInfo.IconID,
            Title = statusInfo.Title,
            Description = statusInfo.Description,
            CustomFXPath = statusInfo.CustomVFXPath,

            Type = statusInfo.Type,
            Stacks = statusInfo.Stacks,
            StackSteps = statusInfo.StackSteps,
            StackToChain = statusInfo.StackToChain,
            Modifiers = (Modifiers)statusInfo.Modifiers,

            ChainedGUID = statusInfo.ChainedGUID,
            ChainedType = statusInfo.ChainType,
            ChainTrigger = statusInfo.ChainTrigger,

            Applier = statusInfo.Applier,
            Dispeller = statusInfo.Dispeller,

            // Additional variables we can run assumptions on.
            Days = totalTime.Days,
            Hours = totalTime.Hours,
            Minutes = totalTime.Minutes,
            Seconds = totalTime.Seconds,
            NoExpire = statusInfo.ExpireTicks == -1,
        };
    }

    public string ReportString()
        => $"[LociStatus: GUID={GUID}," +
        $"\nIconID={IconID}" +
        $"\nTitle={Title}" +
        $"\nDescription={Description}" +
        $"\nCustomFXPath={CustomFXPath}" +
        $"\nExpiresAt={ExpiresAt}" +
        $"\nType={Type}" +
        $"\nModifiers={Modifiers}" +
        $"\nStacks={Stacks}" +
        $"\nStackSteps={StackSteps}" +
        $"\nChainedStatus={ChainedGUID}" +
        $"\nChainTrigger={ChainTrigger}" +
        $"\nApplier={Applier}" +
        $"\nDispeller={Dispeller}]";
}
