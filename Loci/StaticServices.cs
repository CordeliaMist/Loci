using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Loci.Data;
using Lumina.Excel.Sheets;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Loci;

/// <summary>
///     A collection of internally handled Dalamud Interface static services
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
public class Svc
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IPluginLog Logger { get; set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; set; } = null!;
    [PluginService] public static IAddonEventManager AddonEventManager { get; private set; }
    [PluginService] public static IBuddyList Buddies { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; }
    [PluginService] public static ICondition Condition { get; private set; }
    [PluginService] public static IContextMenu ContextMenu { get; private set; }
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] public static IDutyState DutyState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] public static IGameLifecycle GameLifeCycle { get; private set; } = null!;
    [PluginService] public static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static INotificationManager Notifications { get; private set; } = null!;
    [PluginService] public static INamePlateGui NamePlate { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static IPartyList Party { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static ITargetManager Targets { get; private set; } = null!;
    [PluginService] public static ITextureProvider Texture { get; private set; } = null!;
    [PluginService] public static IToastGui Toasts { get; private set; } = null!;
}

// For the data that is only really initialized once based on the current client language.
public static class GameDataSvc
{
    public static ImmutableList<Emote> ValidEmoteCache { get; private set; }
    public static ImmutableList<ParsedEmote> ValidLightEmoteCache { get; private set; }
    public static FrozenDictionary<uint, ParsedEmote> EmoteData { get; private set; } = null!;
    public static FrozenDictionary<byte, ParsedOnlineStatus> OnlineStatus { get; private set; } = null!;
    public static FrozenDictionary<uint, string> JobData { get; private set; } = null!;
    public static FrozenDictionary<ushort, string> WorldData { get; private set; } = null!;
    public static FrozenDictionary<ushort, string> TerritoryData { get; private set; } = null!;

    public static bool _isInitialized = false;
    public static void Init(IDalamudPluginInterface pi)
    {
        if (_isInitialized)
            return;

        ValidEmoteCache = Svc.Data.GetExcelSheet<Emote>().Where(x => x.EmoteCategory.IsValid && !x.Name.ExtractText().IsNullOrWhitespace()).ToImmutableList();
        ValidLightEmoteCache = ValidEmoteCache.Select(x => new ParsedEmote(x)).ToImmutableList();
        EmoteData = Svc.Data.GetExcelSheet<Emote>()
            .Where(x => x.EmoteCategory.IsValid && !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
            .ToDictionary(e => e.RowId, e => new ParsedEmote(e))
            .ToFrozenDictionary();

        OnlineStatus = Svc.Data.GetExcelSheet<OnlineStatus>()
            .Where(x => !x.Unknown1 && !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
            .ToDictionary(s => (byte)s.RowId, s => new ParsedOnlineStatus(s))
            .ToFrozenDictionary();

        JobData = Svc.Data.GetExcelSheet<ClassJob>(Svc.ClientState.ClientLanguage)!
            .ToDictionary(k => k.RowId, k => k.NameEnglish.ToString())
            .ToFrozenDictionary();

        WorldData = Svc.Data.GetExcelSheet<World>(Svc.ClientState.ClientLanguage)!
            .Where(w => !w.Name.IsEmpty && w.DataCenter.RowId != 0 && (w.IsPublic || char.IsUpper(w.Name.ToString()[0])))
            .ToDictionary(w => (ushort)w.RowId, w => w.Name.ToString())
            .ToFrozenDictionary();

        TerritoryData = Svc.Data.GetExcelSheet<TerritoryType>(Svc.ClientState.ClientLanguage)!
            .Where(w => w.RowId != 0)
            .Select(w =>
            {
                var zoneName = w.PlaceName.ValueNullable?.Name.ToString();
                if (string.IsNullOrWhiteSpace(zoneName))
                    return null;

                if (w.ContentFinderCondition.ValueNullable is { } cfc)
                {
                    var cfcStr = cfc.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(cfcStr))
                        zoneName = $"{zoneName} ({cfcStr})";
                }

                return new
                {
                    Id = (ushort)w.RowId,
                    Name = zoneName
                };
            })
            .Where(x => x != null)
            .ToDictionary(x => x!.Id, x => x!.Name)
            .ToFrozenDictionary();

        // Init other data we want here later.

        _isInitialized = true;
    }

    public static void Dispose()
    {
        if (_isInitialized)
            return;

        JobData = null!;
        WorldData = null!;
        TerritoryData = null!;
        _isInitialized = false;
    }
}


