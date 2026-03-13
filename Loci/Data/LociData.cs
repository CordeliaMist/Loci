using CkCommons.Helpers;
using CkCommons.HybridSaver;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;

namespace Loci.Data;

/// <summary>
///     Holds all data relative to statuses, presets, and events within Loci.
/// </summary>
public sealed class LociData : IHybridSavable
{
    private readonly ILogger<LociData> _logger;
    private readonly LociMediator _mediator;
    private readonly FileProvider _fileNames;
    private readonly SaveService _saver;

    // maybe make these static, not sure yet.
    private static List<LociStatus> _statuses = [];
    private static List<LociPreset> _presets  = [];

    public LociData(ILogger<LociData> logger, LociMediator mediator,
        FileProvider fileNames, SaveService saver)
    {
        _logger = logger;
        _mediator = mediator;
        _fileNames = fileNames;
        _saver = saver;
        Load();
    }

    internal static IReadOnlyList<LociStatus> Statuses => _statuses;
    internal static IReadOnlyList<LociPreset> Presets => _presets;

    public LociStatus CreateStatus(string name)
    {
        name = RegexEx.EnsureUniqueName(name, _statuses, (s) => s.Title);
        var newStatus = new LociStatus() { Title = name };
        _statuses.Add(newStatus);
        _saver.Save(this);
        _mediator.Publish(new LociStatusChanged(FSChangeType.Created, newStatus, null));
        return newStatus;
    }

    public LociPreset CreatePreset(string name)
    {
        name = RegexEx.EnsureUniqueName(name, _presets, (s) => s.Title);
        var newPreset = new LociPreset() { Title = name };
        _presets.Add(newPreset);
        _saver.Save(this);
        _mediator.Publish(new LociPresetChanged(FSChangeType.Created, newPreset, null));
        return newPreset;
    }

    public bool ImportStatus(LociStatus? imported)
    {
        if (imported is null)
            return false;

        var newStatus = imported.NewtonsoftDeepClone();
        newStatus.Title = RegexEx.EnsureUniqueName(imported.Title, _statuses, (s) => s.Title);
        _statuses.Add(newStatus);
        _saver.Save(this);
        _mediator.Publish(new LociStatusChanged(FSChangeType.Created, newStatus, null));
        return true;
    }

    public bool ImportPreset(LociPreset? imported)
    {
        if (imported is null)
            return false;

        var newPreset = imported.NewtonsoftDeepClone();
        newPreset.GUID = Guid.NewGuid();
        newPreset.Title = RegexEx.EnsureUniqueName(imported.Title, _presets, (s) => s.Title);
        _presets.Add(newPreset);
        _saver.Save(this);
        _mediator.Publish(new LociPresetChanged(FSChangeType.Created, newPreset, null));
        return true;
    }

    public LociStatus CloneStatus(LociStatus other, string newName)
    {
        var clonedItem = other.NewtonsoftDeepClone();
        clonedItem.GUID = Guid.NewGuid();
        clonedItem.Title = newName;
        _statuses.Add(clonedItem);
        _saver.Save(this);
        _logger.LogDebug($"Cloned status {other.Title} to {newName}.", LoggerType.DataManagement);
        _mediator.Publish(new LociStatusChanged(FSChangeType.Created, clonedItem, null));
        return clonedItem;
    }

    public LociPreset ClonePreset(LociPreset other, string newName)
    {
        var clonedItem = other.NewtonsoftDeepClone();
        clonedItem.GUID = Guid.NewGuid();
        clonedItem.Title = newName;
        _presets.Add(clonedItem);
        _saver.Save(this);
        _logger.LogDebug($"Cloned preset {other.Title} to {newName}.", LoggerType.DataManagement);
        _mediator.Publish(new LociPresetChanged(FSChangeType.Created, clonedItem, null));
        return clonedItem;
    }

