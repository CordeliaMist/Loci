namespace Loci.Services.Mediator;

// If the call should occur in the same thread it was made in or not.
public abstract record MessageBase
{
    public virtual bool KeepThreadContext => false;
}

public record SameThreadMessage : MessageBase
{
    public override bool KeepThreadContext => true;
}
