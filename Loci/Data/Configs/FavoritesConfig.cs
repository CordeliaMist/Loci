using CkCommons.HybridSaver;
using Loci.Services;

namespace Loci.Data;

// Enum selector for hashsets of GUIDs
public enum StarType
{
    Status,
    Preset,
    Event,
}
// Defined internally via StreamWrite for ease of use.
public class FavoritesConfig : IHybridSavable
{
    private readonly ILogger<FavoritesConfig> _logger;
    private readonly SaveService _saver;
    public int ConfigVersion => 0;
    public HybridSaveType SaveType => HybridSaveType.StreamWrite;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(FileProvider ser, out bool upa) => (upa = false, ser.Favorites).Item2;
    public string JsonSerialize() => throw new NotImplementedException();
    public FavoritesConfig(ILogger<FavoritesConfig> logger, SaveService saver)
    {
        _logger = logger;
        _saver = saver;
        Load();
    }

    public static readonly HashSet<Guid>    Statuses    = [];
    public static readonly HashSet<Guid>    Presets     = [];
    public static readonly HashSet<Guid>    Events      = [];
    public static readonly HashSet<uint>    IconIDs     = [];

    public void Load()
    {
        var file = _saver.FileNames.Favorites;
        _logger.LogInformation($"Loading Config file: {file}");
        if (!File.Exists(file))
        {
            _logger.LogWarning($"No Config found at {file}");
            _saver.Save(this);
            return;
        }

        try
        {
            var load = JsonConvert.DeserializeObject<LoadIntermediary>(File.ReadAllText(file));
            if (load is null)
                throw new Bagagwa("Failed to load favorites.");
            // Load favorites.
            // (No Migration Needed yet).
            Statuses.UnionWith(load.Statuses);
            Presets.UnionWith(load.Presets);
            Events.UnionWith(load.Events);
            IconIDs.UnionWith(load.IconIDs);
        }
        catch (Bagagwa e)
        {
            _logger.LogError(e, "Failed to load favorites.");
        }
    }

    public bool Favorite(StarType type, Guid id)
    {
        var res = type switch
        {
            StarType.Status => Statuses.Add(id),
            StarType.Preset => Presets.Add(id),
            StarType.Event => Events.Add(id),
            _ => false
        };
        if (res)
            _saver.Save(this);
        return res;
    }

    public bool Favorite(uint iconId)
    {
        if (IconIDs.Add(iconId))
        {
            _saver.Save(this);
            return true;
        }
        return false;
    }

    public void FavoriteBulk(StarType type, IEnumerable<Guid> ids)
    {
        switch (type)
        {
            case StarType.Status:
                Statuses.UnionWith(ids);
                break;
            case StarType.Preset:
                Presets.UnionWith(ids);
                break;
            case StarType.Event:
                Events.UnionWith(ids);
                break;
        }
        _saver.Save(this);
    }

    public void FavoriteBulk(IEnumerable<uint> iconIds)
    {
        IconIDs.UnionWith(iconIds);
        _saver.Save(this);
    }


    public bool Unfavorite(StarType type, Guid id)
    {
        var res = type switch
        {
            StarType.Status => Statuses.Remove(id),
            StarType.Preset => Presets.Remove(id),
            StarType.Event => Events.Remove(id),
            _ => false
        };
        if (res)
            _saver.Save(this);
        return res;
    }

    public bool Unfavorite(uint iconId)
    {
        if (IconIDs.Remove(iconId))
        {
            _saver.Save(this);
            return true;
        }
        return false;
    }

    public void ToggleFavorite(StarType type, Guid id)
    {
        switch (type)
        {
            case StarType.Status:
                if (!Statuses.Remove(id))
                    Statuses.Add(id);
                break;
            case StarType.Preset:
                if (!Presets.Remove(id))
                    Presets.Add(id);
                break;
            case StarType.Event:
                if (!Events.Remove(id))
                    Events.Add(id);
                break;

        }
        _saver.Save(this);
    }

    #region Saver
    public void WriteToStream(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        j.WriteStartObject();

        j.WritePropertyName(nameof(LoadIntermediary.Version));
        j.WriteValue(ConfigVersion);

        j.WritePropertyName(nameof(LoadIntermediary.Statuses));
        j.WriteStartArray();
        foreach (var status in Statuses)
            j.WriteValue(status);
        j.WriteEndArray();

        j.WritePropertyName(nameof(LoadIntermediary.Presets));
        j.WriteStartArray();
        foreach (var preset in Presets)
            j.WriteValue(preset);
        j.WriteEndArray();

        j.WritePropertyName(nameof(LoadIntermediary.Events));
        j.WriteStartArray();
        foreach (var evt in Events)
            j.WriteValue(evt);
        j.WriteEndArray();

        j.WritePropertyName(nameof(LoadIntermediary.IconIDs));
        j.WriteStartArray();
        foreach (var iconId in IconIDs)
            j.WriteValue(iconId);
        j.WriteEndArray();

        j.WriteEndObject();
    }
    #endregion Saver

    // Used to help with object based deserialization from the json loader.
    private class LoadIntermediary
    {
        public int Version = 1;
        public IEnumerable<Guid> Statuses = [];
        public IEnumerable<Guid> Presets  = [];
        public IEnumerable<Guid> Events   = [];
        public IEnumerable<uint> IconIDs  = [];
    }
}
