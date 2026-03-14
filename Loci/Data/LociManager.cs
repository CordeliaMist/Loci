using CkCommons;
using CkCommons.HybridSaver;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Loci.Services;
using Loci.Services.Mediator;

namespace Loci.Data;

// Could be considered a config file, but could also be split up.
// Effectively any stored status managers are handled here. One for each object type.
// Any manager with 0 statuses or that is ephemeral is not saved.
public sealed class LociManager : DisposableMediatorSubscriberBase, IHybridSavable
{
    private readonly MainConfig _config;
    private readonly FileProvider _fileNames;
    private readonly SaveService _saver;

    // Stores the Player dictionaries of ActorSM's.
    private static Dictionary<string, ActorSM> _managers = [];
    // Seperate lookup holding the pointer address lookup
    private static Dictionary<nint, ActorSM> _addressLookup = [];

    public LociManager(ILogger<LociManager> logger, LociMediator mediator,
        MainConfig config, FileProvider fileNames, SaveService saver)
        : base(logger, mediator)
    {
        _config = config;
        _fileNames = fileNames;
        _saver = saver;
        // Load the config and mark for save on disposal.
        Load();
        _saver.MarkForSaveOnDispose(this);
        // Process object creation here
        Mediator.Subscribe<WatchedObjectCreated>(this, _ => OnObjectCreated(_.Address));
        Mediator.Subscribe<WatchedObjectDestroyed>(this, _ => OnObjectDeleted(_.Address));
        Mediator.Subscribe<TerritoryChanged>(this, _ => OnTerritoryChange(_.PrevTerritory, _.NewTerritory));
        Svc.ClientState.Login += OnLogin;

        if (Svc.ClientState.IsLoggedIn)
            OnLogin();
    }

    // Statically stored manager of the Client for quick data retrieval.
    internal static ActorSM ClientSM = new ActorSM();

    // Move the below to non-static when possible and reallocate the LociProcess.cs data into a service or cache so it can be handled better.

    /// <summary>
    ///   All loaded StatusManagers in the cache, including ones for people not rendered.
    /// </summary>
    internal static IReadOnlyDictionary<string, ActorSM> Managers => _managers;

