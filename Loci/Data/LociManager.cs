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

    // Static readonlys for fast access by label.
    internal static IReadOnlyDictionary<string, ActorSM> Managers => _managers;

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

    // This occurs after the player is finished rendering.
    private void OnTerritoryChange(ushort prev, ushort next)
    {
        var clientNameWorld = PlayerData.NameWithWorld;
        // Clean up all non-client and non-ephemeral managers.
        foreach (var (name, lociSM) in _managers.ToList())
            if (name != clientNameWorld && !lociSM.Ephemeral && !lociSM.OwnerValid)
                _managers.Remove(name);
        Mediator.Publish(new FolderUpdateManagers());
    }

    private unsafe void InitializeData()
    {
        InitClientSM();
        // Then also do this for all other characters
        foreach (var charaAddr in CharaWatcher.Rendered)
        {
            var chara = (Character*)charaAddr;
            if (chara is null || !chara->IsCharacter())
                continue;

            var nameKey = Utils.ToLociName(chara);
            // Assign if existing, otherwise create and assign
            if (_managers.TryGetValue(nameKey, out var lociSM))
            {
                lociSM.Owner = chara;
                Logger.LogTrace($"Assigned {{{nameKey}}} to their LociSM", LoggerType.Data);
            }
            else if (!string.IsNullOrEmpty(nameKey))
            {
                var newSM = new ActorSM() 
                {
                    ActorKind = chara->ObjectKind,
                    Identifier = nameKey,
                    Owner = chara
                };
                _managers.TryAdd(nameKey, newSM);
                Logger.LogTrace($"Created and Assigned {{{nameKey}}} to a new LociSM", LoggerType.Data);
            }
        }
        Mediator.Publish(new FolderUpdateManagers());
    }

    private unsafe void InitClientSM()
    {
        var playerName = PlayerData.NameWithWorld;
        // If it exists, we need to ensure sync.
        if (_managers.TryGetValue(playerName, out var existingSM))
        {
            Logger.LogDebug($"Found existing status manager for player {playerName}, assigning to ClientSM and syncing data.", LoggerType.Data);
            ClientSM = existingSM;
            ClientSM.Owner = PlayerData.Character;
        }
        // Otherwise, we need to create a new entry for both.
        else
        {
            Logger.LogDebug($"No existing client status manager for player {playerName}, creating new one and assigning to ClientSM and StatusManagers.", LoggerType.Data);
            var manager = new ActorSM()
            {
                ActorKind = ObjectKind.Pc,
                Identifier = playerName,
                Owner = PlayerData.Character
            };
            _managers.TryAdd(playerName, manager);
            ClientSM = manager;
        }
    }

    private unsafe void OnObjectCreated(IntPtr address)
    {
        var chara = (Character*)address;
        if (chara is null || chara->ObjectIndex >= 200 || !chara->IsCharacter() || chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        var nameKey = Utils.ToLociName(chara);
        if (string.IsNullOrEmpty(nameKey))
            return;

        if (_managers.TryGetValue(nameKey, out var lociSM))
        {
            lociSM.Owner = chara;
            Logger.LogTrace($"Assigned {{{nameKey}}} to their LociSM", LoggerType.Data);
        }
        else
        {
            var newSM = new ActorSM()
            {
                ActorKind = chara->ObjectKind,
                Identifier = nameKey,
                Owner = PlayerData.Character
            };
            _managers.TryAdd(nameKey, newSM);
            Logger.LogTrace($"Created and Assigned {{{nameKey}}} to a new LociSM", LoggerType.Data);
            Mediator.Publish(new FolderUpdateManagers());
        }
    }

    private unsafe void OnObjectDeleted(IntPtr address)
    {
        var chara = (Character*)address;
        if (chara is null || chara->ObjectIndex >= 200 || !chara->IsCharacter() || chara->ObjectKind is not (ObjectKind.Pc or ObjectKind.Companion))
            return;

        var nameKey = Utils.ToLociName(chara);
        if (_managers.TryGetValue(nameKey, out var lociSM))
        {
            // We dont want to remove the status manager, but we do want to unassign the owner.
            if (lociSM.OwnerValid)
                lociSM.Owner = null;

            // If they are not ephemeral or they have 0 statuses, remove them.
            if (!lociSM.Ephemeral && lociSM.Statuses.Count is 0)
            {
                // Remove if they are not the client.
                if (lociSM != ClientSM)
                {
                    _managers.Remove(nameKey);
                    Logger.LogDebug($"Removed LociSM for {nameKey} due to 0 statuses and not being ephemeral", LoggerType.Data);
                    Mediator.Publish(new FolderUpdateManagers());
                }
            }
        }
    }

    // For registering and unregistering identifiers to existing status managers.
    public unsafe bool AttachIdToActor(string nameWorld, string identifier)
    {
        // Fail if the Client.
        if (PlayerData.NameWithWorld == nameWorld)
            return false;
        // Grab the manager, creating one if not yet present.
        var sm = GetFromName(nameWorld);
        // return if we could add it to the manager or not.
        return sm.EphemeralHosts.Add(identifier);
    }

    public unsafe bool DetachIdFromActor(string nameWorld, string identifier)
    {
        if (PlayerData.NameWithWorld == nameWorld)
            return false;
        // Grab the manager, creating one if not yet present.
        var sm = GetFromName(nameWorld);
        // return if we could add it to the manager or not.
        return sm.EphemeralHosts.Remove(identifier);
    }

    public unsafe static ActorSM GetFromName(string nameKey, bool create = true)
    {
        if (!_managers.TryGetValue(nameKey, out var manager))
        {
            if (create)
            {
                manager = new();
                // Add it to the dictionary.
                _managers.TryAdd(nameKey, manager);
                // If we can identify the player from the object watcher, we should set it in the manager.
                if (CharaWatcher.TryGetFirstUnsafe(x => Utils.ToLociName(x) == nameKey, out var chara))
                {
                    manager.ActorKind = chara->ObjectKind;
                    manager.Identifier = nameKey;
                    manager.Owner = chara;
                }
            }
        }
        return manager!;
    }

    public unsafe static ActorSM GetFromChara(Character* chara, bool create = true)
    {
        var nameKey = Utils.ToLociName(chara);
        if (!_managers.TryGetValue(nameKey, out var manager))
        {
            if (create)
            {
                manager = new()
                {
                    ActorKind = chara->ObjectKind,
                    Identifier = nameKey,
                    Owner = chara,
                };
                _managers.TryAdd(nameKey, manager);
            }
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