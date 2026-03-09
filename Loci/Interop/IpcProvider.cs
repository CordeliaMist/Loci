using CkCommons;
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using Microsoft.Extensions.Hosting;

namespace Loci.Interop;

/// <summary>
///     The IPC Provider for Loci to interact with other plugins <para />
///     It is probably best to move Loci to a seperate provider and host two different providers.
/// </summary>
public class IpcProvider : DisposableMediatorSubscriberBase, IHostedService
{
    private const int LociApiVersion = 2;

    private readonly LociManager _manager;
    private readonly CharaWatcher _watcher;

    // Loci's Personal IPC Events.
    private static ICallGateProvider<int>    ApiVersion;
    private static ICallGateProvider<object> Ready;
    private static ICallGateProvider<object> Disposing;

    // Loci Events
    private static ICallGateProvider<nint, object>       OnManagerModified; // Whenever a players status manager changes in any way.
    private static ICallGateProvider<Guid, bool, object> OnStatusUpdated;   // When a loci statuses changes.
    private static ICallGateProvider<Guid, bool, object> OnPresetUpdated;   // When a loci presets changes.
    
    /// <summary>
    ///     Fired when used on a player that is part of a registered character. <para />
    ///     Registered characters are monitored by other plugins, so target application should be approved. <para />
    ///     If they are approved, the other plugin can then apply this to the target.
    /// </summary>
    private static ICallGateProvider<nint, string, LociStatusInfo, object>       OnTargetApplyStatus;
    private static ICallGateProvider<nint, string, List<LociStatusInfo>, object> OnTargetApplyStatuses;

    // ------ Actor SM Control -----
    // Inform you are managing this actor via an identifier. Returns if successful.
    // Note this has no impact on access, but informs if it should be ephemeral.
    // You are responsible for ensuring they are unregistered when done so target application can work properly.
    private static ICallGateProvider<nint, string, bool>?   RegisterActorByPtr;     // Mark an actor for use by pointer using an identification code.
    private static ICallGateProvider<string, string, bool>? RegisterActorByName;    // Mark an actor for use by name using an identification code.
    private static ICallGateProvider<nint, string, bool>?   UnregisterActorByPtr;   // Unmark an actor by pointer.
    private static ICallGateProvider<string, string, bool>? UnregisterActorByName;  // Unmark an actor by name.

    // Locks a Status with a code.
    // The status cannot be removed unless unlocked with the same code (or plugin disable)
    // Can only be applied to the client player statuses.
    private static ICallGateProvider<Guid, uint, bool>?                     LockStatus;     // Returns if locked successfully.
    private static ICallGateProvider<List<Guid>, uint, (bool, List<Guid>)>? LockStatuses;   // Returns if all were locked, and which weren't.
    private static ICallGateProvider<Guid, uint, bool>?                     UnlockStatus;   // Returns if unlocked successfully.
    private static ICallGateProvider<List<Guid>, uint, (bool, List<Guid>)>? UnlockStatuses; // Rets if any were unlocked, and which weren't.
    private static ICallGateProvider<uint, bool>?                           ClearLocks;     // Equivalent of Glamourer.UnlockState

    // -------- Loci Status Managers --------
    private static ICallGateProvider<string>?           GetOwnManager;    // No Arg | Return base64 string
    private static ICallGateProvider<nint, string>?     GetManagerByPtr;  // nint Arg | Return base64 string
    private static ICallGateProvider<string, string>?   GetManagerByName; // string Arg | Return base64 string
    private static ICallGateProvider<List<LociStatusInfo>>?         GetOwnManagerInfo;      // No Arg | Return List<LociStatusInfo>
    private static ICallGateProvider<nint, List<LociStatusInfo>>?   GetManagerInfoByPtr;    // nint Arg | Return List<LociStatusInfo>
    private static ICallGateProvider<string, List<LociStatusInfo>>? GetManagerInfoByName;   // string Arg | Return List<LociStatusInfo>

    private static ICallGateProvider<string, object>?         SetOwnManager;      // Update client's LociSM.
    private static ICallGateProvider<nint, string, object>?   SetManagerByPtr;    // Update a LociSM by address.
    private static ICallGateProvider<string, string, object>? SetManagerByName;   // Update a LociSM by name.

    private static ICallGateProvider<object>?         ClearOwnManager;    // Clear client's LociSM
    private static ICallGateProvider<nint, object>?   ClearManagerByPtr;  // Clear a LociSM by address
    private static ICallGateProvider<string, object>? ClearManagerByName; // Clear a LociSM by name

    // ------ Loci Data Aquision ------
    private static ICallGateProvider<Guid, LociStatusInfo>? GetStatusInfo;    // Get the tuple information of a status by GUID.
    private static ICallGateProvider<List<LociStatusInfo>>? GetAllStatusInfo; // Get all tuple info of stored statuses.
    private static ICallGateProvider<Guid, LociPresetInfo>? GetPresetInfo;    // Get the tuple information of a preset by GUID.
    private static ICallGateProvider<List<LociPresetInfo>>? GetAllPresetInfo; // Get all tuple info of stored presets.

