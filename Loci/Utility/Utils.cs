using CkCommons;
using CkCommons.Gui;
using CkCommons.Helpers;
using CkCommons.RichText;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Loci.Data;
using Loci.Services;
using LociApi.Enums;
using Lumina.Excel;
using Lumina.Extensions;
using MemoryPack;
using System.Runtime.CompilerServices;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Loci;

public static class Utils
{
    internal static readonly MemoryPackSerializerOptions SerializerOptions = new() { StringEncoding = StringEncoding.Utf16 };
    internal static readonly IEnumerable<ChainTrigger> ChainTypes = Enum.GetValues<ChainTrigger>().ToHashSet();
    internal static readonly IEnumerable<ChainTrigger> ChainTypesNoStk = [ChainTrigger.TimerExpired, ChainTrigger.Dispel];

    internal static Dictionary<uint, StatusIconData?> IconInfoCache = [];

    /// <summary>
    ///     Attempt to get an analysis of the StatusIconData from the given IconID.
    /// </summary>
    public static StatusIconData? GetIconData(uint iconID)
    {
        if (IconInfoCache.TryGetValue(iconID, out var iconInfo))
            return iconInfo;
        // Otherwise, create it or return null if not in the lookup.
        if (Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>().TryGetFirst(x => x.Icon == iconID, out var status))
        {
            var iconData = new StatusIconData(status);
            IconInfoCache[iconID] = iconData;
            return iconData;
        }
        else
        {
            IconInfoCache[iconID] = null;
            return null;
        }
    }

    public static long Time => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    public static unsafe ulong Frame => CSFramework.Instance()->FrameCounter;