    public void RenameStatus(LociStatus status, string newName)
    {
        var prevName = status.Title;
        _logger.LogDebug($"Renaming status {prevName} to {newName}.", LoggerType.DataManagement);
        status.Title = newName;
        _saver.Save(this);
        _mediator.Publish(new LociStatusChanged(FSChangeType.Renamed, status, prevName));
    }

    public void RenamePreset(LociPreset preset, string newName)
    {
        var prevName = preset.Title;
        _logger.LogDebug($"Renaming preset {prevName} to {newName}.", LoggerType.DataManagement);
        preset.Title = newName;
        _saver.Save(this);
        _mediator.Publish(new LociPresetChanged(FSChangeType.Renamed, preset, prevName));
    }

    public void MarkStatusModified(LociStatus status)
    {
        _logger.LogDebug($"Modified status {status.Title}.", LoggerType.DataManagement);
        _saver.Save(this);
        _mediator.Publish(new LociStatusChanged(FSChangeType.Modified, status, null));
    }

    public void MarkPresetModified(LociPreset preset)
    {
        // Ensure the title is still unique.
        _logger.LogDebug($"Modified preset {preset.Title}.", LoggerType.DataManagement);
        _saver.Save(this);
        _mediator.Publish(new LociPresetChanged(FSChangeType.Modified, preset, null));
    }

    public void DeleteStatus(LociStatus status)
    {
        if (_statuses.Remove(status))
        {
            _logger.LogDebug($"Deleted status {status.Title}.", LoggerType.DataManagement);
            _mediator.Publish(new LociStatusChanged(FSChangeType.Deleted, status, null));
            _saver.Save(this);
        }
    }

    public void DeletePreset(LociPreset preset)
    {
        if (_presets.Remove(preset))
        {
            _logger.LogDebug($"Deleted preset {preset.Title}.", LoggerType.DataManagement);
            _mediator.Publish(new LociPresetChanged(FSChangeType.Deleted, preset, null));
            _saver.Save(this);
        }
    }

    public void Save()
        => _saver.Save(this);

    public void MoodleStatusMigration(JObject jObj)
    {
        if (jObj is null)
            return;
        var savedStatuses = jObj["SavedStatuses"]?.ToObject<List<JObject>>() ?? new();
        foreach (var statusObj in savedStatuses)
        {
            try
            {
                // Construct the LociStatus
                var status = new LociStatus
                {
                    GUID = statusObj["GUID"]?.ToObject<Guid>() ?? throw new Bagagwa("Status missing GUID"),
                    IconID = statusObj["IconID"]?.ToObject<uint>() ?? 0,
                    Title = statusObj["Title"]?.ToObject<string>() ?? "",
                    Description = statusObj["Description"]?.ToObject<string>() ?? "",
                    CustomFXPath = statusObj["CustomFXPath"]?.ToObject<string>() ?? "",
                    ExpiresAt = statusObj["ExpiresAt"]?.ToObject<long>() ?? 0,
                    Type = (StatusType)(statusObj["Type"]?.ToObject<byte>() ?? 0), // convert to byte enum
                    Modifiers = (Modifiers)(statusObj["Modifiers"]?.ToObject<int>() ?? 0),
                    Stacks = statusObj["Stacks"]?.ToObject<int>() ?? 1,
                    StackSteps = statusObj["StackSteps"]?.ToObject<int>() ?? 0,
                    StackToChain = 0,
                    ChainedGUID = statusObj["ChainedStatus"]?.ToObject<Guid>() ?? Guid.Empty,
                    ChainedType = ChainType.Status,
                    ChainTrigger = (ChainTrigger)(statusObj["ChainTrigger"]?.ToObject<int>() ?? 0),
                    Applier = statusObj["Applier"]?.ToObject<string>() ?? "",
                    Dispeller = statusObj["Dispeller"]?.ToObject<string>() ?? "",
                    Persistent = statusObj["Persistent"]?.ToObject<bool>() ?? false,
                    Days = statusObj["Days"]?.ToObject<int>() ?? 0,
                    Hours = statusObj["Hours"]?.ToObject<int>() ?? 0,
                    Minutes = statusObj["Minutes"]?.ToObject<int>() ?? 0,
                    Seconds = statusObj["Seconds"]?.ToObject<int>() ?? 0,
                    NoExpire = statusObj["NoExpire"]?.ToObject<bool>() ?? false,
                };
                // Ignore clones
                if (Statuses.Any(s => s.GUID == status.GUID))
                    continue;
                // Ensure unique title.
                _statuses.Add(status);
            }
            catch
            {
                _logger.LogWarning($"Failed to migrate status: {statusObj}");
            }
        }
        _saver.Save(this);
    }

