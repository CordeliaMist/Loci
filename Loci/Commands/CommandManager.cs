using System.Diagnostics.CodeAnalysis;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Loci.Data;
using Loci.Gui;
using Loci.Gui.Components;
using Loci.Services;
using Loci.Services.Mediator;
using LociApi.Enums;
using OtterGui.Classes;

namespace Loci.Commands;

public sealed class CommandManager : IDisposable
{
    private const string MainCommand = "/loci";

    private CommandParser _parser;

    private readonly ILogger<CommandManager> _logger;
    private readonly LociMediator _mediator;
    private readonly LociManager _manager;

    public CommandManager(ILogger<CommandManager> logger, LociMediator mediator, LociManager manager)
    {
        _logger = logger;
        _mediator = mediator;
        _manager = manager;

        // Init the parser with our builder
        _parser = new CommandParser(InitDefinitions());

        // Add Host command handlers.
        Svc.Commands.AddHandler(MainCommand, new CommandInfo(OnLoci) { HelpMessage = "Loci's CLI for commands. Use without args to toggle the UI." });
    }

    public void Dispose()
    {
        Svc.Commands.RemoveHandler(MainCommand);
    }

    private void OnLoci(string command, string args)
    {
        var splitArgs = args.ToLowerInvariant().Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
        Svc.Logger.Warning($"cmd: {command} | args: {args}");
        // if no arguments.
        if (splitArgs.Length == 0)
        {
            _mediator.Publish(new UiToggleMessage(typeof(MainUI)));
            return;
        }

        if (string.Equals(splitArgs[0], "settings", StringComparison.OrdinalIgnoreCase))
        {
            _mediator.Publish(new OpenMainUiTab(LociUITabs.SelectedTab.Settings));
            return;
        }

        if (string.Equals(splitArgs[0], "help", StringComparison.OrdinalIgnoreCase) || _parser.GetAllEntities().Contains(splitArgs[0]))
        {
            OnLocis(command, args);
            return;
        }

        Svc.Chat.Print(new SeStringBuilder().AddYellow(" -- Loci Commands --").BuiltString);
        Svc.Chat.Print(new SeStringBuilder().AddCommand("/loci help", "Get command and usage help.").BuiltString);
        Svc.Chat.Print(new SeStringBuilder().AddCommand("/loci settings", "Open the main UI on the settings tab.").BuiltString);
        Svc.Chat.Print(new SeStringBuilder().AddCommand("/loci", "Toggle the main UI.").BuiltString);
    }


    private void OnLocis(string command, string arguments)
    {
        // Parse the command result
        var res = _parser.ParseArguments(arguments);
        if (res.Result is not ParseOutcome.Success || res.ParsedData is null)
        {
            ShowCmdHelp(arguments, res);
            return;
        }

        // Execute command logic
        switch (res.ParsedData.Definition.Entity)
        {
            // Status Logic
            case "status":
                HandleStatusLogic(res.ParsedData);
                break;

            // Preset Logic
            case "preset":
                HandlePresetLogic(res.ParsedData);
                break;

            // Event Logic (TODO)
            case "events":
                HandleEventLogic();
                break;
        }
    }

    private void CommandError(string message)
    {
        Svc.Chat.PrintError(new SeStringBuilder().AddText(message).BuiltString, "Loci", 527);
    }