    /// <summary>
    ///     Serializes and then deserializes object, returning result of deserialization using <see cref="Newtonsoft.Json"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NewtonsoftDeepClone<T>(this T obj)
    {
        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj))!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DeepClone<T>(this T obj)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(obj))!;
    }

    public unsafe static string ToLociName(Character* chara) => chara->ObjectKind switch
    {
        ObjectKind.Pc => chara->GetNameWithWorld(),
        ObjectKind.Companion => $"{((Companion*)chara)->Owner->NameString}s {chara->NameString}",
        _ => string.Empty
    };

    /// <summary>
    ///     Prepares to apply a LociStatus with the given preparation options. 
    /// </summary>
    public unsafe static LociStatus PreApply(this LociStatus status, params PrepareOptions[] opts)
    {
        var addr = IntPtr.Zero;
        var chara = (Character*)addr;
        status = status.NewtonsoftDeepClone();
        if (opts.Contains(PrepareOptions.ChangeGUID))
            status.GUID = Guid.NewGuid();
        // Update the persistent status.
        status.ExpiresAt = status.NoExpire ? long.MaxValue : Time + status.TotalMilliseconds;
        return status;
    }

    public static LociStatus ToSavedStatus(this LociStatusInfo statusInfo)
    {
        var totalTime = statusInfo.ExpireTicks == -1 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(statusInfo.ExpireTicks);
        return new LociStatus
        {
            GUID = statusInfo.GUID,
            IconID = statusInfo.IconID,
            Title = statusInfo.Title,
            Description = statusInfo.Description,
            CustomFXPath = statusInfo.CustomVFXPath,

            Type = statusInfo.Type,
            Stacks = statusInfo.Stacks,
            StackSteps = statusInfo.StackSteps,
            StackToChain = statusInfo.StackToChain,
            Modifiers = (Modifiers)statusInfo.Modifiers,

            ChainedGUID = statusInfo.ChainedGUID,
            ChainedType = statusInfo.ChainType,
            ChainTrigger = statusInfo.ChainTrigger,

            Applier = statusInfo.Applier,
            Dispeller = statusInfo.Dispeller,

            // Additional variables we can run assumptions on.
            Days = totalTime.Days,
            Hours = totalTime.Hours,
            Minutes = totalTime.Minutes,
            Seconds = totalTime.Seconds,
            NoExpire = statusInfo.ExpireTicks == -1,
        };
    }

    public static LociPreset ToSavedPreset(this LociPresetInfo presetInfo)
        => new LociPreset
        {
            GUID = presetInfo.GUID,
            Title = presetInfo.Title,
            Description = presetInfo.Description,
            Statuses = presetInfo.Statuses,
            ApplyType = (PresetApplyType)presetInfo.ApplicationType
        };

    public static LociEvent ToSavedEvent(this LociEventInfo eventInfo)
        => new LociEvent
        {
            GUID = eventInfo.GUID,
            Enabled = eventInfo.Enabled,
            Title = eventInfo.Title,
            Description = eventInfo.Description,
            EventType = eventInfo.EventType
        };

    public static void ApplyFirstMatch(this List<LociEvent> candidates)
    {
        // Invoke the first match.
        foreach (var match in candidates)
        {
            if (match.ReactionType is ChainType.Status)
            {
                if (LociData.Statuses.FirstOrDefault(s => s.GUID == match.ReactionGUID) is not { } data)
                    continue;
                // Fail if already present.
                if (LociManager.ClientSM.Statuses.FirstOrDefault(s => s.GUID == match.ReactionGUID) is not null && !data.Modifiers.Has(Modifiers.StacksIncrease))
                    continue;
                // Apply
                LociManager.ClientSM.AddOrUpdate(data.PreApply());
                break;
            }
            else
            {
                if (LociData.Presets.FirstOrDefault(s => s.GUID == match.ReactionGUID) is not { } data)
                    continue;
                // Apply
                var toApply = data.ApplyType = PresetApplyType.IgnoreExisting;
                LociManager.ClientSM.ApplyPreset(data);
                break;
            }
        }
    }

    public unsafe static List<string> GetFriendlist()
    {
        var ret = new List<string>();
        var friends = (InfoProxyFriendList*)InfoModule.Instance()->GetInfoProxyById(InfoProxyId.FriendList);
        for (var i = 0; i < friends->InfoProxyCommonList.CharDataSpan.Length; i++)
        {
            var entry = friends->InfoProxyCommonList.CharDataSpan[i];
            var name = entry.NameString;
            if (name.Length is not 0)
                ret.Add($"{name}@{(GameDataSvc.WorldData.TryGetValue(entry.HomeWorld, out var world) ? world : "")}");
        }
        return ret;
    }

    /// <summary>
    ///     Returns a list of pointer addresses that are Character* references for the visible party members.
    /// </summary>
    public unsafe static List<nint> GetVisibleParty()
    {
        if (Svc.Party.Length < 2)
            return [PlayerData.Address];
        else
        {
            var ret = new List<nint>();
            // Get the specially ordered party members here.
            var hud = AgentHUD.Instance();
            var partyMembers = hud->PartyMembers.ToArray();
            // Note the first person is always the player
            var sorted = partyMembers.OrderByDescending(m => (nint)m.Object != nint.Zero).ThenBy(m => m.Index).ToList();
            // Svc.Logger.Information($"Hud Members: {string.Join(", ", sorted.Select(m => $"{((nint)m.Object):X8} (idx {m.Index})"))}");
            // Sort them by the index that they appear in.
            for (var i = 0; i < Math.Min((short)8, hud->PartyMemberCount); i++)
            {
                if (sorted[i].Object is null || !sorted[i].Object->IsCharacter())
                {
                    ret.Add(nint.Zero);
                    continue;
                }
                // Add in the actor.
                ret.Add((nint)sorted[i].Object);
            }
            return ret;
        }
    }

    public unsafe static List<nint> GetNodeOrderedVisibleParty()
    {
        if (Svc.Party.Length < 2)
            return [PlayerData.Address];
        else
        {
            var ret = new List<nint>();
            // Get the specially ordered party members here.
            var hud = AgentHUD.Instance();
            var partyMembers = hud->PartyMembers.ToArray();
            var sorted = partyMembers.Skip(1).OrderByDescending(m => (nint)m.Object != nint.Zero).ThenBy(m => m.Index).ToList();
            sorted.Insert(0, partyMembers[0]);
            // Svc.Logger.Information($"Hud Members: {string.Join(", ", sorted.Select(m => $"{((nint)m.Object):X8} (idx {m.Index})"))}");
            // Sort them by the index that they appear in.
            for (var i = 0; i < Math.Min((short)8, hud->PartyMemberCount); i++)
            {
                if (sorted[i].Object is null || !sorted[i].Object->IsCharacter())
                {
                    ret.Add(nint.Zero);
                    continue;
                }
                // Add in the actor.
                ret.Add((nint)sorted[i].Object);
            }
            return ret;
        }
    }

    // We already know the current players via our watcher.
    public static unsafe nint[] GetTargetablePlayers()
    {
        var list = new List<nint>();
        foreach (Character* chara in CharaWatcher.Rendered)
        {
            if (chara is null) continue;
            if (!chara->GetIsTargetable()) continue;
            // Append to the returns.
            list.Add((nint)chara);
        }
        return list.ToArray();
    }

    public static bool CanSpawnVFX(this Character targetChara)
        => true;

    public static bool CanSpawnFlyText(this Character targetChara)
    {
        if (!targetChara.GetIsTargetable()) return false;
        if (!PlayerData.Interactable) return false;
        if (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Svc.Condition[ConditionFlag.WatchingCutscene]
            || Svc.Condition[ConditionFlag.WatchingCutscene78]
            || Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
            || Svc.Condition[ConditionFlag.Occupied]
            || Svc.Condition[ConditionFlag.Occupied30]
            || Svc.Condition[ConditionFlag.Occupied33]
            || Svc.Condition[ConditionFlag.Occupied38]
            || Svc.Condition[ConditionFlag.Occupied39]
            || Svc.Condition[ConditionFlag.OccupiedInEvent]
            || Svc.Condition[ConditionFlag.BetweenAreas]
            || Svc.Condition[ConditionFlag.BetweenAreas51]
            || Svc.Condition[ConditionFlag.DutyRecorderPlayback]
            || Svc.Condition[ConditionFlag.LoggingOut]) return false;
        return true;
    }


    /// <summary>
    ///     There are conditions where an object can be rendered / created, but not drawable, or currently bring drawn. <para />
    ///     This mainly occurs on login or when transferring between zones, but can also occur during redraws and such.
    ///     We can get around this by checking for various draw conditions.
    /// </summary>
    public static unsafe bool IsCharaDrawn(Character* character)
    {
        nint addr = (nint)character;
        // Invalid address.
        if (addr == nint.Zero) return false;
        // DrawObject does not exist yet.
        if ((nint)character->DrawObject == nint.Zero) return false;
        // RenderFlags are marked as 'still loading'.
        if ((ulong)character->RenderFlags == 2048) return false;
        // There are models loaded into slots, still being applied.
        if (((CharacterBase*)character->DrawObject)->HasModelInSlotLoaded != 0) return false;
        // There are model files loaded into slots, still being applied.
        if (((CharacterBase*)character->DrawObject)->HasModelFilesInSlotLoaded != 0) return false;
        // Object is fully loaded.
        return true;
    }


    public static RowRef<T> CreateRef<T>(uint rowId) where T : struct, IExcelRow<T>
    {
        return new(Svc.Data.Excel, rowId);
    }

    public static string ToDisplayName(this PresetApplyType type)
        => type switch
        {
            PresetApplyType.ReplaceAll => "Replace All",
            PresetApplyType.UpdateExisting => "Update Existing",
            PresetApplyType.IgnoreExisting => "Ignore Existing",
            _ => "UNK"
        };

    public static string ToDisplayName(this KnownDirection type)
        => type switch
        {
            KnownDirection.Self => "From Self",
            KnownDirection.SelfToOther => "From you to others",
            KnownDirection.Other => "From Others",
            KnownDirection.OtherToSelf => "From others to You",
            KnownDirection.Any => "Any Filter",
            _ => "UNK"
        };

    /// <summary>
    ///     Have a blank, and multi selected compressed text output for a printed multi-selection.
    /// </summary>
    public static string PrintRange(this IEnumerable<string> s, out string FullList, string noneStr = "Any")
    {
        FullList = null!;
        var list = s.ToArray();
        if (list.Length is 0)
            return noneStr;
        if (list.Length is 1)
            return list[0].ToString();
        FullList = string.Join("\n", list.Select(x => x.ToString()));
        return $"{list.Length} selected";
    }

    public static void AttachTooltip(this LociStatus item)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
            return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(350f, 0f), new Vector2(350f, float.MaxValue));
        using var s = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.One * 8f)
            .Push(ImGuiStyleVar.WindowRounding, 4f)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f);
        using var c = ImRaii.PushColor(ImGuiCol.Border, LociCol.Gold.Uint());
        using var tt = ImRaii.Tooltip();

        // push the title, converting all color tags into the actual label.
        CkRichText.Text(item.Title, cloneId: 100);
        if (!item.Description.IsNullOrWhitespace())
        {
            ImGui.Separator();
            CkRichText.Text(350f, item.Description);
        }

        CkGui.ColorText("Duration:", ImGuiColors.ParsedGold);
        var length = TimeSpan.FromTicks(item.NoExpire ? -1 : item.TotalMilliseconds);
        ImGui.SameLine();
        ImGui.Text($"{length.Days}d {length.Hours}h {length.Minutes}m {length.Seconds}");

        CkGui.ColorText("Category:", ImGuiColors.ParsedGold);
        ImGui.SameLine();
        ImGui.Text(item.Type.ToString());

        if (item.ChainedGUID != Guid.Empty)
        {
            if (item.ChainedType is ChainType.Status)
            {
                CkGui.ColorText("Chained Status:", ImGuiColors.ParsedGold);
                ImGui.SameLine();
                var status = LociData.Statuses.FirstOrDefault(x => x.GUID == item.ChainedGUID)?.Title ?? "Unknown";
                CkRichText.Text(status, 100);
            }
            else
            {
                CkGui.ColorText("Chained Preset:", ImGuiColors.ParsedGold);
                ImGui.SameLine();
                var preset = LociData.Presets.FirstOrDefault(x => x.GUID == item.ChainedGUID)?.Title ?? "Unknown";
                CkRichText.Text(preset, 100);
            }
        }
    }

    public static void AttachTooltip(this LociStatusInfo item, IEnumerable<LociStatusInfo> statuses, IEnumerable<LociPresetInfo> presets)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(350f, 0f), new Vector2(350f, float.MaxValue));
        using var s = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.One * 8f)
            .Push(ImGuiStyleVar.WindowRounding, 4f)
            .Push(ImGuiStyleVar.PopupBorderSize, 1f);
        using var c = ImRaii.PushColor(ImGuiCol.Border, LociCol.Gold.Uint());
        using var tt = ImRaii.Tooltip();

        // push the title, converting all color tags into the actual label.
        CkRichText.Text(item.Title, cloneId: 100);
        if (!item.Description.IsNullOrWhitespace())
        {
            ImGui.Separator();
            CkRichText.Text(350f, item.Description);
        }

        CkGui.ColorText("Duration:", ImGuiColors.ParsedGold);
        var length = TimeSpan.FromTicks(item.ExpireTicks);
        ImGui.SameLine();
        ImGui.Text($"{length.Days}d {length.Hours}h {length.Minutes}m {length.Seconds}");

        CkGui.ColorText("Category:", ImGuiColors.ParsedGold);
        ImGui.SameLine();
        ImGui.Text(item.Type.ToString());

        if (item.ChainedGUID != Guid.Empty)
        {
            if (item.ChainType is ChainType.Status)
            {
                CkGui.ColorText("Chained Status:", ImGuiColors.ParsedGold);
                ImGui.SameLine();
                var status = statuses.FirstOrDefault(x => x.GUID == item.ChainedGUID).Title ?? "Unknown";
                CkRichText.Text(status, 100);
            }
            else
            {
                CkGui.ColorText("Chained Preset:", ImGuiColors.ParsedGold);
                ImGui.SameLine();
                var preset = presets.FirstOrDefault(x => x.GUID == item.ChainedGUID).Title ?? "Unknown";
                CkRichText.Text(preset, 100);
            }
        }
    }

    public static void AttachTooltip(this ParsedEmote emote, IDalamudTextureWrap img)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
        {
            using var s = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.One * 8f)
                .Push(ImGuiStyleVar.WindowRounding, 4f)
                .Push(ImGuiStyleVar.PopupRounding, 4f);
            using var c = ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink);
            using var popup = ImRaii.Tooltip();
            using (ImRaii.Group())
            {
                ImGui.Image(img.Handle, new Vector2(ImGui.GetFrameHeight() * 2));
                ImGui.SameLine();
                using (ImRaii.Group())
                {
                    ImGui.Text(emote.Name);
                    CkGui.ColorTextInline($"(Id: {emote.RowId})", CkGui.Color(ImGuiColors.DalamudGrey2));
                    CkGui.ColorText($"(Icon: {emote.IconId})", CkGui.Color(ImGuiColors.DalamudGrey));
                }
            }
            ImGui.Separator();

            CkGui.ColorText("Commands:", ImGuiColors.ParsedGold);
            CkGui.TextInline(string.Join(", ", emote.CommandsSafe.Select(cmd => "/" + cmd)));
        }
    }

    public static void ShowFormattingInfo()
    {
        CkGui.FramedHoverIconText(FAI.Code, LociCol.Gold.Uint());
        CkGui.AttachToolTip($"This supports formatting tags." +
            $"--NL----COL--Colors:--COL-- [color=red]...[/color], [color=5]...[/color]" +
            $"--NL----COL--Glow:--COL-- [glow=blue]...[/glow], [glow=7]...[/glow]" +
            $"--NL----COL--Italics:--COL-- [i]...[/i]" +
            $"--SEP--The following colors are available:" +
            $"--NL--{string.Join(", ", Enum.GetValues<XlDataUiColor>().Select(x => x.ToString()).Where(x => !x.StartsWith("_")))}" +
            $"--SEP--For extra color, look up numeric value with --COL--\"/xldata uicolor\"--COL-- command", ImGuiColors.DalamudViolet);
    }


    public static SeString ParseBBSeString(string text, bool nullTerminator = true)
        => ParseBBSeString(text, out _, nullTerminator);
    
    public static SeString ParseBBSeString(string text, out bool hadError, bool nullTerminator = true)
    {
        hadError = false;
        try
        {
            var parts = CkRichText.RichTextRegex().Split(text);
            var str = new SeStringBuilder();
            int[] openTags = new int[3]; // 0=color, 1=glow, 2=italics

            foreach (var s in parts)
            {
                if (s.Length is 0)
                    continue;

                if (TryParseColorTag(s, out var colorValue, out var isOpeningColor))
                {
                    if (isOpeningColor)
                    {
                        if (colorValue is 0 || Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>().GetRowOrDefault(colorValue) is null)
                            return ReturnError("Error: Color is out of range.", ref hadError);
                        str.AddUiForeground(colorValue);
                        openTags[0]++;
                    }
                    else
                    {
                        str.AddUiForegroundOff();
                        // Remove it, and error if it 0 prior to the removal.
                        if (openTags[0] <= 0)
                            return ReturnError("Error: Opening and closing color tags mismatch.", ref hadError);
                        openTags[0]--;
                    }
                    continue;
                }
                if (TryParseGlowTag(s, out var glowValue, out var isOpeningGlow))
                {
                    if (isOpeningGlow)
                    {
                        if (glowValue is 0 || Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>().GetRowOrDefault(glowValue) is null)
                            return ReturnError("Error: Glow color is out of range.", ref hadError);
                        // Add it, as it was successful.
                        str.AddUiGlow(glowValue);
                        openTags[1]++;
                    }
                    else
                    {
                        // Remove it, and error if it 0 prior to the removal.
                        str.AddUiGlowOff();
                        if (openTags[1] <= 0)
                            return ReturnError("Error: Opening and closing glow tags mismatch.", ref hadError);
                        openTags[1]--;
                    }
                    continue;
                }
                else if (s.Equals("[i]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddItalicsOn();
                    openTags[2]++;
                }
                else if (s.Equals("[/i]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddItalicsOff();
                    if (openTags[2] <= 0)
                        return ReturnError("Error: Opening and closing italics tags mismatch.", ref hadError);
                    openTags[2]--;
                }
                else
                {
                    str.AddText(s);
                }
            }

            // Fail if not all valid at the end
            if (!openTags.All(x => x == 0))
                return ReturnError("Error: Opening and closing elements mismatch.", ref hadError);

            if (nullTerminator)
                str.AddText("\0");

            hadError = false;
            return str.Build();
        }
        catch (Bagagwa ex)
        {
            hadError = true;
            return new SeStringBuilder().AddText($"{ex.Message}\0").Build();
        }

        SeString ReturnError(string errorMsg, ref bool hasError)
        {
            hasError = true;
            return new SeStringBuilder().AddText($"{errorMsg}\0").Build();
        }
    }

    // Helper to parse [color=xxx] and [/color]
    private static bool TryParseColorTag(string s, out ushort value, out bool isOpening)
    {
        value = 0;
        isOpening = false;
        if (s.StartsWith("[color=", StringComparison.OrdinalIgnoreCase))
        {
            isOpening = true;
            var content = s[7..^1];
            if (!ushort.TryParse(content, out value))
                value = (ushort)Enum.GetValues<XlDataUiColor>().FirstOrDefault(x => x.ToString().Equals(content, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        if (s.Equals("[/color]", StringComparison.OrdinalIgnoreCase))
        {
            isOpening = false;
            return true;
        }

        return false;
    }

    // Helper to parse [glow=xxx] and [/glow]
    private static bool TryParseGlowTag(string s, out ushort value, out bool isOpening)
    {
        value = 0;
        isOpening = false;

        if (s.StartsWith("[glow=", StringComparison.OrdinalIgnoreCase))
        {
            isOpening = true;
            var content = s[6..^1];
            if (!ushort.TryParse(content, out value))
                value = (ushort)Enum.GetValues<XlDataUiColor>().FirstOrDefault(x => x.ToString().Equals(content, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        if (s.Equals("[/glow]", StringComparison.OrdinalIgnoreCase))
        {
            isOpening = false;
            return true;
        }

        return false;
    }

    // Player waiters
    public static async Task WaitForPlayerLoading()
    {
        while (!await Svc.Framework.RunOnFrameworkThread(IsPlayerFullyLoaded).ConfigureAwait(false))
        {
            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    public static bool IsPlayerFullyLoaded()
        => PlayerData.Interactable && IsScreenReady();

    public static unsafe bool IsScreenReady()
    {
        if (AddonHelp.TryGetAddonByName<AtkUnitBase>("NowLoading", out var a) && a->IsVisible) return false;
        if (AddonHelp.TryGetAddonByName<AtkUnitBase>("FadeMiddle", out var b) && b->IsVisible) return false;
        if (AddonHelp.TryGetAddonByName<AtkUnitBase>("FadeBack", out var c) && c->IsVisible) return false;
        return true;
    }
}