    // ------ Loci Application ------
    // Calls unique to the ClientPlayer (Locking, Tuple application ext)
    private static ICallGateProvider<Guid, object>?           ApplyStatus;        // Apply a LociStatus to client by GUID.
    private static ICallGateProvider<Guid, uint, bool>?       ApplyLockedStatus;  // Apply a LociStatus to client by GUID, then lock with a code. Ret if lock worked.
    private static ICallGateProvider<List<Guid>, object>?     ApplyStatuses;      // bulk LociStatus application to Client.
    private static ICallGateProvider<List<Guid>, uint, bool>? ApplyLockedStatuses;// bulk LociStatus application to Client with lock. Returns which were locked successfully.
    
    private static ICallGateProvider<LociStatusInfo, object>?           ApplyStatusInfo;        // Apply using Tuple format.
    private static ICallGateProvider<LociStatusInfo, uint, bool>?       ApplyLockedStatusInfo;  // Apply using Tuple format with lock. Ret if lock worked.
    private static ICallGateProvider<List<LociStatusInfo>, object>?     ApplyStatusInfos;       // bulk LociStatus application using Tuple format to Client.
    private static ICallGateProvider<List<LociStatusInfo>, uint, bool>? ApplyLockedStatusInfos; // bulk LociStatus application using Tuple format to Client with lock. Returns which were locked successfully.

    // Normal Status application by GUID (single and bulk)
    private static ICallGateProvider<Guid, nint, object>?         ApplyStatusByPtr;    // Apply a LociStatus to a target by GUID via pointer.
    private static ICallGateProvider<List<Guid>, nint, object>?   ApplyStatusesByPtr;  // Bulk apply LociStatuses to a target by GUID via pointer.
    private static ICallGateProvider<Guid, string, object>?       ApplyStatusByName;   // Apply a LociStatus to a target by GUID via name.
    private static ICallGateProvider<List<Guid>, string, object>? ApplyStatusesByName; // Bulk apply LociStatuses to a target by GUID via name.

    private static ICallGateProvider<Guid, nint, object>?         ApplyPresetByPtr;   // Applies a LociPreset to a target by GUID via pointer.
    private static ICallGateProvider<Guid, string, object>?       ApplyPresetByName;  // Applies a LociPreset to a target by GUID via name.
    private static ICallGateProvider<List<Guid>, nint, object>?   ApplyPresetsByPtr;  // Bulk applies LociPresets to a target by GUID via pointer.
    private static ICallGateProvider<List<Guid>, string, object>? ApplyPresetsByName; // Bulk applies a LociPreset to a target by GUID via name.

    // Removal calls
    private static ICallGateProvider<Guid, bool>?                 RemoveStatus;         // Remove a lociStatus from the client by GUID. Returns if it worked.
    private static ICallGateProvider<List<Guid>, object>?         RemoveStatuses;       // Remove statuses in bulk. No return value.
    private static ICallGateProvider<Guid, nint, bool>?           RemoveStatusByPtr;    // Remove a lociStatus from a target. Returns if it worked.
    private static ICallGateProvider<List<Guid>, nint, object>?   RemoveStatusesByPtr;  // Bulk remove statuses from a target by pointer. No return value.
    private static ICallGateProvider<Guid, string, bool>?         RemoveStatusByName;   // Remove a lociStatus from a target by name. Returns if it worked.
    private static ICallGateProvider<List<Guid>, string, object>? RemoveStatusesByName; // Bulk remove statuses from a target by name. No return value.

    internal static event Action<nint> OnSMModifiedCalled;
    internal static event Action<LociStatus, bool> OnStatusModifiedCalled;
    internal static event Action<LociPreset, bool> OnPresetModifiedCalled;
    internal static event Action<nint, string, LociStatusInfo> OnApplyToTargetCalled;
    internal static event Action<nint, string, List<LociStatusInfo>> OnApplyToTargetBulkCalled;

    // Internal events for monitoring (Prevents ICallGate overhead for internal updates only.
    public IpcProvider(ILogger<IpcProvider> logger, LociMediator mediator,
        LociManager manager, CharaWatcher watcher)
        : base(logger, mediator)
    {
        _manager = manager;
        _watcher = watcher;
        Init();
        Logger.LogInformation("Started IpcProviderLoci");
    }

    public static void OnSMModified(nint charaAddr)
    {
        try
        {
            OnSMModifiedCalled?.Invoke(charaAddr);
            OnManagerModified?.SendMessage(charaAddr);
            // IpcProviderMoodles.InvokeManagerModified(charaAddr);
        }
        catch (Bagagwa ex)
        {
            Svc.Logger.Warning($"Failed to call OnManagerModified for {charaAddr:X}. Exception: {ex.Message}");
        }
    }