    public void SundStatusMigration(JObject jObj)
    {
        if (jObj is null)
            return;
        var savedStatuses = jObj["Statuses"]?.ToObject<List<JObject>>() ?? new();
        foreach (var statusObj in savedStatuses)
        {
            try
            {
                // Construct the LociStatus
                var status = new LociStatus
                {
                    GUID = statusObj["GUID"]?.ToObject<Guid>() ?? throw new Bagagwa("Status missing GUID"),
                    IconID = statusObj["IconID"]?.ToObject<uint>() ?? 0,
                    Title = statusObj["Title"]?.ToObject<string>() ?? "",
                    Description = statusObj["Description"]?.ToObject<string>() ?? "",
                    CustomFXPath = statusObj["CustomFXPath"]?.ToObject<string>() ?? "",
                    ExpiresAt = statusObj["ExpiresAt"]?.ToObject<long>() ?? 0,
                    Type = (StatusType)(statusObj["Type"]?.ToObject<byte>() ?? 0), // convert to byte enum
                    Modifiers = (Modifiers)(statusObj["Modifiers"]?.ToObject<int>() ?? 0),
                    Stacks = statusObj["Stacks"]?.ToObject<int>() ?? 1,
                    StackSteps = statusObj["StackSteps"]?.ToObject<int>() ?? 0,
                    StackToChain = statusObj["StackToChain"]?.ToObject<int>() ?? 0,
                    ChainedGUID = statusObj["ChainedGUID"]?.ToObject<Guid>() ?? Guid.Empty,
                    ChainedType = (ChainType)(statusObj["ChainedType"]?.ToObject<byte>() ?? 0),
                    ChainTrigger = (ChainTrigger)(statusObj["ChainTrigger"]?.ToObject<int>() ?? 0),
                    Applier = statusObj["Applier"]?.ToObject<string>() ?? "",
                    Dispeller = statusObj["Dispeller"]?.ToObject<string>() ?? "",
                    Persistent = statusObj["Persistent"]?.ToObject<bool>() ?? false,
                    Days = statusObj["Days"]?.ToObject<int>() ?? 0,
                    Hours = statusObj["Hours"]?.ToObject<int>() ?? 0,
                    Minutes = statusObj["Minutes"]?.ToObject<int>() ?? 0,
                    Seconds = statusObj["Seconds"]?.ToObject<int>() ?? 0,
                    NoExpire = statusObj["NoExpire"]?.ToObject<bool>() ?? false,
                };
                // Ignore clones
                if (Statuses.Any(s => s.GUID == status.GUID))
                    continue;
                // Ensure unique title.
                _statuses.Add(status);
            }
            catch
            {
                _logger.LogWarning($"Failed to migrate status: {statusObj}");
            }
        }
        _saver.Save(this);
    }

    public void MoodlePresetMigration(JObject jObj)
    {
        if (jObj is null)
            return;
        var savedPresets = jObj["SavedPresets"]?.ToObject<List<JObject>>() ?? new();
        foreach (var presetObj in savedPresets)
        {
            try
            {
                var preset = new LociPreset
                {
                    GUID = presetObj["GUID"]?.ToObject<Guid>() ?? throw new Bagagwa("Missing GUID"),
                    Statuses = presetObj["Statuses"]?.ToObject<List<Guid>>() ?? new(),
                    ApplyType = (PresetApplyType)(presetObj["ApplicationType"]?.ToObject<byte>() ?? 0),
                    Title = presetObj["Title"]?.ToObject<string>() ?? "",
                };
                // Prevent duplicates
                if (Presets.Any(p => p.GUID == preset.GUID))
                    continue;
                // Ensure unique title.
                _presets.Add(preset);
            }
            catch
            {
                _logger.LogWarning($"Failed to migrate preset: {presetObj}");
            }
        }
        _saver.Save(this);
    }