    private void HandleStatusLogic(ParsedCommand parsed)
    {
        if (!GetTargetStatusManager(parsed, out var sm)) return;
        if (sm.Ephemeral)
        {
            CommandError("Can't modify actors managed by other plugins.");
            return;
        }

        // handle positionals (get the status to apply)
        var combined = string.Join(" ", parsed.Positionals);
        if (!FindStatus(combined, out var matchedStatus)) return;

        _logger.LogDebug($"Selected Status: {matchedStatus.Title}");

        // clone the matched status to a new status object to apply if any changes need to be made
        // so they aren't updating the original status
        var tempStatus = matchedStatus.NewtonsoftDeepClone();

        if (parsed.Flags.Contains("-permanent"))
        {
            tempStatus.Days = 0;
            tempStatus.Hours = 0;
            tempStatus.Minutes = 0;
            tempStatus.Seconds = 0;
            tempStatus.NoExpire = true;
            tempStatus.Modifiers &= ~Modifiers.PersistExpireTime;
        }

        if (parsed.Flags.Contains("-dispellable"))
            tempStatus.Modifiers |= Modifiers.CanDispel;

        if (parsed.Flags.Contains("-nochain"))
            tempStatus.ChainedGUID = Guid.Empty;

        if (parsed.Flags.Contains("-novfx"))
            tempStatus.CustomFXPath = "Clear";

        switch (parsed.MatchedAction)
        {
            case "apply":
                sm.AddOrUpdate(tempStatus.PreApply(), ManagerChangeType.ApplyRemove);
                break;

            case "remove":
                sm.Cancel(tempStatus, ManagerChangeType.ApplyRemove);
                break;

            case "toggle":
                if (sm.ContainsStatus(tempStatus))
                    sm.Cancel(tempStatus, ManagerChangeType.ApplyRemove);
                else
                    sm.AddOrUpdate(tempStatus.PreApply(), ManagerChangeType.ApplyRemove);
                break;

            default:
                CommandError("Not a valid status action.");
                break;
        }

        unsafe
        {
            _logger.LogInformation($"Status command updated {Utils.ToLociName(sm.Owner)}");
        }
    }

    private void HandlePresetLogic(ParsedCommand parsed)
    {
        if (!GetTargetStatusManager(parsed, out var sm)) return;
        if (sm.Ephemeral)
        {
            CommandError("Can't modify actors managed by other plugins.");
            return;
        }

        // handle positionals (get the status to apply)
        var combined = string.Join(" ", parsed.Positionals);
        if (!FindPreset(combined, out var matchedPreset)) return;

        _logger.LogDebug($"Selected preset: {matchedPreset.Title}");

        switch (parsed.MatchedAction)
        {
            case "apply":
                sm.ApplyPreset(matchedPreset, ManagerChangeType.ApplyRemove);
                break;

            case "remove":
                sm.RemovePreset(matchedPreset, ManagerChangeType.ApplyRemove);
                break;

            case "toggle":
                if (sm.ContainsPreset(matchedPreset))
                    sm.RemovePreset(matchedPreset, ManagerChangeType.ApplyRemove);
                else
                    sm.ApplyPreset(matchedPreset, ManagerChangeType.ApplyRemove);
                break;

            default:
                CommandError("Not a valid status action.");
                break;
        }

        unsafe
        {
            _logger.LogInformation($"Preset command updated {Utils.ToLociName(sm.Owner)}");
        }
    }


    private unsafe bool GetTargetStatusManager(ParsedCommand parsed, [NotNullWhen(true)] out ActorSM? actorSm)
    {
        actorSm = null;
        // figure out what our target is
        if (parsed.Params.ContainsKey("-player") || parsed.Params.ContainsKey("-players"))
        {
            // TODO: implement player lookup logic.
            CommandError("Unimplemented logic");
            return false;
        }
        else if (parsed.Params.ContainsKey("-t") || parsed.Params.ContainsKey("-target"))
        {
            if (CharaWatcher.TryGetValue(Svc.Targets.Target?.Address ?? IntPtr.Zero, out var target))
            {
                // try to get the status manager of our current target
                actorSm = _manager.GetOrCreateSM(target);
            }
            else
            {
                CommandError("-t or -target specified, but you have not target!");
                return false;
            }
        }
        else if (parsed.Params.ContainsKey("-ft") || parsed.Params.ContainsKey("-focustarget"))
        {
            if (CharaWatcher.TryGetValue(Svc.Targets.FocusTarget?.Address ?? IntPtr.Zero, out var target))
                // try to get the status manager of our current target
                actorSm = _manager.GetOrCreateSM(target);
            else
            {
                CommandError("-ft or -focustarget specified, but you have no focus target!");
                return false;
            }
        }
        else if (parsed.Params.Count == 0)
        {
            // target self
            actorSm = LociManager.ClientSM;
        }
        else
        {
            CommandError("No valid target was found.");
            return false;
        }

        _logger.LogDebug($"Selected SM: {Utils.ToLociName(actorSm.Owner)}");
        return true;
    }