    public static void OnStatusModified(LociStatus status, bool removed)
    {
        try
        {
            OnStatusModifiedCalled?.Invoke(status, removed);
            OnStatusUpdated?.SendMessage(status.GUID, removed);
            // IpcProviderMoodles.InvokeStatusUpdated(status.GUID, removed);
        }
        catch (Bagagwa ex)
        {
            Svc.Logger.Warning($"Failed to call OnStatusUpdated for {status.Title}. Exception: {ex.Message}");
        }
    }

    public static void OnPresetModified(LociPreset preset, bool removed)
    {
        try
        {
            OnPresetModifiedCalled?.Invoke(preset, removed);
            OnPresetUpdated?.SendMessage(preset.GUID, removed);
            // IpcProviderMoodles.InvokePresetUpdated(preset.GUID, removed);
        }
        catch (Bagagwa ex)
        {
            Svc.Logger.Warning($"Failed to call OnPresetUpdated for {preset.Title}. Exception: {ex.Message}");
        }
    }

    public static void OnApplyToTarget(nint targetAddr, string host, LociStatusInfo status)
    {
        try
        {
            OnTargetApplyStatus?.SendMessage(targetAddr, host, status);
            OnApplyToTargetCalled?.Invoke(targetAddr, host, status);
        }
        catch (Bagagwa ex)
        {
            Svc.Logger.Warning($"Failed to call OnTargetApplyStatus for {targetAddr:X}. Exception: {ex.Message}");
        }
    }