    /// <summary>
    ///   A lookup dictionary holding StatusManagers for all rendered actors.
    /// </summary>
    internal static IReadOnlyDictionary<nint, ActorSM> Rendered => _addressLookup;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Svc.ClientState.Login -= OnLogin;
    }

    private async void OnLogin()
    {
        // Wait for the player to be fully loaded in first.
        await Utils.WaitForPlayerLoading().ConfigureAwait(false);
        // Init data
        InitializeData();
    }

    /// <summary>
    ///   This occurs after the player is finished rendering.
    /// </summary>
    private void OnTerritoryChange(ushort prev, ushort next)
    {
        var clientNameWorld = PlayerData.NameWithWorld;
        // Clean up all non-client and non-ephemeral managers.
        foreach (var (name, lociSM) in _managers.ToList())
        {
            // If the manager is the clientSM, ignore it
            if (lociSM == ClientSM)
                continue;
            // If ephemeral or have a valid owner, ignore
            if (lociSM.Ephemeral || lociSM.OwnerValid)
                continue;
            // Otherwise, remove the manager
            _managers.Remove(name);
            // Dont need to remove the address lookup since the owner isnt valid.
        }

        Mediator.Publish(new FolderUpdateManagers());
    }

    /// <summary>
    ///   Initializes the data for the player and all currently rendered characters. 
    ///   Indended to only be called on login.
    /// </summary>
    private unsafe void InitializeData()
    {
        InitClientSM();
        // Then also do this for all other characters
        foreach (var charaAddr in CharaWatcher.Rendered.ToList())
        {
            var chara = (Character*)charaAddr;
            if (chara is null || !chara->IsCharacter() || chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
                continue;

            // Get their Penumbra.GameData object manager name
            var nameKey = Utils.ToLociName(chara);

            // If this name exists in the lookup for a cached manager, mark as visible.
            if (_managers.TryGetValue(nameKey, out var actorSM))
                MarkRendered(actorSM, chara);
            // otherwise, if the nameKey is not empty, create it as new
            else if (!string.IsNullOrEmpty(nameKey))
                AddManager(chara, nameKey);
        }
        // Update the draw folders
        Mediator.Publish(new FolderUpdateManagers());
    }

    private unsafe void MarkRendered(ActorSM actorSM, Character* chara, bool isClient = false)
    {
        actorSM.Owner = chara;
        _addressLookup[(nint)chara] = actorSM;
        Logger.LogTrace($"Updated {{{actorSM.Identifier}}} to Rendered (Visibile) state", LoggerType.Data);
        if (isClient)
        {
            ClientSM = actorSM;
            ClientSM.Owner = PlayerData.Character;
        }
        // Invoke that the manager changed on render to help DataSyncs detect initial display.
        actorSM.NeedFireEvent = true;
    }

    private unsafe void MarkUnrendered(ActorSM actorSM, Character* chara)
    {
        actorSM.Owner = null;
        _addressLookup.Remove((nint)chara);
        Logger.LogTrace($"Updated {{{actorSM.Identifier}}} to Unrendered (Invisible) state", LoggerType.Data);

        // If they are not ephemeral and have 0 statuses, we should remove them.
        if (actorSM != ClientSM && !actorSM.Ephemeral && actorSM.Statuses.Count == 0)
        {
            _managers.Remove(actorSM.Identifier);
            Logger.LogDebug($"Removed {actorSM.Identifier} because it was unrendered with 0 statuses and non-ephemeral", LoggerType.Data);
        }
    }

    private unsafe ActorSM AddManager(Character* chara, string nameKey, bool isClient = false)
    {
        var newSM = new ActorSM()
        {
            ActorKind = chara->ObjectKind,
            Identifier = nameKey,
            Owner = chara
        };
        _managers.TryAdd(nameKey, newSM);
        _addressLookup[(nint)chara] = newSM;
        Logger.LogTrace($"Created and Assigned {{{nameKey}}} to a new LociSM", LoggerType.Data);
        if (isClient)
            ClientSM = newSM;

        // Invoke that the manager changed on render to help DataSyncs detect initial display.
        newSM.NeedFireEvent = true;
        // return the created manager.
        return newSM;
    }

    private unsafe void InitClientSM()
    {
        var playerName = PlayerData.NameWithWorld;
        // If it exists, we need to ensure sync.
        if (_managers.TryGetValue(playerName, out var existingSM))
            MarkRendered(existingSM, PlayerData.Character, true);
        else
            AddManager(PlayerData.Character, playerName, true);
    }

    private unsafe void OnObjectCreated(IntPtr address)
    {
        var chara = (Character*)address;
        if (chara is null || chara->ObjectIndex >= 200 || !chara->IsCharacter() || chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        // Get the name data for the character.
        var nameKey = Utils.ToLociName(chara);
        if (string.IsNullOrEmpty(nameKey))
            return;

        // If it exists, update it to visible
        if (_managers.TryGetValue(nameKey, out var existingSM))
        {
            MarkRendered(existingSM, chara);
            Mediator.Publish(new FolderUpdateManagers());
        }
        // Otherwise, create a new manager if the nameKey is valid
        else if (!string.IsNullOrEmpty(nameKey))
        {
            AddManager(chara, nameKey);
            Mediator.Publish(new FolderUpdateManagers());
        }
    }

    private unsafe void OnObjectDeleted(IntPtr address)
    {
        var chara = (Character*)address;
        if (chara is null || chara->ObjectIndex >= 200 || !chara->IsCharacter() || chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        // Get the namekey, if this was empty, we never had it as a manager, so ignore.
        var nameKey = Utils.ToLociName(chara);
        if (string.IsNullOrEmpty(nameKey))
            return;

        // Otherwise, see if it exists, and if it does, we need to remove it.
        if (_managers.TryGetValue(nameKey, out var lociSM))
        {
            MarkUnrendered(lociSM, chara);
            Mediator.Publish(new FolderUpdateManagers());
        }
    }

    // Should no longer be needed, but also breaks current stuff.
    //public unsafe ActorSM GetFromName(string nameKey, bool create = true)
    //{
    //    if (!_managers.TryGetValue(nameKey, out var manager))
    //    {
    //        if (create)
    //        {
    //            manager = new();
    //            // Add it to the dictionary.
    //            _managers.TryAdd(nameKey, manager);
    //            // If we can identify the player from the object watcher, we should set it in the manager.
    //            if (CharaWatcher.TryGetFirstUnsafe(x => Utils.ToLociName(x) == nameKey, out var chara))
    //            {
    //                manager.ActorKind = chara->ObjectKind;
    //                manager.Identifier = nameKey;
    //                manager.Owner = chara;
    //            }
    //        }
    //    }
    //    return manager!;
    //}

    public unsafe ActorSM GetOrCreateSM(Character* chara, bool create = true)
    {
        var nameKey = Utils.ToLociName(chara);
        // Try pulling from the list of managers, even ones that are not rendered.
        // This allows them to be properly synced and updated.
        if (!_managers.TryGetValue(nameKey, out var manager) && create)
        {
            manager = AddManager(chara, nameKey);
            Mediator.Publish(new FolderUpdateManagers());
        }
        
        return manager!;
    }
    public void Save()
        => _saver.Save(this);

    #region HybridSavable
    public int ConfigVersion => 1;
    public HybridSaveType SaveType => HybridSaveType.Json;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(FileProvider files, out bool _) => (_ = false, files.ManagersConfig).Item2;
    public void WriteToStream(StreamWriter writer) => throw new NotImplementedException();
    public string JsonSerialize()
    {
        var filteredManagers = _managers
            .Where(x => !x.Value.Ephemeral && x.Value.Statuses.Count is not 0)
            .ToDictionary(x => x.Key, x => x.Value);
        // construct the config object to serialize.
        return new JObject()
        {
            ["Version"] = ConfigVersion,
            ["StatusManagers"] = JObject.FromObject(filteredManagers),
        }.ToString(Formatting.None); // No pretty formatting here.
    }

    public void Load()
    {
        var file = _fileNames.ManagersConfig;
        Logger.LogInformation($"Loading Managers Config for: {file}");
        if (!File.Exists(file))
        {
            Logger.LogWarning($"No Managers Config found at {file}");
            // create a new file with default values.
            _saver.Save(this);
            return;
        }

        // Read the json from the file.
        var jsonText = File.ReadAllText(file);
        var jObject = JObject.Parse(jsonText);
        var version = jObject["Version"]?.Value<int>() ?? 1;

        switch (version)
        {
            case 0:
            case 1:
                LoadV1(jObject);
                break;
            default:
                Logger.LogError("Invalid Version!");
                return;
        }
        // Update the saved data.
        _saver.Save(this);
    }

    private void LoadV1(JObject jObject)
    {
        // Load in as normal.
        _managers = jObject["StatusManagers"]?.ToObject<Dictionary<string, ActorSM>>() ?? new Dictionary<string, ActorSM>();
        // Clear out all data aside from statuses from the clientManagers.
        foreach (var (name, data) in _managers.ToList())
        {
            data.AddTextShown.Clear();
            data.RemTextShown.Clear();
            data.LockedStatuses.Clear();
            data.EphemeralHosts.Clear();
        }
    }
    #endregion HybridSavable
}