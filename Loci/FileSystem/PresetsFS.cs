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
public sealed class PresetsFS : CkFileSystem<LociPreset>, IMediatorSubscriber, IHybridSavable, IDisposable
{
    private readonly ILogger<PresetsFS> _logger;
    private readonly LociManager _manager;
    private readonly SaveService _hybridSaver;
    public LociMediator Mediator { get; init; }
    public PresetsFS(ILogger<PresetsFS> logger, LociMediator mediator,
        LociManager manager, SaveService saver)
    {
        _logger = logger;
        Mediator = mediator;
        _manager = manager;
        _hybridSaver = saver;

        Mediator.Subscribe<LociPresetChanged>(this, _ => OnPresetChange(_.Type, _.Item, _.OldString));
        Mediator.Subscribe<ReloadCKFS>(this, _ => { if (_.Module is LociModule.Presets) Reload(); });
        Changed += OnChange;
        Reload();
    }

    private void Reload()
    {
        if (Load(new FileInfo(_hybridSaver.FileNames.CKFS_Presets), LociData.Presets, PresetToIdentifier, PresetToName))
            _hybridSaver.Save(this);

        _logger.LogDebug($"Reloaded CKFS with {LociData.Presets.Count} preset.");
    }

    public void MergeWithMigratableFile(string migratablePath)
    {
        var basePath = new FileInfo(_hybridSaver.FileNames.CKFS_Presets);
        var migratableFile = new FileInfo(migratablePath);
        MigrateAndReloadFsFile(migratableFile, basePath, LociData.Presets, PresetToIdentifier, PresetToName);
        _logger.LogInformation($"Migrated presets from {migratableFile.FullName} to {basePath.FullName}.");
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

    public bool FindLeaf(LociPreset loot, [NotNullWhen(true)] out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<LociPreset>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == loot);
        return leaf != null;
    }

    private void OnPresetChange(FSChangeType type, LociPreset item, string? oldName)
    {
        switch (type)
        {
            case FSChangeType.Created:
                var parent = Root;
                if (oldName != null)
                    Generic.Safe(() => parent = FindOrCreateAllFolders(oldName));
                // Dupe the leaf
                CreateDuplicateLeaf(parent, NewRichText.StripDisallowedRichTags(item.Title, 0), item);
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
                    return;
                }
            case FSChangeType.Renamed when oldName is not null:
                {
                    if (!FindLeaf(item, out var leaf))
                        return;

                    var old = NewRichText.StripDisallowedRichTags(oldName, 0).FixName();
                    var newName = NewRichText.StripDisallowedRichTags(item.Title, 0).FixName();
                    // Only auto-rename if the leafs path name was the same as the old name. If it was custom, ignore it.
                    if (old == leaf.Name || leaf.Name.IsDuplicateName(out var baseName, out _) && baseName == old)
                        RenameWithDuplicates(leaf, newName);
                    return;
                }
        }
    }

    // Used for saving and loading.
    private static string PresetToIdentifier(LociPreset item)
        => item.ID.ToString();

    private static string PresetToName(LociPreset item)
        => NewRichText.StripDisallowedRichTags(item.Title, 0).FixName();

    private static bool PresetHasDefaultPath(LociPreset item, string fullPath)
    {
        var regex = new Regex($@"^{Regex.Escape(PresetToName(item))}( \(\d+\))?$");
        return regex.IsMatch(fullPath);
    }

    private static (string, bool) SavePreset(LociPreset item, string fullPath)
        => PresetHasDefaultPath(item, fullPath) ? (string.Empty, false) : (PresetToIdentifier(item), true);

    // HybridSavable
    public int ConfigVersion => 0;
    public int MaxBackups => 2;
    public HybridSaveType SaveType => HybridSaveType.StreamWrite;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string ToFilePath(FileProvider files) => files.CKFS_Presets;

    public string JsonSerialize()
        => throw new NotImplementedException();

    public void WriteToStream(StreamWriter writer) 
        => SaveToFile(writer, SavePreset, true);
}

