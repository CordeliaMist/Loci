using CkCommons;
using CkCommons.DrawSystem;
using CkCommons.HybridSaver;
using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using LociApi.Ipc;

namespace Loci.DrawSystem;

public class SMDrawSystem : DynamicDrawSystem<ActorSM>, IMediatorSubscriber, IDisposable, IHybridSavable
{
    public const string PLAYER_TAG = "Players";
    public const string MINION_TAG = "Minions";
    public const string PET_TAG = "Pets"; // Not working atm maybe idk

    private readonly ILogger<SMDrawSystem> _logger;
    private readonly SaveService _hybridSaver;

    private readonly object _folderUpdateLock = new();

    public LociMediator Mediator { get; init; }

    public SMDrawSystem(ILogger<SMDrawSystem> logger, LociMediator mediator, SaveService saver)
    {
        _logger = logger;
        Mediator = mediator;
        _hybridSaver = saver;

        // Load the hierarchy and initialize the folders.
        LoadData();

        // These can possibly occur at the same time and must be accounted for.
        Mediator.Subscribe<FolderUpdateManagers>(this, _ => { lock (_folderUpdateLock) UpdateFolders(); });

        // Subscribe to the changes (which is to change very, very soon, with overrides.
        DDSChanged += OnChange;
        CollectionUpdated += OnCollectionUpdate;
    }

    public void Dispose()
    {
        Mediator.UnsubscribeAll(this);
        DDSChanged -= OnChange;
        CollectionUpdated -= OnCollectionUpdate;
    }

    // Note that this will change very soon, as saves should only occur for certain changes.
    private void OnChange(DDSChange type, IDynamicNode<ActorSM> obj, IDynamicCollection<ActorSM>? _, IDynamicCollection<ActorSM>? __)
    {
        if (type is not (DDSChange.FullReloadStarting or DDSChange.FullReloadFinished))
        {
            _logger.LogInformation($"DDS Change [{type}] for node [{obj.Name} ({obj.FullPath})] occured. Saving Config.");
            _hybridSaver.Save(this);
        }
    }

    private void OnCollectionUpdate(CollectionUpdate kind, IDynamicCollection<ActorSM> collection, IEnumerable<DynamicLeaf<ActorSM>>? _)
    {
        if (kind is CollectionUpdate.OpenStateChange)
            _hybridSaver.Save(this);
    }

    private void LoadData()
    {
        // Before we load anything, inverse the sort direction of root.
        SetSortDirection(root, true);
        // If any changes occured, re-save the file.
        if (LoadFile(new FileInfo(_hybridSaver.FileNames.DDS_Managers)))
        {
            _logger.LogInformation("WhitelistDrawSystem folder structure changed on load, saving updated structure.");
            _hybridSaver.Save(this);
        }
        // See if the file doesnt exist, if it does not, load defaults.
        else if (!File.Exists(_hybridSaver.FileNames.DDS_Managers))
        {
            _logger.LogInformation("Loading Defaults and saving.");
            EnsureAllFolders(new Dictionary<string, string>());
            _hybridSaver.Save(this);
        }
    }

    protected override bool EnsureAllFolders(Dictionary<string, string> _)
    {
        bool anyChanged = false;
#if DEBUG
        Func<IEnumerable<ActorSM>> source = () => LociManager.Managers.Values;
#else
        Func<IEnumerable<ActorSM>> source = () => LociManager.Rendered.Values;
#endif
        // Players
        if (!FolderMap.ContainsKey(PLAYER_TAG))
            anyChanged |= AddFolder(new ManagerFolder(root, idCounter + 1u, FAI.User, PLAYER_TAG, CkCol.TriStateCheck.Uint(),
                () => [.. source().Where(x => x.ActorKind is ObjectKind.Pc)], GetDefaultSorter()));
        // Minions
        if (!FolderMap.ContainsKey(MINION_TAG))
            anyChanged |= AddFolder(new ManagerFolder(root, idCounter + 1u, FAI.User, MINION_TAG, CkCol.TriStateCheck.Uint(),
                () => [.. source().Where(x => x.ActorKind is ObjectKind.Companion)], GetDefaultSorter()));
        // Pets (Not working atm, maybe later?)
        if (!FolderMap.ContainsKey(PET_TAG))
            anyChanged |= AddFolder(new ManagerFolder(root, idCounter + 1u, FAI.User, PET_TAG, CkCol.TriStateCheck.Uint(),
                () => [.. source().Where(x => x.ActorKind is ObjectKind.BattleNpc)], GetDefaultSorter()));
        // Ensure show empty is false
        SetShowIfEmptyState(PLAYER_TAG, false);
        SetShowIfEmptyState(MINION_TAG, false);
        SetShowIfEmptyState(PET_TAG, false);

        _logger.LogInformation($"Ensured all folders, total now {FolderMap.Count} folders.");
        return anyChanged;
    }

    public void UpdateFilters()
    {
        var sorter = GetDefaultSorter();
        // Update all children to be either favorites first or not.
        foreach (var folder in Root.Children.OfType<ManagerFolder>())
            SetSorterSteps(folder, sorter);
    }

    private IReadOnlyList<ISortMethod<DynamicLeaf<ActorSM>>> GetDefaultSorter()
        => [SorterExtensions.ByName];

    // HybridSavable
    public int ConfigVersion => 0;
    public HybridSaveType SaveType => HybridSaveType.StreamWrite;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(FileProvider files, out bool _)
        => (_ = false, files.DDS_Managers).Item2;

    public string JsonSerialize()
        => throw new NotImplementedException();

    public void WriteToStream(StreamWriter writer)
        => SaveToFile(writer);
}