    public static void OnApplyToTarget(nint targetAddr, string host, List<LociStatusInfo> statuses)
    {
        try
        {
            OnTargetApplyStatuses?.SendMessage(targetAddr, host, statuses);
            OnApplyToTargetBulkCalled?.Invoke(targetAddr, host, statuses);
        }
        catch (Bagagwa ex)
        {
            Svc.Logger.Warning($"Failed to call OnTargetApplyStatuses for {targetAddr:X}. Exception: {ex.Message}");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Generic.Safe(() => Ready?.SendMessage());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Stopping IpcProvider Service");
        Disposing?.SendMessage();
        Deinit();
        return Task.CompletedTask;
    }

    private void Init()
    {
        ApiVersion = Svc.PluginInterface.GetIpcProvider<int>("Loci.GetApiVersion");
        Ready = Svc.PluginInterface.GetIpcProvider<object>("Loci.Ready");
        Disposing = Svc.PluginInterface.GetIpcProvider<object>("Loci.Disposing");
        // Configure Funcs and Actions
        ApiVersion.RegisterFunc(() => LociApiVersion);

        // Events triggered by Loci.
        OnManagerModified = Svc.PluginInterface.GetIpcProvider<nint, object>("Loci.OnManagerModified");
        OnStatusUpdated = Svc.PluginInterface.GetIpcProvider<Guid, bool, object>("Loci.OnStatusUpdated");
        OnPresetUpdated = Svc.PluginInterface.GetIpcProvider<Guid, bool, object>("Loci.OnPresetUpdated");
        OnTargetApplyStatus = Svc.PluginInterface.GetIpcProvider<nint, string, LociStatusInfo, object>("Loci.OnTargetApplyStatus");
        OnTargetApplyStatuses = Svc.PluginInterface.GetIpcProvider<nint, string, List<LociStatusInfo>, object>("Loci.OnTargetApplyStatus");

        // SM Control
        RegisterActorByPtr = Svc.PluginInterface.GetIpcProvider<nint, string, bool>("Loci.RegisterActorByPtr");
        RegisterActorByName = Svc.PluginInterface.GetIpcProvider<string, string, bool>("Loci.RegisterActorByName");
        UnregisterActorByPtr = Svc.PluginInterface.GetIpcProvider<nint, string, bool>("Loci.UnregisterActorByPtr");
        UnregisterActorByName = Svc.PluginInterface.GetIpcProvider<string, string, bool>("Loci.UnregisterActorByName");
        RegisterActorByPtr.RegisterFunc(RegisterByPtr);
        RegisterActorByName.RegisterFunc(RegisterByName);
        UnregisterActorByPtr.RegisterFunc(UnregisterByPtr);
        UnregisterActorByName.RegisterFunc(UnregisterByName);

        // Locking
        LockStatus = Svc.PluginInterface.GetIpcProvider<Guid, uint, bool>("Loci.LockStatus");
        LockStatuses = Svc.PluginInterface.GetIpcProvider<List<Guid>, uint, (bool, List<Guid>)>("Loci.LockStatuses");
        UnlockStatus = Svc.PluginInterface.GetIpcProvider<Guid, uint, bool>("Loci.UnlockStatus");
        UnlockStatuses = Svc.PluginInterface.GetIpcProvider<List<Guid>, uint, (bool, List<Guid>)>("Loci.UnlockStatuses");
        ClearLocks = Svc.PluginInterface.GetIpcProvider<uint, bool>("Loci.ClearLocks");
        LockStatus.RegisterFunc(LockStatusSingle);
        LockStatuses.RegisterFunc(LockStatusesBulk);
        UnlockStatus.RegisterFunc(UnlockStatusSingle);
        UnlockStatuses.RegisterFunc(UnlockStatusesBulk);
        ClearLocks.RegisterFunc(ClearMatchingLocks);

        // SM Logic
        GetOwnManager = Svc.PluginInterface.GetIpcProvider<string>("Loci.GetOwnManager");
        GetManagerByPtr = Svc.PluginInterface.GetIpcProvider<nint, string>("Loci.GetManagerByPtr");
        GetManagerByName = Svc.PluginInterface.GetIpcProvider<string, string>("Loci.GetManagerByName");
        GetOwnManager.RegisterFunc(GetClientSM);
        GetManagerByPtr.RegisterFunc(GetSMByPtr);
        GetManagerByName.RegisterFunc(GetSMByName);

        GetOwnManagerInfo = Svc.PluginInterface.GetIpcProvider<List<LociStatusInfo>>("Loci.GetOwnManagerInfo");
        GetManagerInfoByPtr = Svc.PluginInterface.GetIpcProvider<nint, List<LociStatusInfo>>("Loci.GetManagerInfoByPtr");
        GetManagerInfoByName = Svc.PluginInterface.GetIpcProvider<string, List<LociStatusInfo>>("Loci.GetManagerInfoByName");
        GetOwnManagerInfo.RegisterFunc(GetOwnSMInfo);
        GetManagerInfoByPtr.RegisterFunc(GetSMInfoByPtr);
        GetManagerInfoByName.RegisterFunc(GetSMInfoByName);

        SetOwnManager = Svc.PluginInterface.GetIpcProvider<string, object>("Loci.SetOwnManager");
        SetManagerByPtr = Svc.PluginInterface.GetIpcProvider<nint, string, object>("Loci.SetManagerByPtr");
        SetManagerByName = Svc.PluginInterface.GetIpcProvider<string, string, object>("Loci.SetManagerByName");
        SetOwnManager.RegisterAction(SetOwnSM);
        SetManagerByPtr.RegisterAction(SetSMByPtr);
        SetManagerByName.RegisterAction(SetSMByName);

        ClearOwnManager = Svc.PluginInterface.GetIpcProvider<object>("Loci.ClearOwnManager");
        ClearManagerByPtr = Svc.PluginInterface.GetIpcProvider<nint, object>("Loci.ClearManagerByPtr");
        ClearManagerByName = Svc.PluginInterface.GetIpcProvider<string, object>("Loci.ClearManagerByName");
        ClearOwnManager.RegisterAction(ClearOwnSM);
        ClearManagerByPtr.RegisterAction(ClearSMByPtr);
        ClearManagerByName.RegisterAction(ClearSMByName);
        // Data Aquision
        GetStatusInfo = Svc.PluginInterface.GetIpcProvider<Guid, LociStatusInfo>("Loci.GetStatusInfo");
        GetAllStatusInfo = Svc.PluginInterface.GetIpcProvider<List<LociStatusInfo>>("Loci.GetAllStatusInfo");
        GetPresetInfo = Svc.PluginInterface.GetIpcProvider<Guid, LociPresetInfo>("Loci.GetPresetInfo");
        GetAllPresetInfo = Svc.PluginInterface.GetIpcProvider<List<LociPresetInfo>>("Loci.GetAllPresetInfo");
        GetStatusInfo.RegisterFunc(id => LociData.Statuses.FirstOrDefault(s => s.GUID == id) is { } match ? match.ToTuple() : default);
        GetAllStatusInfo.RegisterFunc(() => LociData.Statuses.Select(s => s.ToTuple()).ToList());
        GetPresetInfo.RegisterFunc(id => LociData.Presets.FirstOrDefault(p => p.GUID == id) is { } match ? match.ToTuple() : default);
        GetAllPresetInfo.RegisterFunc(() => LociData.Presets.Select(p => p.ToTuple()).ToList());

        // Client Application
        ApplyStatus = Svc.PluginInterface.GetIpcProvider<Guid, object>("Loci.ApplyStatus");
        ApplyLockedStatus = Svc.PluginInterface.GetIpcProvider<Guid, uint, bool>("Loci.ApplyLockedStatus");
        ApplyStatuses = Svc.PluginInterface.GetIpcProvider<List<Guid>, object>("Loci.ApplyStatuses");
        ApplyLockedStatuses = Svc.PluginInterface.GetIpcProvider<List<Guid>, uint, bool>("Loci.ApplyLockedStatuses");
        ApplyStatus.RegisterAction(ApplySingleStatus);
        ApplyLockedStatus.RegisterFunc(ApplySingleLockedStatus);
        ApplyStatuses.RegisterAction(ApplyBulkStatuses);
        ApplyLockedStatuses.RegisterFunc(ApplyBulkLockedStatuses);

        ApplyStatusInfo = Svc.PluginInterface.GetIpcProvider<LociStatusInfo, object>("Loci.ApplyStatusInfo");
        ApplyLockedStatusInfo = Svc.PluginInterface.GetIpcProvider<LociStatusInfo, uint, bool>("Loci.ApplyLockedStatusInfo");
        ApplyStatusInfos = Svc.PluginInterface.GetIpcProvider<List<LociStatusInfo>, object>("Loci.ApplyStatusInfos");
        ApplyLockedStatusInfos = Svc.PluginInterface.GetIpcProvider<List<LociStatusInfo>, uint, bool>("Loci.ApplyLockedStatusInfos");
        ApplyStatusInfo.RegisterAction(ApplySingleStatusInfo);
        ApplyLockedStatusInfo.RegisterFunc(ApplySingleLockedStatusInfo);
        ApplyStatusInfos.RegisterAction(ApplyBulkStatusInfos);
        ApplyLockedStatusInfos.RegisterFunc(ApplyBulkLockedStatusInfos);
        // Normal Application
        ApplyStatusByPtr = Svc.PluginInterface.GetIpcProvider<Guid, nint, object>("Loci.ApplyStatusByPtr");
        ApplyStatusesByPtr = Svc.PluginInterface.GetIpcProvider<List<Guid>, nint, object>("Loci.ApplyStatusesByPtr");
        ApplyStatusByName = Svc.PluginInterface.GetIpcProvider<Guid, string, object>("Loci.ApplyStatusByName");
        ApplyStatusesByName = Svc.PluginInterface.GetIpcProvider<List<Guid>, string, object>("Loci.ApplyStatusesByName");
        ApplyStatusByPtr.RegisterAction(ApplySingleStatusByPtr);
        ApplyStatusByName.RegisterAction(ApplySingleStatusByName);
        ApplyStatusesByPtr.RegisterAction(ApplyBulkStatusesByPtr);
        ApplyStatusesByName.RegisterAction(ApplyBulkStatusesByName);

        // Normal Preset Application
        ApplyPresetByPtr = Svc.PluginInterface.GetIpcProvider<Guid, nint, object>("Loci.ApplyPresetByPtr");
        ApplyPresetByName = Svc.PluginInterface.GetIpcProvider<Guid, string, object>("Loci.ApplyPresetByName");
        ApplyPresetsByPtr = Svc.PluginInterface.GetIpcProvider<List<Guid>, nint, object>("Loci.ApplyPresetsByPtr");
        ApplyPresetsByName = Svc.PluginInterface.GetIpcProvider<List<Guid>, string, object>("Loci.ApplyPresetsByName");
        ApplyPresetByPtr.RegisterAction(ApplySinglePresetByPtr);
        ApplyPresetByName.RegisterAction(ApplySinglePresetByName);
        ApplyPresetsByPtr.RegisterAction(ApplyBulkPresetsByPtr);
        ApplyPresetsByName.RegisterAction(ApplyBulkPresetsByName);

        // Removal
        RemoveStatus = Svc.PluginInterface.GetIpcProvider<Guid, bool>("Loci.RemoveStatus");
        RemoveStatusByPtr = Svc.PluginInterface.GetIpcProvider<Guid, nint, bool>("Loci.RemoveStatusByPtr");
        RemoveStatusByName = Svc.PluginInterface.GetIpcProvider<Guid, string, bool>("Loci.RemoveStatusByName");
        RemoveStatuses = Svc.PluginInterface.GetIpcProvider<List<Guid>, object>("Loci.RemoveStatuses");
        RemoveStatusesByPtr = Svc.PluginInterface.GetIpcProvider<List<Guid>, nint, object>("Loci.RemoveStatusesByPtr");
        RemoveStatusesByName = Svc.PluginInterface.GetIpcProvider<List<Guid>, string, object>("Loci.RemoveStatusesByName");
        RemoveStatus.RegisterFunc(RemoveSingleStatus);
        RemoveStatusByPtr.RegisterFunc(RemoveSingleStatusByPtr);
        RemoveStatusByName.RegisterFunc(RemoveSingleStatusByName);
        RemoveStatuses.RegisterAction(RemoveBulkStatuses);
        RemoveStatusesByPtr.RegisterAction(RemoveBulkStatusesByPtr);
        RemoveStatusesByName.RegisterAction(RemoveBulkStatusesByName);
    }

    private void Deinit()
    {
        ApiVersion?.UnregisterFunc();

        RegisterActorByPtr?.UnregisterFunc();
        RegisterActorByName?.UnregisterFunc();
        UnregisterActorByPtr?.UnregisterFunc();
        UnregisterActorByName?.UnregisterFunc();
        // Locking
        LockStatus?.UnregisterFunc();
        LockStatuses?.UnregisterFunc();
        UnlockStatus?.UnregisterFunc();
        UnlockStatuses?.UnregisterFunc();
        ClearLocks?.UnregisterFunc();
        // SM Logic
        GetOwnManager?.UnregisterFunc();
        GetManagerByPtr?.UnregisterFunc();
        GetManagerByName?.UnregisterFunc();

        GetOwnManagerInfo?.UnregisterFunc();
        GetManagerInfoByPtr?.UnregisterFunc();
        GetManagerInfoByName?.UnregisterFunc();

        SetOwnManager?.UnregisterAction();
        SetManagerByPtr?.UnregisterAction();
        SetManagerByName?.UnregisterAction();

        ClearOwnManager?.UnregisterAction();
        ClearManagerByPtr?.UnregisterAction();
        ClearManagerByName?.UnregisterAction();
        // Data Aquision
        GetStatusInfo?.UnregisterFunc();
        GetAllStatusInfo?.UnregisterFunc();
        GetPresetInfo?.UnregisterFunc();
        GetAllPresetInfo?.UnregisterFunc();
        // Client Application
        ApplyStatus?.UnregisterAction();
        ApplyLockedStatus?.UnregisterFunc();
        ApplyStatuses?.UnregisterAction();
        ApplyLockedStatuses?.UnregisterFunc();

        ApplyStatusInfo?.UnregisterAction();
        ApplyLockedStatusInfo?.UnregisterFunc();
        ApplyStatusInfos?.UnregisterAction();
        ApplyLockedStatusInfos?.UnregisterFunc();
        // Normal Application
        ApplyStatusByPtr?.UnregisterAction();
        ApplyStatusByName?.UnregisterAction();
        ApplyStatusesByPtr?.UnregisterAction();
        ApplyStatusesByName?.UnregisterAction();

        // Normal Preset Application
        ApplyPresetByPtr?.UnregisterAction();
        ApplyPresetByName?.UnregisterAction();
        ApplyPresetsByPtr?.UnregisterAction();
        ApplyPresetsByName?.UnregisterAction();

        // Removal
        RemoveStatus?.UnregisterFunc();
        RemoveStatusByPtr?.UnregisterFunc();
        RemoveStatusByName?.UnregisterFunc();
        RemoveStatuses?.UnregisterAction();
        RemoveStatusesByPtr?.UnregisterAction();
        RemoveStatusesByName?.UnregisterAction();
    }

    internal bool RegisterByPtr(nint ptr, string id) => CharaWatcher.Rendered.Contains(ptr) ? RegisterInternal(ptr, id) : false;
    internal bool RegisterByName(string name, string id) => CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr) ? RegisterInternal(addr, id) : false;
    internal unsafe bool RegisterInternal(nint charaAddr, string identifier)
    {
        Character* chara = (Character*)charaAddr;
        return chara is null ? false : _manager.AttachIdToActor(chara->GetNameWithWorld(), identifier);
    }

