using CkCommons;
using CkCommons.Helpers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;
using Loci.Interop;
using Loci.Services.Mediator;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Hosting;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Loci.Processors;

/// <summary>
///     The core processor for the Loci Module
/// </summary>
public class LociProcessor : DisposableMediatorSubscriberBase, IHostedService
{
    private readonly MainConfig _config;
    private readonly LociMemory _lociMemory;
    private readonly PartyListProcessor _partyList;
    private readonly StatusProcessor _statuses;
    private readonly StatusCustomProcessor _statusesCustom;
    private readonly TargetInfoProcessor _targetInfo;
    private readonly FocusTargetInfoProcessor _focusTargetInfo;
    private readonly TargetInfoBuffDebuffProcessor _targetInfoBuffDebuff;
    private readonly FlyPopupTextProcessor _flyText;
    private readonly LociManager _manager;

    private static nint _tooltipMemory;

    public LociProcessor(ILogger<LociProcessor> logger, LociMediator mediator, MainConfig config,
        LociMemory memory, PartyListProcessor party, StatusProcessor statuses, StatusCustomProcessor customs,
        TargetInfoProcessor targetInfo, FocusTargetInfoProcessor ftInfo, TargetInfoBuffDebuffProcessor bdtInfo,
        FlyPopupTextProcessor flyPopupText, LociManager manager)
        : base(logger, mediator)
    {
        _config = config;
        _lociMemory = memory;
        _partyList = party;
        _statuses = statuses;
        _statusesCustom = customs;
        _targetInfo = targetInfo;
        _focusTargetInfo = ftInfo;
        _targetInfoBuffDebuff = bdtInfo;
        _flyText = flyPopupText;
        _manager = manager;

        Mediator.Subscribe<EnabledStateChanged>(this, _ => 
        {
            unsafe
            {
                if (!_.NewState)
                    HideAll();
            }
        });

        _tooltipMemory = Marshal.AllocHGlobal(2 * 1024);
        Svc.Framework.Update += OnTick;
    }

    // Static readonly data
    public static readonly List<string> StatusEffectPaths = ["Clear"];
    public static readonly HashSet<uint> NegativeStatuses = [];
    public static readonly HashSet<uint> PositiveStatuses = [];
    public static readonly HashSet<uint> SpecialStatuses = [];
    public static readonly HashSet<uint> DispelableIcons = [];
    public static readonly Dictionary<uint, uint> IconStackCounts = [];

