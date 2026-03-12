using CkCommons;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Loci.Data;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;
using Microsoft.Extensions.Hosting;
using OtterGui.String;
using OtterGui.Text.Widget.Editors;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;

namespace Loci.Processors;

public unsafe partial class LociMemory : IHostedService
{
    private readonly ILogger<LociMemory> _logger;
    private readonly LociMediator _mediator;
    private readonly MainConfig _config;
    private readonly LociEventData _eventData;
    public LociMemory(ILogger<LociMemory> logger, LociMediator mediator, MainConfig config, LociEventData eventData)
    {
        _logger = logger;
        _mediator = mediator;
        _config = config;
        _eventData = eventData;

        logger.LogInformation("Initializing Memory");
        Svc.Hook.InitializeFromAttributes(this);
        // Hook the function delegate as well.
        AtkComponentIconText_LoadIconByID = Marshal.GetDelegateForFunctionPointer<AtkComponentIconText_LoadIconByIDDelegate>(Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? 41 8D 45 3D"));
        ProcessEmoteHook = Svc.Hook.HookFromSignature<OnEmoteFuncDelegate>("E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", ProcessEmoteDetour);
        ProcessGearsetChangeHook = Svc.Hook.HookFromAddress<GearsetChangedDelegate>((nint)RaptureGearsetModule.MemberFunctionPointers.EquipGearsetInternal, GearsetChangedDetour);

        ReceiveAtkCompIconTxtEventHook.SafeEnable();
        ProcessEmoteHook.SafeEnable();
        ProcessGearsetChangeHook.SafeEnable();
        SheApplierHook.SafeEnable();
        BattleLog_AddToScreenLogWithScreenLogKindHook.SafeEnable();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Memory hooks enabled and delegates assigned.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Safe disable-dispose all hooks, and clear funcs.
        ReceiveAtkCompIconTxtEventHook.SafeDispose();
        ProcessEmoteHook.SafeDispose();
        ProcessGearsetChangeHook.SafeDispose();
        SheApplierHook.SafeDispose();
        BattleLog_AddToScreenLogWithScreenLogKindHook.SafeDispose();
        // Clear the function delegates.
        ReceiveAtkCompIconTxtEventHook = null!;
        ProcessEmoteHook = null!;
        SheApplierHook = null!;
        BattleLog_AddToScreenLogWithScreenLogKindHook = null!;
        AtkComponentIconText_LoadIconByID = null!;
        _logger.LogInformation("Memory hooks and delegates safely disposed and cleared.");
        return Task.CompletedTask;
    }


    // The delegate for loading an icon by its ID, used for the status icons in the target info and player info bars.
    public delegate nint AtkComponentIconText_LoadIconByIDDelegate(void* iconText, int iconId);
    // The hookable func for this delegate.
    internal static AtkComponentIconText_LoadIconByIDDelegate AtkComponentIconText_LoadIconByID = null!;

    // The delegate for receiving events when hovering over an icon in the positive, negative, or special status icons.
    public delegate void AtkComponentIconText_ReceiveEvent(nint a1, short a2, nint a3, nint a4, nint a5);
    [Signature("44 0F B7 C2 4D 8B D1", DetourName = nameof(ReceiveAtkCompIconTxtEventDetour), Fallibility = Fallibility.Auto)]
    internal static Hook<AtkComponentIconText_ReceiveEvent> ReceiveAtkCompIconTxtEventHook = null!;

    /// <summary>
    ///     Handles the detour of when we hover over an icon in our positive, negative, or special status icons.
    /// </summary>
    private void ReceiveAtkCompIconTxtEventDetour(nint a1, short a2, nint a3, nint a4, nint a5)
    {
        try
        {
            // _logger.LogDebug($"{a1:X16}, {a2}, {a3:X16}, {a4:X16}, {a5:X16}");
            if (a2 is 6)
                LociProcessor.HoveringOver = a1;

            if (a2 is 7)
                LociProcessor.HoveringOver = 0;

            // Handle Cancellation Request on Right Click
            if (a2 is 9 && LociProcessor.WasRightMousePressed)
            {
                // We dunno what status this is yet, so mark the address for next check.
                LociProcessor.CancelRequests.Add(a1);
                LociProcessor.HoveringOver = 0;
            }
        }
        catch (Bagagwa e)
        {
            _logger.LogError($"Error processing AtkCompIconTxtEventDetour: {e}");
        }
        // Ret the original, always
        ReceiveAtkCompIconTxtEventHook.Original(a1, a2, a3, a4, a5);
    }

    /// <summary>
    ///     For applying the SHE VFX.
    /// </summary>
    internal delegate nint SheApplier(string path, nint target, nint target2, float speed, char a5, UInt16 a6, char a7);
    [Signature("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 27 B2 01", DetourName = nameof(SheApplierDetour), Fallibility = Fallibility.Auto)]
    internal static Hook<SheApplier> SheApplierHook = null!;

    private nint SheApplierDetour(string path, nint target, nint target2, float speed, char a5, UInt16 a6, char a7)
    {
        try
        {
            _logger.LogInformation($"SheApplier {path}, {target:X16}, {target2:X16}, {speed}, {a5}, {a6}, {a7}", LoggerType.Memory);
        }
        catch (Bagagwa e)
        {
            _logger.LogError($"Error in SheApplierDetour: {e}");
        }
        return SheApplierHook.Original(path, target, target2, speed, a5, a6, a7);
    }

    /// <summary>
    ///     Spawn a SHE VFX by the iconID of the status effect.
    /// </summary>
    internal void SpawnSHE(uint iconID, nint target, nint target2, float speed = -1.0f, char a5 = char.MinValue, UInt16 a6 = 0, char a7 = char.MinValue)
    {
        try
        {
            string smallPath = GameDataHelp.GetVfxPathByID(iconID);
            if (smallPath.IsNullOrWhitespace())
            {
                _logger.LogInformation($"Path for IconID: {iconID} is empty", LoggerType.SheVfx);
                return;
            }
            _logger.LogTrace($"Path for IconID: {iconID} is: {smallPath}", LoggerType.SheVfx);
            SpawnSHE(smallPath, target, target2, speed, a5, a6, a7);
        }
        catch (Bagagwa e)
        {
            _logger.LogError($"Error in SpawnSHE: {e}");
        }
    }

    /// <summary>
    ///     spawn a SHE VFX by its path.
    /// </summary>
    internal void SpawnSHE(string path, nint target, nint target2, float speed = -1.0f, char a5 = char.MinValue, UInt16 a6 = 0, char a7 = char.MinValue)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogInformation($"Path for SHE is empty", LoggerType.SheVfx);
                return;
            }

            var fullPath = GameDataHelp.GetVfxPath(path);
            _logger.LogTrace($"Path for SHE is: {fullPath}", LoggerType.SheVfx);
            SheApplierHook.Original(fullPath, target, target2, speed, a5, a6, a7);
        }
        catch (Bagagwa e)
        {
            _logger.LogError($"Error in SpawnSHE: {e}");
        }
    }
}
#pragma warning restore CS0649 // Ignore "Field is never assigned to" warnings for IPC fields

