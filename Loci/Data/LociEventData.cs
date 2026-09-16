using CkCommons.Helpers;
using CkCommons.HybridSaver;
using Loci.Services;
using Loci.Services.Mediator;

namespace Loci.Data;

/// <summary>
///     Holds all data relative to events, presets, and events within Loci.
/// </summary>
public sealed class LociEventData : IHybridSavable
{
    private readonly ILogger<LociEventData> _logger;
    private readonly LociMediator _mediator;
    private readonly FileProvider _fileNames;
    private readonly SaveService _saver;

    // maybe make these static, not sure yet.
    private static List<LociEvent> _events = [];

    public LociEventData(ILogger<LociEventData> logger, LociMediator mediator,
        FileProvider fileNames, SaveService saver)
    {
        _logger = logger;
        _mediator = mediator;
        _fileNames = fileNames;
        _saver = saver;
        Load();
    }

    internal static IReadOnlyList<LociEvent> Events => _events;

    public LociEvent CreateEvent(string name)
    {
        var newEvent = new LociEvent() { Title = name };
        _events.Add(newEvent);
        _saver.Save(this);
        _mediator.Publish(new LociEventChanged(FSChangeType.Created, newEvent, null));
        return newEvent;
    }

    public bool ImportEvent(LociEvent? imported)
    {
        if (imported is null)
            return false;

        var newEvent = imported.NewtonsoftDeepClone();
        newEvent.Title = RegexEx.EnsureUniqueName(imported.Title, _events, (s) => s.Title);
        _events.Add(newEvent);
        _saver.Save(this);
        _mediator.Publish(new LociEventChanged(FSChangeType.Created, newEvent, null));
        return true;
    }

    public LociEvent CloneEvent(LociEvent other, string newName)
    {
        var clonedItem = other.NewtonsoftDeepClone();
        clonedItem.GUID = Guid.NewGuid();
        clonedItem.Title = newName;
        _events.Add(clonedItem);
        _saver.Save(this);
        _logger.LogDebug($"Cloned event {other.Title} to {newName}.", LoggerType.DataManagement);
        _mediator.Publish(new LociEventChanged(FSChangeType.Created, clonedItem, null));
        return clonedItem;
    }

    public void RenameEvent(LociEvent lociEvent, string newName)
    {
        var prevName = lociEvent.Title;
        _logger.LogDebug($"Renaming event {prevName} to {newName}.", LoggerType.DataManagement);
        lociEvent.Title = newName;
        _saver.Save(this);
        _mediator.Publish(new LociEventChanged(FSChangeType.Renamed, lociEvent, prevName));
    }

    public void MarkEventModified(LociEvent lociEvent)
    {
        _logger.LogDebug($"Modified event {lociEvent.Title}.", LoggerType.DataManagement);
        _saver.Save(this);
        _mediator.Publish(new LociEventChanged(FSChangeType.Modified, lociEvent, null));
    }

    public void DeleteEvent(LociEvent lociEvent)
    {
        if (_events.Remove(lociEvent))
        {
            _logger.LogDebug($"Deleted event {lociEvent.Title}.", LoggerType.DataManagement);
            _mediator.Publish(new LociEventChanged(FSChangeType.Deleted, lociEvent, null));
            _saver.Save(this);
        }
    }

    public void Save()
        => _saver.Save(this);

    #region HybridSavable
    public int ConfigVersion => 1;
    public int MaxBackups => 2;
    public HybridSaveType SaveType => HybridSaveType.Json;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string ToFilePath(FileProvider ser) => ser.EventsConfig;
    public void WriteToStream(StreamWriter writer) => throw new NotImplementedException();
    public string JsonSerialize()
    {
        // construct the config object to serialize.
        return new JObject()
        {
            ["Version"] = ConfigVersion,
            ["Events"] = JArray.FromObject(_events),
        }.ToString(Formatting.None); // No pretty formatting here.
    }

    public void Load()
    {
        var file = _fileNames.EventsConfig;
        _logger.LogInformation($"Loading EventsConfig for: {file}");
        _events.Clear();
        if (!File.Exists(file))
        {
            _logger.LogWarning($"No EventsConfig found at {file}");
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
                _logger.LogError("Invalid Version!");
                return;
        }
        // Update the saved data.
        _saver.Save(this);
    }

    private void LoadV1(JObject jObject)
    {
        // Load in as normal.
        _events = jObject["Events"]?.ToObject<List<LociEvent>>() ?? new List<LociEvent>();
    }
    #endregion HybridSavable
}