using CkCommons;
using CkCommons.FileSystem;
using CkCommons.HybridSaver;
using CkCommons.RichText;
using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Loci.DrawSystem;

// Use this temporarily until we can find a better way to integrate into DDS.
public sealed class StatusesFS : CkFileSystem<LociStatus>, IMediatorSubscriber, IHybridSavable, IDisposable
{
    private readonly ILogger<StatusesFS> _logger;
    private readonly LociManager _manager;
    private readonly SaveService _hybridSaver;
    public LociMediator Mediator { get; init; }
    public StatusesFS(ILogger<StatusesFS> logger, LociMediator mediator,
        LociManager manager, SaveService saver)
    {
        _logger = logger;
        Mediator = mediator;
        _manager = manager;
        _hybridSaver = saver;

        Mediator.Subscribe<LociStatusChanged>(this, _ => OnStatusChange(_.Type, _.Item, _.OldString));
        Mediator.Subscribe<ReloadCKFS>(this, _ => { if (!_.IsPresetFS) Reload(); });
        Changed += OnChange;
        Reload();
    }

    private void Reload()
    {
        if (Load(new FileInfo(_hybridSaver.FileNames.CKFS_Statuses), LociData.Statuses, StatusToIdentifier, StatusToName))
            _hybridSaver.Save(this);

        _logger.LogDebug($"Reloaded CKFS with {LociData.Statuses.Count} statuses.");
    }

    public void MergeWithMigratableFile(string migratablePath)
    {
        var basePath = new FileInfo(_hybridSaver.FileNames.CKFS_Statuses);
        var migratableFile = new FileInfo(migratablePath);
        MigrateAndReloadFsFile(migratableFile, basePath, LociData.Statuses, StatusToIdentifier, StatusToName);
        _logger.LogInformation($"Migrated statuses from {migratableFile.FullName} to {basePath.FullName}.");
        _hybridSaver.Save(this);
    }

    public void Dispose()
    {
        Mediator.UnsubscribeAll(this);
        Changed -= OnChange;
    }

    private void OnChange(FileSystemChangeType type, IPath _1, IPath? _2, IPath? _3)
    {
        if (type is not FileSystemChangeType.Reload)
            _hybridSaver.Save(this);
    }

    public bool FindLeaf(LociStatus loot, [NotNullWhen(true)] out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<LociStatus>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == loot);
        return leaf != null;
    }

    private void OnStatusChange(FSChangeType type, LociStatus item, string? oldName)
    {
        switch (type)
        {
            case FSChangeType.Created:
                var parent = Root;
                if (oldName != null)
                    Generic.Safe(() => parent = FindOrCreateAllFolders(oldName));
                // Dupe the leaf
                CreateDuplicateLeaf(parent, CkRichText.StripDisallowedRichTags(item.Title, 0), item);
                return;
            case FSChangeType.Deleted:
                {
                    if (FindLeaf(item, out var leaf))
                        Delete(leaf);
                    return;
                }
            case FSChangeType.Modified:
                {
                    // need to run checks for type changes and modifications.
                    if (!FindLeaf(item, out var existingLeaf))
                        return;
                    // Check for type changes.
                    if (existingLeaf.Value.GetType() != item.GetType())
                        UpdateLeafValue(existingLeaf, item);
                    // Detect potential renames.
                    if (existingLeaf.Name != CkRichText.StripDisallowedRichTags(item.Title, 0))
                        RenameWithDuplicates(existingLeaf, CkRichText.StripDisallowedRichTags(item.Title, 0));
                    return;
                }
            case FSChangeType.Renamed when oldName != null:
                {
                    if (!FindLeaf(item, out var leaf))
                        return;

                    var old = CkRichText.StripDisallowedRichTags(oldName, 0).FixName();
                    if (old == leaf.Name || leaf.Name.IsDuplicateName(out var baseName, out _) && baseName == old)
                        RenameWithDuplicates(leaf, CkRichText.StripDisallowedRichTags(item.Title, 0));
                    return;
                }
        }
    }

    // Used for saving and loading.
    private static string StatusToIdentifier(LociStatus item)
        => item.ID.ToString();

    private static string StatusToName(LociStatus item)
        => CkRichText.StripDisallowedRichTags(item.Title, 0).FixName();

    private static bool StatusHasDefaultPath(LociStatus item, string fullPath)
    {
        var regex = new Regex($@"^{Regex.Escape(StatusToName(item))}( \(\d+\))?$");
        return regex.IsMatch(fullPath);
    }

    private static (string, bool) SaveStatus(LociStatus item, string fullPath)
        => StatusHasDefaultPath(item, fullPath) ? (string.Empty, false) : (StatusToIdentifier(item), true);

    // HybridSavable
    public int ConfigVersion => 0;
    public HybridSaveType SaveType => HybridSaveType.StreamWrite;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(FileProvider files, out bool _)
        => (_ = false, files.CKFS_Statuses).Item2;

    public string JsonSerialize()
        => throw new NotImplementedException();

    public void WriteToStream(StreamWriter writer) 
        => SaveToFile(writer, SaveStatus, true);
}

