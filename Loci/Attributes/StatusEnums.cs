using System.Runtime.CompilerServices;

namespace Loci;

// Byte should be the preferred version.
public enum StatusType : int
{
    Positive,
    Negative,
    Special
}

/// <summary>
///     From Loci
/// </summary>
[Flags]
public enum Modifiers : uint // use uint to allow for futureproof options.
{
    None = 0,
    CanDispel           = 1u << 0, // Can be dispelled.
    StacksIncrease      = 1u << 1, // Stackable statuses, when reapplied, can increase their stack count.
    StacksRollOver      = 1u << 2, // When a stack reaches its cap, it starts over and counts up again.
    PersistExpireTime   = 1u << 3, // When reapplied, the expire time remains the same.
    StacksMoveToChain   = 1u << 4, // When a ChainStatus trigger occurs, the current stacks are is carried over.
    StacksCarryToChain  = 1u << 5, // When the stacks increase and hit max, remaining stacks carry over.
    PersistAfterTrigger = 1u << 6, // When a ChainStatus trigger occurs, the original status remains.
    // Ideas: Persist original after chain trigger, ext.. 
}

public enum ChainType : byte
{
    Status = 0,
    Preset = 1,
    // Event = 2, // Future application.
}

// What must occur for a chained status to trigger.
// Could be expanded upon to be caused by many things.
public enum ChainTrigger : int
{
    Dispel = 0,
    HitMaxStacks = 1,
    TimerExpired = 2,
    HitSetStacks = 3
}

// Bitwise operation help for Status Modifiers.
public static class ModifierExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this Modifiers value, Modifiers flag)
        => (value & flag) == flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAny(this Modifiers value, Modifiers flags)
        => (value & flags) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(ref this Modifiers value, Modifiers flag, bool enabled)
    {
        if (enabled) value |= flag;
        else value &= ~flag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(ref this Modifiers value, Modifiers flag)
        => value &= ~flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Toggle(ref this Modifiers value, Modifiers flag)
        => value ^= flag;
}