    internal bool UnregisterByPtr(nint ptr, string id) => CharaWatcher.Rendered.Contains(ptr) ? UnregisterInternal(ptr, id) : false;
    internal bool UnregisterByName(string name, string id) => CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr) ? UnregisterInternal(addr, id) : false;
    internal unsafe bool UnregisterInternal(nint charaAddr, string identifier)
    {
        Character* chara = (Character*)charaAddr;
        return chara is null ? false : _manager.DetachIdFromActor(chara->GetNameWithWorld(), identifier);
    }

    internal unsafe bool LockStatusSingle(Guid id, uint key) => PlayerData.Available ? LociManager.GetFromChara(PlayerData.Character).LockStatus(id, key) : false;
    internal unsafe (bool, List<Guid>) LockStatusesBulk(List<Guid> ids, uint key) => PlayerData.Available ? LociManager.GetFromChara(PlayerData.Character).LockStatuses(ids, key) : (false, ids);
    internal unsafe bool UnlockStatusSingle(Guid id, uint key) => PlayerData.Available ? LociManager.GetFromChara(PlayerData.Character).UnlockStatus(id, key) : false;
    internal unsafe (bool, List<Guid>) UnlockStatusesBulk(List<Guid> ids, uint key) => PlayerData.Available ? LociManager.GetFromChara(PlayerData.Character).UnlockStatuses(ids, key) : (false, ids);
    internal unsafe bool ClearMatchingLocks(uint key) => PlayerData.Available ? LociManager.GetFromChara(PlayerData.Character).ClearLocks(key) : false;


    // Rets the Base64 encoded LociSM. Null if not rendered or accessible.
    internal string GetClientSM() => GetSMInternal(PlayerData.Address);
    internal string GetSMByPtr(nint addr) => CharaWatcher.Rendered.Contains(addr) ? GetSMInternal(addr) : null!;
    internal string GetSMByName(string name) => CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr) ? GetSMInternal(addr) : null!;
    internal unsafe string GetSMInternal(nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null)
        {
            Logger.LogWarning("GetLociManager addr is NULL");
            return null!;
        }
        return LociManager.GetFromChara(chara).ToBase64();
    }

    // Gets the LociSM of the player address in Tuple format. If not rendered, returns null.
    internal List<LociStatusInfo> GetOwnSMInfo() => GetSMInfoInternal(PlayerData.Address);
    internal List<LociStatusInfo> GetSMInfoByPtr(nint addr) => CharaWatcher.Rendered.Contains(addr) ? GetSMInfoInternal(addr) : new List<LociStatusInfo>();
    internal List<LociStatusInfo> GetSMInfoByName(string name) => CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr) ? GetSMInfoInternal(addr) : new List<LociStatusInfo>();
    internal unsafe List<LociStatusInfo> GetSMInfoInternal(nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        return chara is not null ? LociManager.GetFromChara(chara).GetStatusInfoList() : new List<LociStatusInfo>();
    }

    // Updates the LociSM with a provided base64 string. Fails silently if not rendered or accessible.
    internal void SetOwnSM(string newData) => SetSMInternal(PlayerData.Address, newData);
    internal void SetSMByPtr(nint ptr, string newData)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        SetSMInternal(ptr, newData);
    }
    internal void SetSMByName(string name, string newData)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        SetSMInternal(addr, newData);
    }
    internal unsafe void SetSMInternal(nint charaAddr, string newData)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is not null)
            LociManager.GetFromChara(chara).Apply(newData);
    }

    // Clears the encoded base64 data to a visible player's StatusManager, if rendered.
    internal void ClearOwnSM() => ClearSMInternal(PlayerData.Address);
    internal void ClearSMByPtr(nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ClearSMInternal(ptr);
    }
    internal void ClearSMByName(string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ClearSMInternal(addr);
    }
    internal unsafe void ClearSMInternal(nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null)
            return;
        // Grab the SM and cancel all non-persistent statuses that are not locked.
        var mySM = LociManager.GetFromChara(chara);
        // TODO: Make this filter out locked statuses!
        foreach (var s in mySM.Statuses)
            if (!s.Persistent)
                mySM.Cancel(s);
    }

    // Apply status by Guid, Normally.
    internal void ApplySingleStatus(Guid id) => ApplyStatusInternal(id, PlayerData.Address);
    internal bool ApplySingleLockedStatus(Guid id, uint key) => ApplyStatusInternal(id, PlayerData.Address, key);
    internal void ApplySingleStatusByPtr(Guid id, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyStatusInternal(id, ptr);
    }
    internal void ApplySingleStatusByName(Guid id, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyStatusInternal(id, addr);
    }
    internal unsafe bool ApplyStatusInternal(Guid id, nint charaAddr, uint lockKey = 0)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return false;

        if (LociData.Statuses.FirstOrDefault(x => x.GUID == id) is not { } status)
            return false;
        
        var sm = LociManager.GetFromChara(chara);
        if (!sm.Ephemeral)
        {
            Logger.LogDebug($"Adding or Updating Loci {status.Title} to {chara->GetNameWithWorld()}", LoggerType.IpcProvider);
            sm.AddOrUpdate(status.PreApply(), true, true, lockKey);
        }

        // Return if this status is locked with our key.
        return lockKey is 0 ? true : sm.LockedByKey(status.GUID, lockKey);
    }

    // Normal Apply Status by GUID. (Includes Bulk Version)
    internal void ApplyBulkStatuses(List<Guid> ids) => ApplyStatusesInternal(ids, PlayerData.Address);
    internal bool ApplyBulkLockedStatuses(List<Guid> ids, uint key) => ApplyStatusesInternal(ids, PlayerData.Address, key);
    internal void ApplyBulkStatusesByPtr(List<Guid> ids, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyStatusesInternal(ids, ptr);
    }
    internal void ApplyBulkStatusesByName(List<Guid> ids, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyStatusesInternal(ids, addr);
    }
    internal unsafe bool ApplyStatusesInternal(List<Guid> ids, nint charaAddr, uint lockKey = 0)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return false;

        var lookup = ids.ToHashSet();
        var toApply = LociData.Statuses.Where(s => lookup.Contains(s.GUID)).ToList();
        if (toApply.Count is 0) return false;

        // Grab the manager and run an add or update to them.
        var sm = LociManager.GetFromChara(chara);
        foreach (var status in toApply)
        {
            if (!sm.Ephemeral)
            {
                Logger.LogDebug($"Adding or Updating Loci {status.Title} to {chara->GetNameWithWorld()}", LoggerType.IpcProvider);
                sm.AddOrUpdate(status.PreApply(), true, true, lockKey);
            }
        }
        return lockKey is 0 ? true : sm.AnyLockedByKey(ids, lockKey);
    }

    internal void ApplySingleStatusInfo(LociStatusInfo info) => ApplyStatusInfoInternal(info, PlayerData.Address);
    internal bool ApplySingleLockedStatusInfo(LociStatusInfo info, uint key) => ApplyStatusInfoInternal(info, PlayerData.Address, key);
    internal void ApplySingleStatusInfoByPtr(LociStatusInfo info, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyStatusInfoInternal(info, ptr);
    }
    internal void ApplySingleStatusInfoByName(LociStatusInfo info, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyStatusInfoInternal(info, addr);
    }
    internal unsafe bool ApplyStatusInfoInternal(LociStatusInfo info, nint charaAddr, uint lockKey = 0)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return false;
        // Grab the manager and run an add or update to them.
        Logger.LogDebug($"Applying status: ({info.Title})");
        var sm = LociManager.GetFromChara(chara);
        sm.AddOrUpdate(LociStatus.FromTuple(info).PreApply(), true, true, lockKey);
        return true;
    }

    internal void ApplyBulkStatusInfos(List<LociStatusInfo> infos) => ApplyStatusInfosInternal(infos, PlayerData.Address);
    internal bool ApplyBulkLockedStatusInfos(List<LociStatusInfo> infos, uint key) => ApplyStatusInfosInternal(infos, PlayerData.Address, key);
    internal void ApplyBulkStatusInfosByPtr(List<LociStatusInfo> infos, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyStatusInfosInternal(infos, ptr);
    }
    internal void ApplyBulkStatusInfosByName(List<LociStatusInfo> infos, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyStatusInfosInternal(infos, addr);
    }
    internal unsafe bool ApplyStatusInfosInternal(List<LociStatusInfo> infos, nint charaAddr, uint lockKey = 0)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return false;
        // Grab the manager and run an add or update to them.
        Logger.LogDebug($"Applying statuses: ({string.Join(",", infos.Select(s => s.Title))})");
        var sm = LociManager.GetFromChara(chara);
        foreach (var statusInfo in infos)
            sm.AddOrUpdate(LociStatus.FromTuple(statusInfo).PreApply(), true, true, lockKey);
        // Ret the result.
        return lockKey is 0 ? true : sm.AnyLockedByKey(infos.Select(x => x.GUID), lockKey);
    }

    internal void ApplySinglePreset(Guid id) => ApplyPresetInternal(id, PlayerData.Address);
    internal void ApplySinglePresetByPtr(Guid id, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyPresetInternal(id, ptr);
    }
    internal void ApplySinglePresetByName(Guid id, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyPresetInternal(id, addr);
    }
    internal unsafe void ApplyPresetInternal(Guid id, nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null)
            return;
        if (LociData.Presets.FirstOrDefault(x => x.GUID == id) is not { } preset)
            return;
        // Grab the manager and run an add or update to them.
        var sm = LociManager.GetFromChara(chara);
        if (!sm.Ephemeral)
            sm.ApplyPreset(preset);
    }

    internal void ApplyBulkPresets(List<Guid> ids) => ApplyPresetsInternal(ids, PlayerData.Address);
    internal void ApplyBulkPresetsByPtr(List<Guid> ids, nint ptr)
    {
        if (!CharaWatcher.Rendered.Contains(ptr)) return;
        ApplyPresetsInternal(ids, ptr);
    }
    internal void ApplyBulkPresetsByName(List<Guid> ids, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        ApplyPresetsInternal(ids, addr);
    }
    internal unsafe void ApplyPresetsInternal(List<Guid> ids, nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return;
        var lookup = ids.ToHashSet();
        var toApply = LociData.Presets.Where(s => lookup.Contains(s.GUID)).ToList();
        if (toApply.Count is 0) return;
        // Grab the manager and run an add or update to them.
        var sm = LociManager.GetFromChara(chara);
        foreach (var preset in toApply)
        {
            if (!sm.Ephemeral)
                sm.ApplyPreset(preset);
        }
    }

    internal bool RemoveSingleStatus(Guid id) => RemoveStatusInternal(id, PlayerData.Address);
    internal bool RemoveSingleStatusByPtr(Guid id, nint addr) => CharaWatcher.Rendered.Contains(addr) && RemoveStatusInternal(id, addr);
    internal bool RemoveSingleStatusByName(Guid id, string name) => CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr) && RemoveStatusInternal(id, addr);
    internal unsafe bool RemoveStatusInternal(Guid id, nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return false;
        var sm = LociManager.GetFromChara(chara);

        if (sm.Statuses.FirstOrDefault(x => x.GUID == id) is not { } status)
            return false;
        // Remove if not ephemeral? (Prevent removing from controlled actors)
        if (!sm.Ephemeral && !status.Persistent)
            return sm.Cancel(id);

        return false;
    }

    internal void RemoveBulkStatuses(List<Guid> ids) => RemoveStatusesInternal(ids, PlayerData.Address);
    internal void RemoveBulkStatusesByPtr(List<Guid> ids, nint addr)
    {
        if (!CharaWatcher.Rendered.Contains(addr)) return;
        RemoveStatusesInternal(ids, addr);
    }
    internal void RemoveBulkStatusesByName(List<Guid> ids, string name)
    {
        if (!CharaWatcher.TryGetFirst(c => c.GetNameWithWorld() == name, out var addr)) return;
        RemoveStatusesInternal(ids, addr);
    }
    internal unsafe void RemoveStatusesInternal(List<Guid> ids, nint charaAddr)
    {
        Character* chara = (Character*)charaAddr;
        if (chara is null) return;
        var sm = LociManager.GetFromChara(chara);

        foreach (var guid in ids)
        {
            if (sm.Statuses.FirstOrDefault(x => x.GUID == guid) is not { } status)
                continue;
            // Remove if not ephemeral
            if (!sm.Ephemeral && !status.Persistent)
                sm.Cancel(guid);
        }
    }
}

