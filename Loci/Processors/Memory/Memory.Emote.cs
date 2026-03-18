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
        // Ensure the original fires normally.
        ProcessEmoteHook.Original(unk, emoteCallerAddr, emoteId, targetId, unk2);
        // Then validate and publish any valid interactions.
        var callerObj = (GameObject*)emoteCallerAddr;
        var targetObj = GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(targetId);

        if (callerObj is null) // Do not check target, it can be null if you emote nothing.
            return;

        _mediator.Publish(new EmotePerformedMessage(emoteId, (nint)callerObj, (nint)targetObj));
    }
}
