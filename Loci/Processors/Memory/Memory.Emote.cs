using CkCommons;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;
using Microsoft.Extensions.Hosting;
using OtterGui.Text.Widget.Editors;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;

namespace Loci.Processors;

// Emote Related
public unsafe partial class LociMemory
{
    public delegate void OnEmoteFuncDelegate(ulong unk, ulong emoteCallerAddr, ushort emoteId, ulong targetId, ulong unk2);
    internal static Hook<OnEmoteFuncDelegate> ProcessEmoteHook = null!;

    /// <summary>
    ///     Processes who did what emote for achievement and trigger purposes.
    ///     Provides the source and target along with the emote ID.
    /// </summary>
    private unsafe void ProcessEmoteDetour(ulong unk, ulong emoteCallerAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            // Cast it to a gameobject and validate it.
            GameObject* emoteCaller = (GameObject*)emoteCallerAddr;
            if (emoteCaller == null)
                throw new Bagagwa("Emote Caller GameObject is null in EmoteDetour.");

            var tgtObj = GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(targetId);

            if ((MainConfig.LoggerFilters & LoggerType.Memory) != 0)
            {
                var emoteCallerName = (emoteCaller->IsCharacter()) ? ((Character*)emoteCaller)->GetNameWithWorld() : "No Player Was Emote Caller";
                var emoteName = GameDataSvc.EmoteData.TryGetValue(emoteId, out var data) ? data.Name : "UNK";
                var targetName = (tgtObj != null && tgtObj->IsCharacter()) ? ((Character*)tgtObj)->GetNameWithWorld() : "No Player Was Target";
                _logger.LogTrace($"OnEmote >> [{emoteCallerName}] used Emote [{emoteName}](ID:{emoteId}) on Target: [{targetName}]", LoggerType.Memory);
            }

            CheckEmoteEvents(emoteId, emoteCaller, tgtObj);
        }
        catch (Bagagwa e)
        {
            _logger.LogError(e, "Error in EmoteDetour");
        }

        ProcessEmoteHook.Original(unk, emoteCallerAddr, emoteId, targetId, unk2);
    }

    private void CheckEmoteEvents(ushort emoteId, GameObject* source, GameObject* target)
    {
        var callerAddr = (nint)source;
        var targetAddr = (nint)target;
        // Caller must be something, (Target can be nothing)
        if (!CharaWatcher.Rendered.Contains(callerAddr))
            return;

        // Filter based on the type.
        var isClientRendered = PlayerData.Available;
        var clientIsCaller = isClientRendered && callerAddr == PlayerData.Address;
        var clientIsTarget = isClientRendered && targetAddr == PlayerData.Address;

        // Get the health triggers scoped down to this person we are monitoring.
        var emoteEvents = LociEventData.Events
            .Where(IsValidMatch)
            .OrderByDescending(e => e.Priority)
            .ToList();

        _logger.LogTrace($"Found {emoteEvents.Count} entries to iterate", LoggerType.Memory);
        emoteEvents.ApplyFirstMatch();

        bool IsValidMatch(LociEvent ee)
        {
            if (!ee.Enabled || ee.EventType is not LociEventType.Emote || ee.IndicatedID != emoteId)
                return false;

            switch (ee.Direction)
            {
                case KnownDirection.Any:
                    return true;

                case KnownDirection.OtherToSelf:
                    // Ensure valid states.
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && !clientIsCaller && clientIsTarget))
                        return false;
                    // If the target was defined, ensure it matches.
                    return !string.IsNullOrEmpty(ee.WhitelistedName)
                        ? Utils.ToLociName((Character*)callerAddr) == ee.WhitelistedName
                        : true;

                case KnownDirection.Other:
                    // Ensure valid states.
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && !clientIsCaller))
                        return false;
                    // If the target was defined, ensure it matches.
                    return !string.IsNullOrEmpty(ee.WhitelistedName)
                        ? Utils.ToLociName((Character*)targetAddr) == ee.WhitelistedName
                        : true;

                case KnownDirection.SelfToOther:
                    // Ensure valid states.
                    if (!(CharaWatcher.Rendered.Contains(targetAddr) && clientIsCaller))
                        return false;
                    // If the target was defined, ensure it matches.
                    return !string.IsNullOrEmpty(ee.WhitelistedName)
                        ? Utils.ToLociName((Character*)targetAddr) == ee.WhitelistedName
                        : true;

                case KnownDirection.Self:
                    return clientIsCaller;

                default:
                    return false;
            }
        }
    }
}