    private bool FindStatus(string combined, [NotNullWhen(true)] out LociStatus? matchedStatus)
    {
        if (Guid.TryParse(combined, out var guid))
        {
            if (LociData.Statuses.FirstOrDefault(s => s.GUID == guid) is { } status)
            {
                matchedStatus = status;
            }
            else
            {
                CommandError($"No status was found with this GUID: \"{combined}\"");
                matchedStatus = null;
                return false;
            }
        }
        else
        {
            // we're trying to get the name of the status.
            if (LociData.Statuses.OrderBy(s => s.Title).FirstOrDefault(s => s.Title.Contains(combined, StringComparison.OrdinalIgnoreCase)) is { } status)
            {
                matchedStatus = status;
            }
            else
            {
                CommandError($"Could not find a status containing \"{combined}\"");
                matchedStatus = null;
                return false;
            }
        }

        return true;
    }

    private bool FindPreset(string combined, [NotNullWhen(true)] out LociPreset? preset)
    {
        if (Guid.TryParse(combined, out var guid))
        {
            if (LociData.Presets.FirstOrDefault(s => s.GUID == guid) is { } p)
            {
                preset = p;
            }
            else
            {
                CommandError($"No preset was found with this GUID: \"{combined}\"");
                preset = null;
                return false;
            }
        }
        else
        {
            // we're trying to get the name of the status.
            if (LociData.Presets.OrderBy(s => s.Title)
                    .FirstOrDefault(s => s.Title.Contains(combined, StringComparison.OrdinalIgnoreCase)) is { } p)
            {
                preset = p;
            }
            else
            {
                CommandError($"Could not find a preset containing \"{combined}\"");
                preset = null;
                return false;
            }
        }

        return true;
    }

    //private unsafe IEnumerable<UserData> ResolveTargets(string positional)
    //{
    //    try
    //    {
    //        return positional.ToLowerInvariant() switch
    //        {
    //            "nearby" => GetNearbyTargets(),
    //            "ft" or "focustarget" => ResolveTarget(true),
    //            "t" or "target" => ResolveTarget(false),
    //            _ => []
    //        };
    //    }
    //    catch (Bagagwa ex)
    //    {
    //        _logger.LogError($"Exception while resolving targets: {ex}");
    //        return [];
    //    }

    //    IEnumerable<UserData> GetNearbyTargets()
    //    {
    //        var nearby = _radar.RadarUsers.Where(r => r.CanSendRequests);
    //        nearby = nearby.Where(u =>
    //        {
    //            if (!CharaWatcher.Rendered.Contains(u.Address))
    //                return false;

    //            return PlayerData.DistanceTo(((Character*)u.Address)->Position) <= 5;
    //        });
    //        return nearby.Select(u => new UserData(u.UID));
    //    };

    //    IEnumerable<UserData> ResolveTarget(bool isFocus)
    //    {
    //        var target = isFocus ? TargetSystem.Instance()->FocusTarget : TargetSystem.Instance()->Target;
    //        // Return if a match was found that was valid
    //        return target != null && _radar.RadarUsers.FirstOrDefault(u => u.CanSendRequests && u.Address == (nint)target) is { } match
    //            ? [new UserData(match.UID)] : [];
    //    }
    //}

    //private void HandleRequestResponse(ParsedCommand parsed, bool isAccept)
    //{
    //    var relatedRequests = (parsed.Positionals[0].ToLowerInvariant() switch
    //    {
    //        "area" => _requests.Incoming.Where(r => r.SentFromCurrentArea(LocationSvc.WorldId, LocationSvc.Current.TerritoryId)),
    //        "world" => _requests.Incoming.Where(r => r.SentFromWorld(LocationSvc.WorldId)),
    //        "all" => _requests.Incoming,
    //        _ => []
    //    }).ToList();

