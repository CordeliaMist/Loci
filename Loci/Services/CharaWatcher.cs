using CkCommons;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Loci.Services.Mediator;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Loci.Services;

public unsafe class CharaWatcher : IHostedService
{
    public static class Delegates
    {
        public unsafe delegate void CharaInfo(Character* thisPtr, ObjectKind kind, string nameKey);
        public unsafe delegate bool CharaPtr(Character* thisPtr);
    }
    
    internal Hook<Character.Delegates.OnInitialize> OnCharaInitializeHook;
    internal Hook<Character.Delegates.Dtor> OnCharaDestroyHook;
    internal Hook<Character.Delegates.Terminate> OnCharaTerminateHook;

    private readonly ILogger<CharaWatcher> _logger;
    private readonly LociMediator _mediator;

    private static readonly CancellationTokenSource _runtimeCTS = new();

    public unsafe CharaWatcher(ILogger<CharaWatcher> logger, LociMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;

        OnCharaInitializeHook = Svc.Hook.HookFromAddress<Character.Delegates.OnInitialize>((nint)Character.StaticVirtualTablePointer->OnInitialize, InitializeCharacter);
        OnCharaTerminateHook = Svc.Hook.HookFromAddress<Character.Delegates.Terminate>((nint)Character.StaticVirtualTablePointer->Terminate, TerminateCharacter);
        OnCharaDestroyHook = Svc.Hook.HookFromAddress<Character.Delegates.Dtor>((nint)Character.StaticVirtualTablePointer->Dtor, DestroyCharacter);
        
        OnCharaInitializeHook.SafeEnable();
        OnCharaTerminateHook.SafeEnable();
        OnCharaDestroyHook.SafeEnable();
    }

    // A persistent static cache holding all rendered Character pointers.
    public static HashSet<nint> Rendered { get; private set; } = [];

    /// <summary>
    ///     The targeted player character. Null if not a player, or no target.
    /// </summary>
    public static Character* Target
    {
        get
        {
            var obj = TargetSystem.Instance()->GetTargetObject();
            return obj != null && obj->IsCharacter() ? (Character*)obj : null;
        }
    }

    // Hosted service and initialization stuffies.
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CollectInitialData();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _runtimeCTS.SafeCancel();
        // Remove the hooks
        OnCharaInitializeHook?.Dispose();
        OnCharaTerminateHook?.Dispose();
        OnCharaDestroyHook?.Dispose();
        // Clear the data
        Rendered.Clear();
        return Task.CompletedTask;
    }

    private unsafe void CollectInitialData()
    {
        var objManager = GameObjectManager.Instance();
        // Standard Actor Handling.
        for (var i = 0; i < 200; i++)
        {
            GameObject* obj = objManager->Objects.IndexSorted[i];
            if (obj is null) continue;
            // Only process characters.
            if (!obj->IsCharacter()) continue;

            Character* chara = (Character*)obj;
            // this is confusing because sometimes these can be either?
            if (chara->GetObjectKind() is (ObjectKind.Pc or ObjectKind.BattleNpc or ObjectKind.Companion))
                NewCharacterRendered(chara);
        }
    }

    public static bool TryGetFirst(Func<Character, bool> predicate, [NotNullWhen(true)] out nint charaAddr)
    {
        foreach (Character* addr in Rendered)
        {
            if (predicate(*addr))
            {
                charaAddr = (nint)addr;
                return true;
            }
        }
        charaAddr = nint.Zero;
        return false;
    }

    // Can make additional methods here to search by key as well most likely? Idk.
    public static unsafe bool TryGetFirstUnsafe(Delegates.CharaPtr predicate, [NotNullWhen(true)] out Character* character)
    {
        foreach (Character* addr in Rendered)
        {
            if (predicate(addr))
            {
                character = addr;
                return true;
            }
        }
        character = null;
        return false;
    }

    /// <summary>
    ///     Obtain a Character* if rendered, returning false otherwise.
    /// </summary>
    public static unsafe bool TryGetValue(nint address, [NotNullWhen(true)] out Character* character)
    {
        if (Rendered.Contains(address))
        {
            character = (Character*)address;
            return true;
        }
        character = null;
        return false;
    }

    /// <summary>
    ///     Entry point for initialized characters. Should interface with anything 
    ///     wishing to detect created objects. <para />
    ///     Doing so will ensure any final lines are processed prior to the address invalidating.
    /// </summary>
    private unsafe void NewCharacterRendered(Character* chara)
    {
        var address = (nint)chara;
        // Do not track if not a valid object type. (Maybe move to after gpose actor adding)
        if (chara->GetObjectKind() is not (ObjectKind.Pc or ObjectKind.BattleNpc or ObjectKind.Companion))
            return;
        // Other Actors
        _logger.LogDebug($"Character Rendered: {(nint)chara:X} - {chara->GetName()}", LoggerType.Objects);
        Rendered.Add(address);
        _mediator.Publish(new WatchedObjectCreated(address));
    }

    private unsafe void CharacterRemoved(Character* chara)
    {
        var address = (nint)chara;
        // Other Actors
        _logger.LogDebug($"Character Removed: {(nint)chara:X} - {chara->GetName()}", LoggerType.Objects);
        Rendered.Remove(address);
        _mediator.Publish(new WatchedObjectDestroyed(address));
    }

    public unsafe static string GetLociKey(Character* chara) => chara->ObjectKind switch
    {
        ObjectKind.Pc => chara->GetNameWithWorld(),
        ObjectKind.Companion => $"{((Companion*)chara)->Owner->NameString}'s {chara->NameString}",
        ObjectKind.BattleNpc => $"{chara->NameString} ({(nint)chara:X})", // Could be too vague, maybe another way to validate pets?
        _ => string.Empty
    };


    // Init with original first, than handle so it is present in our other lookups.
    private unsafe void InitializeCharacter(Character* chara)
    {
        try { OnCharaInitializeHook!.OriginalDisposeSafe(chara); }
        catch (Exception e) { _logger.LogError($"Error: {e}"); }
        Svc.Framework.Run(() => NewCharacterRendered(chara));
    }

    private unsafe void TerminateCharacter(Character* chara)
    {
        CharacterRemoved(chara);
        try { OnCharaTerminateHook!.OriginalDisposeSafe(chara); }
        catch (Exception e) { _logger.LogError($"Error: {e}"); }
    }

    private unsafe GameObject* DestroyCharacter(Character* chara, byte freeMemory)
    {
        CharacterRemoved(chara);
        try { return OnCharaDestroyHook!.OriginalDisposeSafe(chara, freeMemory); }
        catch (Exception e) { _logger.LogError($"Error: {e}"); return null; }
    }
}
