using CkCommons;
using CkCommons.HybridSaver;
using Dalamud.Game.ClientState.Conditions;
using Loci.Gui.Components;
using Loci.Services;

namespace Loci.Data;

public class ConfigStorage
{
    public Version? LastRunVersion { get; set; } = null;

    // Tab Selection Memory
    public LociUITabs.SelectedTab CurrentTab { get; set; } = LociUITabs.SelectedTab.Statuses;
    
    // General
    public bool OpenOnStartup { get; set; } = true;

    public bool Enabled { get; set; } = true;
    public bool SheVfxEnabled { get; set; } = true; // Enable SHE Status application
    public bool SheVfxRestricted { get; set; } = true; // Restricted to party, friends, and nearby only.
    public bool FlyText { get; set; } = true;
    public int FlyTextLimit { get; set; } = 10; // Within 5-20
    public bool OffInDuty { get; set; } = false;
    public bool OffInCombat { get; set; } = false;
    public int IconSelectorHeight { get; set; } = 33;
    public bool AllowEsuna { get; set; } = true;
    public bool OthersCanEsuna { get; set; } = true;
}

public class MainConfig : IHybridSavable
{
    private readonly ILogger<MainConfig> _logger;
    private readonly SaveService _saver;

    public MainConfig(ILogger<MainConfig> logger, SaveService saver)
    {
        _logger = logger;
        _saver = saver;
        Load();
    }

    public void Save() => _saver.Save(this);
    public void Load()
    {
        var file = _saver.FileNames.MainConfig;
        _logger.LogInformation("Loading in Config for file: " + file);
        if (!File.Exists(file))
        {
            _logger.LogWarning("Config file not found for: " + file);
            _saver.Save(this);
            return;
        }

        var jsonText = File.ReadAllText(file);
        var jObject = JObject.Parse(jsonText);
        var version = jObject["Version"]?.Value<int>() ?? 0;

            // Load instance configuration
        Current = jObject["Config"]?.ToObject<ConfigStorage>() ?? new ConfigStorage();

        // Load static fields safely
        LogLevel = Enum.TryParse(jObject["LogLevel"]?.Value<string>(), out LogLevel lvl) ? lvl : LogLevel.Trace;

        // Handle outdated hash set format, and new format for log filters.
        var token = jObject["Filters"];
        if(token is JArray array)
        {
            var list = array.ToObject<List<LoggerType>>() ?? new List<LoggerType>();
            LoggerFilters = list.Aggregate(LoggerType.None, (acc, val) => acc | val);
        }
        else
        {
            LoggerFilters = token?.ToObject<LoggerType>() ?? LoggerType.Recommended;
        }

        Save();
    }

    public ConfigStorage Current { get; private set; } = new();
    public Dictionary<LociCol, uint> LociColors { get; private set; } = [];
    public Dictionary<CkCol, uint> CkColors { get; private set; } = [];
    
    public static LogLevel LogLevel = LogLevel.Trace;
    public static LoggerType LoggerFilters = LoggerType.Recommended;

    public bool CanLociModifyUI()
    {
        if (!Current.Enabled)
            return false;

        if (!Current.OffInDuty && Svc.Condition[ConditionFlag.BoundByDuty] || Svc.Condition[ConditionFlag.BoundByDuty56] || Svc.ClientState.IsPvP)
            return false;

        if (!Current.OffInCombat && Svc.Condition[ConditionFlag.InCombat])
            return false;
        // Otherwise, valid!
        return true;
    }

    // Hybrid Savable stuff
    [JsonIgnore] public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    [JsonIgnore] public HybridSaveType SaveType => HybridSaveType.Json;
    public int ConfigVersion => 0;
    public string GetFileName(FileProvider files, out bool upa) => (upa = false, files.MainConfig).Item2;
    public void WriteToStream(StreamWriter writer) => throw new NotImplementedException();
    public string JsonSerialize()
    {
        return new JObject()
        {
            ["Version"] = ConfigVersion,
            ["Config"] = JObject.FromObject(Current),
            ["LogLevel"] = LogLevel.ToString(),
            ["Filters"] = JToken.FromObject(LoggerFilters)
        }.ToString(Formatting.Indented);
    }

}
