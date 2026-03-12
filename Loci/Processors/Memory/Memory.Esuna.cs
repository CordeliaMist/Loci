using CkCommons;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Loci.Data;
using Loci.Services;
using LociApi.Enums;
using System.Security.Cryptography.Xml;

namespace Loci.Processors;

// For Esuna Related stuff
public unsafe partial class LociMemory
{
    // Esuna, Medica -> CastID: 7568
    public delegate void BattleLog_AddToScreenLogWithScreenLogKind(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType);
    [Signature("48 85 C9 0F 84 ?? ?? ?? ?? 56 41 56", DetourName = nameof(BattleLog_AddToScreenLogWithScreenLogKindDetour), Fallibility = Fallibility.Auto)]
    internal static Hook<BattleLog_AddToScreenLogWithScreenLogKind> BattleLog_AddToScreenLogWithScreenLogKindHook = null!;
    public unsafe void BattleLog_AddToScreenLogWithScreenLogKindDetour(nint target, nint source, FlyTextKind kind, byte a4, byte a5, int actionID, int statusID, int stackCount, int damageType)
    {
        try
        {
            _logger.LogTrace($"BattleLog_AddActionLogMessageDetour: {target:X16}, {source:X16}, {kind}, {a4}, {a5}, {actionID}, {statusID}, {stackCount}, {damageType}", LoggerType.Memory);
            // If the Status can be Esunad
            if (_config.Current.AllowEsuna)
            {
                // If action is Esuna
                if (actionID == 7568 && kind is FlyTextKind.HasNoEffect)
                {
                    // Only check logic if the source and target are valid actors.
                    if (CharaWatcher.TryGetValue(source, out Character* chara) && CharaWatcher.TryGetValue(target, out Character* targetChara))
                    {
                        // Check permission (Must be allowing from others, or must be from self)
                        if (_config.Current.OthersCanEsuna || chara->ObjectIndex == 0)
                        {
                            // Grab the status manager. (Do not trigger on Ephemeral, wait for them to update via IPC)
                            if (LociManager.GetFromChara(targetChara) is { } manager && !manager.Ephemeral)
                            {
                                bool fromClient = chara->ObjectIndex == 0;

                                foreach (LociStatus status in manager.Statuses)
                                {
                                    // Ensure only negative statuses are dispelled.
                                    if (status.Type != StatusType.Negative) continue;
                                    // If it cannot be dispelled, skip it.
                                    else if (!status.Modifiers.Has(Modifiers.CanDispel)) continue;
                                    // Client cannot dispel locked statuses.
                                    else if (fromClient && manager.LockedStatuses.ContainsKey(status.GUID)) continue;
                                    // Prevent dispelling if not from client and others are not allowed.
                                    else if (!fromClient && !_config.Current.OthersCanEsuna) continue;
                                    // Others cannot dispel if they are not whitelisted.
                                    else if (!IsValidDispeller(status, chara)) continue;

                                    // Perform the dispel, expiring the timer. Also apply the chain if desired.
                                    status.ExpiresAt = 0;
                                    if (status.ChainedGUID != Guid.Empty && status.ChainTrigger is ChainTrigger.Dispel)
                                    {
                                        status.ApplyChain = true;
                                    }
                                    // This return is to not show the failed message
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Bagagwa e)
        {
            _logger.LogError($"Error in BattleLog_AddToScreenLogWithScreenLogKindDetour: {e}");
        }
        BattleLog_AddToScreenLogWithScreenLogKindHook.Original(target, source, kind, a4, a5, actionID, statusID, stackCount, damageType);
    }

    private static unsafe bool IsValidDispeller(LociStatus status, Character* chara)
        => status.Dispeller.Length is 0 || status.Dispeller == chara->GetNameWithWorld();
}