    //    // if none were filtered, abort.
    //    if (relatedRequests.Count is 0)
    //        return;
    //}


    private void HandleEventLogic()
    {
        CommandError("Logic not implemented yet.");
    }

    #region Parsing and Help

    private void ShowCmdHelp(string arguments, ParseResult res)
    {
        var split = arguments.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? entity = split.Length > 0 ? split[0] : null;
        string? action = split.Length > 1 ? split[1] : null;

        if (string.IsNullOrEmpty(entity) && string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Valid args for ").AddText("/loci ", 527).AddText("are:").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("status", "apply, remove, or toggle statuses.").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("preset", "apply, remove, or toggle presets.").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("event", "create or invoke events that interact with status and presets.").BuiltString);
            return;
        }

        // Switch based on the entity type to show entity-level or action-level help.
        switch (entity?.ToLowerInvariant())
        {
            case "status": ShowStatusHelp(action, res.ErrorMsg); break;
            case "preset": ShowPresetHelp(action, res.ErrorMsg); break;
            case "event": ShowEventHelp(action, res.ErrorMsg); break;
            default:
                // Unknown entity → show main help with entity highlighted as invalid
                if (!string.IsNullOrEmpty(entity))
                    Svc.Chat.PrintError(new SeStringBuilder().AddText("Loci", 527, true).AddText("Invalid Entity: ").AddRed(entity, true).BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Valid args for ").AddText("/loci ", 527).AddText("are:").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddCommand("status", "apply, remove, or toggle statuses.").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddCommand("preset", "apply, remove, or toggle presets.").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddCommand("event", "create or invoke events that interact with status and presets.").BuiltString);
                break;
        }
    }

    private void ShowStatusHelp(string? action, string? badArg)
    {
        // For generic help
        if (string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder()
                .AddText("Loci", 527, true)
                .AddText(" Template: ")
                .AddText("/loci status ", 527).AddYellow("<action> ").AddGreen("<name> ").AddBlue("[target params] ").AddText("[Flags]", 537)
                .BuiltString);
            // Show possible actions
            Svc.Chat.Print(new SeStringBuilder().AddYellow("    》 Actions: ")
                .AddText("apply").AddText(", ", 527).AddText("remove").AddText(", ", 527).AddText("toggle").BuiltString);
            // Show possible paramaters.
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ")
                .AddText("The name or GUID of the status")
                .BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Target Params: ")
                .AddText("-t / -target, -ft / -focustarget, -player <name>, -players <names>")
                .BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Flags: ")
                .AddText("-permanent, -dispellable, -nochain, -novfx")
                .BuiltString);
            return;
        }

        // May need to refine this further as time goes on to give more detailed errors.
        if (!string.IsNullOrEmpty(badArg))
            Svc.Chat.PrintError(new SeStringBuilder().AddText("Request command error: ").AddRed(badArg, true).BuiltString);