    // Static interactable data for this singleton LociProcessor.
    public static nint HoveringOver = 0;
    public static List<nint> CancelRequests = [];
    public static bool WasRightMousePressed = false;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var x in Svc.Data.GetExcelSheet<Status>())
        {
            if (IconStackCounts.TryGetValue(x.Icon, out var count))
            {
                if (count < x.MaxStacks)
                    IconStackCounts[x.Icon] = x.MaxStacks;
            }
            else
            {
                IconStackCounts[x.Icon] = x.MaxStacks;
            }

            var fxpath = x.HitEffect.ValueNullable?.Location.ValueNullable?.Location.ExtractText();
            if (!fxpath.IsNullOrWhitespace() && !StatusEffectPaths.Contains(fxpath))
                StatusEffectPaths.Add(fxpath);

            if (NegativeStatuses.Contains(x.RowId) || PositiveStatuses.Contains(x.RowId) || SpecialStatuses.Contains(x.RowId))
                continue;

            if (x.CanIncreaseRewards == 1)
                SpecialStatuses.Add(x.RowId);
            else if (x.StatusCategory == 1)
                PositiveStatuses.Add(x.RowId);
            else if (x.StatusCategory == 2)
            {
                NegativeStatuses.Add(x.RowId);
                DispelableIcons.Add(x.Icon);
                for (var i = 1; i < x.MaxStacks; i++)
                    DispelableIcons.Add((uint)(x.Icon + i));
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Disposing LociProcessor...");
        Svc.Framework.Update -= OnTick;

        Svc.Framework.RunOnFrameworkThread(() =>
        {
            HideAll();
            _partyList.Dispose();
            _statuses.Dispose();
            _statusesCustom.Dispose();
            _targetInfo.Dispose();
            _focusTargetInfo.Dispose();
            _targetInfoBuffDebuff.Dispose();
            _flyText.Dispose();
            Marshal.FreeHGlobal(_tooltipMemory);
        });
        return Task.CompletedTask;
    }

    public void HideAll()
    {
        _partyList.HideAll();
        _statuses.HideAll();
        _statusesCustom.HideAll();
        _targetInfo.HideAll();
        _focusTargetInfo.HideAll();
        _targetInfoBuffDebuff.HideAll();
    }

    private List<(nint CharaAddr, string customPath)> SHECandidates = [];
    private unsafe void OnTick(IFramework _)
    {
        // List of VFX that should be handled by the StatusHitEffect.
        SHECandidates.Clear();

        if (HoveringOver != 0)
        {
            if (KeyMonitor.IsKeyPressed((int)Keys.LButton))
                WasRightMousePressed = false;

            if (KeyMonitor.IsKeyPressed((int)Keys.RButton))
                WasRightMousePressed = true;
        }

        // Only process the managers that need updates to save on performance
        // (Do this later when we can test without impacting everyone by accident)


        // Iterate through them
        foreach (var (ownerNameWorld, sm) in LociManager.Managers)
        {
            var removed = new List<LociStatus>();
            var doChainApply = new List<LociStatus>();

            foreach (var x in sm.Statuses)
            {
                if (x.ClickedOff && sm.LockedStatuses.ContainsKey(x.GUID))
                {
                    x.ClickedOff = false;
                    continue;
                }

                // Deterministic Logic
                if (x.ShouldExpireOnChain())
                    x.ExpiresAt = 0;
                
                if (x.HadNaturalTimerFalloff() && x.ChainTrigger is ChainTrigger.TimerExpired)
                    x.ApplyChain = true;

                // Get the expire time.
                bool timeExpired = x.ExpiresAt - Utils.Time <= 0;
                                
                // Process status removal.
                if (timeExpired || x.ClickedOff)
                {
                    EnsureRemTextWasShown(sm, x);
                    removed.Add(x);
                }
                else
                {
                    EnsureAddTextWasShown(sm, x);
                }
                // Mark the status to apply the chain, then reset the flag.
                if (x.ApplyChain)
                {
                    doChainApply.Add(x);
                    x.ApplyChain = false;
                }
            }

            HandleSHECandidates();

            // Now process the removal of all statuses marked.
            // (This allows for chains to be applied without removing original if desired)
            if (removed.Count > 0)
                foreach (var status in removed)
                    sm.Remove(status);

            // Handle any status chaining logic.
            if (doChainApply.Count > 0)
                foreach (var status in doChainApply)
                    HandleChaining(sm, status);

            // Handle any other SHECandidates processing removed and chain applications.
            if (removed.Count > 0 || doChainApply.Count > 0)
                HandleSHECandidates();

            // Handle event firing.
            if (sm.NeedFireEvent)
            {
                sm.NeedFireEvent = false;
                // If the status manager owner exists, we can mark them as modified.
                if (sm.Owner is not null)
                {
                    try
                    {
                        IpcProvider.OnSMModified((nint)sm.Owner);
                    }
                    catch (Bagagwa e)
                    {
                        Logger.LogWarning($"Something went wrong on LociSMModified IPCEvent!\n{e.Message}\n" +
                            $"One of your Plugins may have outdated IPC parameters for this IPCEvent");
                    }
                }
            }
        }

        // Clear any remaining Cancel Requests not yet processed before iterating SHECandidates
        CancelRequests.Clear();
    }

    // Helper function to process the SHECandidates.
    private unsafe void HandleSHECandidates()
    {
        foreach (var x in SHECandidates)
        {
            Character* chara = (Character*)x.CharaAddr;
            if (ShouldSpawnHitEffect(chara, x.customPath))
            {
                Logger.LogDebug($"StatusHitEffect on: {chara->NameString} / {x.customPath}");
                if (x.customPath == "kill")
                    _lociMemory.SpawnSHE("dk04ht_canc0h", x.CharaAddr, x.CharaAddr, -1, char.MinValue, 0, char.MinValue);
                else
                    _lociMemory.SpawnSHE(x.customPath, x.CharaAddr, x.CharaAddr, -1, char.MinValue, 0, char.MinValue);
            }
            else
            {
                Logger.LogDebug($"SHE skipped on: {chara->NameString} / {x.customPath}", LoggerType.Processors);
            }
        }
        SHECandidates.Clear();
    }

    // Helper function to ensure the add text is shown for a status, if it should be.
    private unsafe void EnsureAddTextWasShown(ActorSM manager, LociStatus status)
    {
        if (manager.AddTextShown.Contains(status.GUID))
            return;

        if (_config.CanLociModifyUI() && manager.OwnerValid)
        {
            if (manager.Owner->CanSpawnFlyText())
                _flyText.Enqueue(new(status, true, manager.Owner->EntityId));

            if (manager.Owner->CanSpawnVFX())
            {
                if (!SHECandidates.Any(s => s.CharaAddr == (nint)manager.Owner))
                {
                    var vfxPath = string.IsNullOrWhiteSpace(status.CustomFXPath) ? GameDataHelp.GetVfxPathByID((uint)status.IconID) : status.CustomFXPath;
                    SHECandidates.Add(((nint)manager.Owner, vfxPath));
                }
            }
        }
        manager.AddTextShown.Add(status.GUID);
    }

    private unsafe void EnsureRemTextWasShown(ActorSM manager, LociStatus status)
    {
        if (manager.RemTextShown.Contains(status.GUID))
            return;

        if (_config.CanLociModifyUI() && manager.Owner is not null)
        {
            if (manager.Owner->CanSpawnFlyText())
                _flyText.Enqueue(new(status, false, manager.Owner->EntityId));

            if (manager.Owner->CanSpawnVFX())
                if (!SHECandidates.Any(s => s.CharaAddr == (nint)manager.Owner))
                    SHECandidates.Add(((nint)manager.Owner, "kill"));
        }

        manager.RemTextShown.Add(status.GUID);
    }

    private void HandleChaining(ActorSM manager, LociStatus cur)
    {
        switch (cur.ChainedType)
        {
            case ChainType.Status:
                ChainStatus(manager, cur);
                break;
            case ChainType.Preset:
                ChainPreset(manager, cur);
                break;
            default:
                break;
        }
    }

    private unsafe void ChainStatus(ActorSM manager, LociStatus cur)
    {
        // Fail if not found
        if (LociData.Statuses.FirstOrDefault(s => s.GUID == cur.ChainedGUID) is not { } status)
            return;

        // Get old max stacks
        int oldMax = IconStackCounts.TryGetValue((uint)cur.IconID, out var oCount) ? (int)oCount : 1;
        // Aquire the new chained status to be applied.
        LociStatus? newStatus = manager.AddOrUpdate(status.PreApply());
        // If the new status if not valid just fail this process.
        if (newStatus is null)
            return;
        // Get the new max stacks, and if stackable, transfer stack logic.
        int newMaxStacks = IconStackCounts.TryGetValue((uint)newStatus.IconID, out var nCount) ? (int)nCount : 1;
        if (newMaxStacks > 1)
        {
            if (cur.Modifiers.Has(Modifiers.StacksCarryToChain))
            {
                // Use (oldMax - 1) here because our stacks always start at 1, not 0. So if it has 8 stacks, it can only increment 7 times.
                var toCarryOver = (cur.Stacks + cur.StackSteps) - (oldMax - 1);
                newStatus.Stacks = Math.Min(newStatus.Stacks - newStatus.StackSteps + toCarryOver, newMaxStacks);
            }
            else if (cur.Modifiers.Has(Modifiers.StacksMoveToChain))
                newStatus.Stacks = Math.Min(oldMax, newMaxStacks);
        }

        // Fix ensuring cap is hit when the chain trigger is max stacks.
        if (cur.ChainTrigger is ChainTrigger.HitMaxStacks)
        {
            cur.Stacks = cur.Modifiers.Has(Modifiers.StacksRollOver) ? Math.Clamp((cur.Stacks + cur.StackSteps) - oldMax, 1, oldMax) : oldMax;
            manager.AddTextShown.Remove(cur.GUID);
            EnsureAddTextWasShown(manager, cur);
        }

        // Ensure the add text is shown for this newly chained status, and then break out.
        EnsureAddTextWasShown(manager, status);
    }

    private unsafe void ChainPreset(ActorSM manager, LociStatus cur)
    {
        // Fail if not found
        if (LociData.Presets.FirstOrDefault(p => p.GUID == cur.ChainedGUID) is not { } preset)
            return;
        // Get old max stacks
        int oldMax = IconStackCounts.TryGetValue((uint)cur.IconID, out var oCount) ? (int)oCount : 1;
        // Apply the new preset
        manager.ApplyPreset(preset);

        // Dont worry about rolling stacks over since we dont really control that here.

        // But do worry about hitting max stacks.
        if (cur.ChainTrigger is ChainTrigger.HitMaxStacks)
        {
            cur.Stacks = cur.Modifiers.Has(Modifiers.StacksRollOver) ? Math.Clamp((cur.Stacks + cur.StackSteps) - oldMax, 1, oldMax) : oldMax;
            manager.AddTextShown.Remove(cur.GUID);
            EnsureAddTextWasShown(manager, cur);
        }

        // Ensuring the add text for the preset statuses is already handled here.
    }

    private unsafe bool ShouldSpawnHitEffect(Character* chara, string vfxPath)
    {
        if (!_config.Current.SheVfxEnabled)
            return false;

        // If our vfx spawning is not restricted, assume true
        if (!_config.Current.SheVfxRestricted)
            return true;

        // Otherwise it's limited
        if ((nint)chara == PlayerData.Address)
            return true;
        // Otherwise, they must be in our friendlist...
        if (Utils.GetFriendlist().Contains(chara->GetNameWithWorld()))
            return true;
        // Or our visible party...
        if (Utils.GetVisibleParty().Any(pm => pm == (nint)chara))
            return true;
        // Or within a close enough distance to us...
        if (Vector3.Distance(PlayerData.Character->Position, chara->Position) < 15f)
            return true;

        return false;
    }

    // Update to include the status manager parent.
    public static unsafe void SetIcon(AtkUnitBase* addon, AtkResNode* container, LociStatus status, ActorSM sm)
    {
        // If the container is not visible, make it visible so we can set the icon. We will toggle it back to hidden at the end if needed.
        if (!container->IsVisible())
            container->NodeFlags ^= NodeFlags.Visible;

        // Load the icon into the component container.
        LociMemory.AtkComponentIconText_LoadIconByID(container->GetAsAtkComponentNode()->Component, (int)status.AdjustedIconID);

        var dispelNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[0];

        // Make it not marked as dispelable if it is not part of the dispelable icons cache.
        if (status.Modifiers.Has(Modifiers.CanDispel) && !DispelableIcons.Contains((uint)status.IconID))
            status.Modifiers.Set(Modifiers.CanDispel, false);

        // Toggle visibility if it does not match the dispel nodes visibility
        if (status.Modifiers.Has(Modifiers.CanDispel) != dispelNode->IsVisible())
            dispelNode->NodeFlags ^= NodeFlags.Visible;

        // Ensure that the text is properly updated, including the timer text.
        var textNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[2];
        var timerText = "";
        if (status.ExpiresAt != long.MaxValue)
        {
            var rem = status.ExpiresAt - Utils.Time;
            timerText = rem > 0 ? GetTimerText(rem) : "";
        }

        // If there is timer text...
        if (timerText is not null)
        {
            // Then if the text node is not visible, make it visible so we can set the text.
            if (!textNode->IsVisible())
                textNode->NodeFlags ^= NodeFlags.Visible;
        }

        // Encode the text into the status
        var t = textNode->GetAsAtkTextNode();
        t->SetText((timerText ?? SeString.Empty).Encode());

        // Use a different color when applied by the client
        if (status.Applier == PlayerData.NameWithWorld)
        {
            t->TextColor = CreateColor(0xc9ffe4ff);
            t->EdgeColor = CreateColor(0x0a5f24ff);
            t->BackgroundColor = CreateColor(0);
        }
        else
        {
            t->TextColor = CreateColor(0xffffffff);
            t->EdgeColor = CreateColor(0x333333ff);
            t->BackgroundColor = CreateColor(0);
        }

        var addr = (nint)(container->GetAsAtkComponentNode()->Component);
        // _logger.LogDebug($"- = - {MemoryHelper.ReadStringNullTerminated((nint)(addon->Name))} - = -");
        
        // Process how we handle the hovered tooltip
        if (HoveringOver == addr && status.TooltipShown == -1)
        {
            // Update tooltips across the entire status manager
            foreach (var manager in LociManager.Managers)
                foreach (var s in manager.Value.Statuses)
                    s.TooltipShown = -1;
            
            // Then hide the tooltip for this ID.
            status.TooltipShown = addon->Id;
            AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            
            // Get what to write to this tooltip for the title and description, using our tooltip memory
            var str = status.Title;
            if (status.Description != "")
                str += $"\n{status.Description}";

            // write out the string to memory
            MemoryHelper.WriteSeString(_tooltipMemory, Utils.ParseBBSeString(str));
            AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, container, (byte*)_tooltipMemory);
        }

        // Hide it if no longer hovering the status
        if (status.TooltipShown == addon->Id && HoveringOver != addr)
        {
            //_logger.LogDebug($"Trigger 1 {addr:X16} / {Utils.Frame} / {GetCallStackID()}");
            status.TooltipShown = -1;
            if (HoveringOver == 0)
            {
                //_logger.LogDebug($"Trigger 2 / {Utils.Frame} / {GetCallStackID()}");
                AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            }
        }

        // If we requested to cancel this via a right click, then flag it for this.
        if (CancelRequests.Remove(addr))
        {
            // Move hiding the mouseover to here so we can reference the status that is removed.
            var name = addon->NameString;
            if (!sm.Ephemeral && sm.OwnerValid && sm.Owner->ObjectKind is FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Companion)
            {
                // Allow for companions that are non-ephemeral
                if (name == "_FocusTargetInfo" || name == "_TargetInfo")
                    status.ClickedOff = true;
            }
            else if (name.StartsWith("_StatusCustom") || name == "_Status")
                status.ClickedOff = true;
        }
    }

    public static string GetTimerText(long rem)
    {
        var seconds = MathF.Ceiling((float)rem / 1000f);
        if (seconds <= 59)
            return seconds.ToString();
        
        var minutes = MathF.Floor((float)seconds / 60f);
        if (minutes <= 59)
            return $"{minutes}m";
        
        var hours = MathF.Floor((float)minutes / 60f);
        if (hours <= 59)
            return $"{hours}h";
        
        var days = MathF.Floor((float)hours / 24f);
        if (days <= 9)
            return $"{days}d";
        
        return $">9d";
    }

    private unsafe static ByteColor CreateColor(uint color)
    {
        color = BinaryPrimitives.ReverseEndianness(color);
        var ptr = &color;
        return *(ByteColor*)ptr;
    }
}