    public void SundPresetMigration(JObject jObj)
    {
        if (jObj is null)
            return;
        var savedPresets = jObj["Presets"]?.ToObject<List<JObject>>() ?? new();
        foreach (var presetObj in savedPresets)
        {
            try
            {
                var preset = new LociPreset
                {
                    GUID = presetObj["GUID"]?.ToObject<Guid>() ?? throw new Bagagwa("Missing GUID"),
                    Statuses = presetObj["Statuses"]?.ToObject<List<Guid>>() ?? new(),
                    ApplyType = (PresetApplyType)(presetObj["ApplyType"]?.ToObject<byte>() ?? 0),
                    Title = presetObj["Title"]?.ToObject<string>() ?? "",
                    Description = presetObj["Description"]?.ToObject<string>() ?? "",
                };
                // Prevent duplicates
                if (Presets.Any(p => p.GUID == preset.GUID))
                    continue;
                // Add it if ID was unique.
                _presets.Add(preset);
            }
            catch
            {
                _logger.LogWarning($"Failed to migrate preset: {presetObj}");
            }
        }
        _saver.Save(this);
    }

    #region HybridSavable
    public int ConfigVersion => 1;
    public HybridSaveType SaveType => HybridSaveType.Json;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(FileProvider files, out bool _) => (_ = false, files.DataConfig).Item2;
    public void WriteToStream(StreamWriter writer) => throw new NotImplementedException();
    public string JsonSerialize()
    {
        // construct the config object to serialize.
        return new JObject()
        {
            ["Version"] = ConfigVersion,
            ["Statuses"] = JArray.FromObject(_statuses),
            ["Presets"] = JArray.FromObject(_presets),
        }.ToString(Formatting.None); // No pretty formatting here.
    }

    public void Load()
    {
        var file = _fileNames.DataConfig;
        _logger.LogInformation($"Loading DataConfig for: {file}");
        _statuses.Clear();
        _presets.Clear();
        if (!File.Exists(file))
        {
            _logger.LogWarning($"No DataConfig found at {file}");
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
                // Migrate to V1 first, then load V1.
                MigrateV0ToV1(jObject);
                LoadV1(jObject);
                break;
            case 1:
                LoadV1(jObject);
                break;
            default:
                _logger.LogError("Invalid Version!");
                return;
        }
        // Update the saved data.
        _saver.Save(this);
    }

    public void MigrateV0ToV1(JObject root)
    {
        root["Version"] = 1;
        MigrateStatusArray(root["Statuses"] as JArray);
    }

    private void MigrateStatusArray(JArray? jArray)
    {
        if (jArray is not JArray statuses)
            return;

        foreach (var token in statuses)
        {
            if (token is not JObject status)
                continue;

            // Rename ChainedStatus -> ChainedGUID
            if (status.TryGetValue("ChainedStatus", out var chained))
            {
                status["ChainedGUID"] = chained;
                status.Remove("ChainedStatus");
            }

            // Add StackToChain
            if (!status.ContainsKey("StackToChain"))
                status["StackToChain"] = 0;

            // Add ChainedType
            if (!status.ContainsKey("ChainedType"))
                status["ChainedType"] = 0; // ChainType.Status default
        }
    }

    private void LoadV1(JObject jObject)
    {
        // Load in as normal.
        _statuses = jObject["Statuses"]?.ToObject<List<LociStatus>>() ?? new List<LociStatus>();
        _presets = jObject["Presets"]?.ToObject<List<LociPreset>>() ?? new List<LociPreset>();
    }
    #endregion HybridSavable
}