        switch (action.ToLowerInvariant())
        {
            case "apply":
            case "remove":
            case "toggle":
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Command ")
                    .AddText($"status {action.ToLowerInvariant()} ", 527).AddText("is missing args  》").AddGreen("<name> ").AddBlue("[target params]")
                    .AddText("[Flags]", 537).BuiltString);

                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ")
                    .AddText("The name or GUID of the status")
                    .BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Target Params: ")
                    .AddText("-t / -target, -ft / -focustarget, -player <name>, -players <names>")
                    .BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Flags: ")
                    .AddText("-permanent, -dispellable, -nochain, -novfx")
                    .BuiltString);
                break;
            default:
                CommandError($"Unknown action {action}"); // basic response.
                break;
        }
    }

    private void ShowPresetHelp(string? action, string? badArg)
    {
        // For generic help
        if (string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder()
                .AddText("Loci", 527, true)
                .AddText(" Template: ")
                .AddText("/loci preset ", 527).AddYellow("<action> ").AddGreen("<name> ").AddBlue("[target params]").AddText("[Flags]", 537)
                .BuiltString);
            // Show possible actions
            Svc.Chat.Print(new SeStringBuilder().AddYellow("    》 Actions: ")
                .AddText("apply").AddText(", ", 527).AddText("remove").AddText(", ", 527).AddText("toggle").BuiltString);
            // Show possible paramaters.
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ")
                .AddText("The name or GUID of the status")
                .BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Target Params: ")
                .AddText("-t / -target, -ft / -focustarget, -player <name>, -players <names>")
                .BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Flags: ")
                .AddText("-permanent, -dispellable, -nochain, -novfx")
                .BuiltString);
            return;
        }

        // May need to refine this further as time goes on to give more detailed errors.
        if (!string.IsNullOrEmpty(badArg))
            Svc.Chat.PrintError(new SeStringBuilder().AddText("Request command error: ").AddRed(badArg, true).BuiltString);

        switch (action.ToLowerInvariant())
        {
            case "apply":
            case "remove":
            case "toggle":
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Command ")
                    .AddText($"preset {action.ToLowerInvariant()} ", 527).AddText("is missing args  》").AddGreen("<name> ").AddBlue("[target params]")
                    .AddText("[Flags]", 537).BuiltString);

                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ")
                    .AddText("The name or GUID of the status")
                    .BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Target Params: ")
                    .AddText("-t / -target, -ft / -focustarget, -nearby, -all, -player <name>, -players <names>")
                    .BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Flags: ")
                    .AddText("-permanent, -dispellable, -nochain, -novfx")
                    .BuiltString);
                break;
            default:
                CommandError($"Unknown action {action}"); // basic response.
                break;
        }
    }

    private void ShowEventHelp(string? action, string? badArg)
    {
        // Not yet implemented
        // For generic help
/*        if (string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder()
                .AddText("Loci", 527, true)
                .AddText(" Generic Template: ")
                .AddText("/loci group ", 527).AddYellow("<action> ").AddGreen("<Name>").AddBlue("[params]").AddText("[Flags]", 537)
                .BuiltString);
            // Show possible actions
            Svc.Chat.Print(new SeStringBuilder().AddYellow("    》 Actions: ")
                .AddText("create add remove rename move merge delete(TBD)").BuiltString);

            // Show possible paramaters.
            Svc.Chat.Print(new SeStringBuilder().AddText("    》 Names, Params, Flags: ", 527).AddText("See Action Helps for info").BuiltString);
            return;
        }

        // May need to refine this further as time goes on to give more detailed errors.
        if (!string.IsNullOrEmpty(badArg))
            Svc.Chat.PrintError(new SeStringBuilder().AddText("Group command error: ").AddRed(badArg, true).BuiltString);

        switch (action.ToLowerInvariant())
        {
            case "create":
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Command ")
                    .AddText("/loci folder create ", 527).AddText(" missing args 》").AddGreen("<Name>").AddBlue("[params]").AddText("[Flags]", 537).BuiltString);

                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ").AddText("The name of the new folder").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Params: ").AddText("-parent").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddText("    》 Flags: ", 537).AddText("-ensurefolder").BuiltString);
                break;

            case "add":
            case "remove":
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Command ")
                    .AddText($"/loci folder {action} ", 527).AddText(" missing args 》").AddGreen("<Name> ").BuiltString);

                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ").AddText("The name of the new folder").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Params: ").AddText("-parent").BuiltString);
                break;

            case "move":
                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name(s): ").AddText("The Folder(s) being moved").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Params: ").AddText("-target").BuiltString);
                break;

            case "merge":
                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ").AddText("The Group(s) being merged").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Params: ").AddText("-target").BuiltString);
                break;
        }*/
    }

    private Dictionary<string, CommandDefinition> InitDefinitions()
        => new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["status:apply"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["apply"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["status:remove"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["remove"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["status:toggle"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["toggle"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["preset:apply"] = new CommandDefinition
            {
                Entity = "preset",
                Action = ["apply"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["preset:remove"] = new CommandDefinition
            {
                Entity = "preset",
                Action = ["remove"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["preset:toggle"] = new CommandDefinition
            {
                Entity = "preset",
                Action = ["toggle"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
        };

    #endregion Parsing and Help
}
