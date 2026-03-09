using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Loci.Data;
using Loci.Gui;
using Loci.Services.Mediator;
using OtterGui.Classes;

namespace Loci.Commands;

public sealed class CommandManager : IDisposable
{
    private const string MainCommand = "/loci";

    private CommandParser _parser;

    private readonly ILogger<CommandManager> _logger;
    private readonly LociMediator _mediator;
    private readonly MainConfig _config;

    public CommandManager(ILogger<CommandManager> logger, LociMediator mediator, MainConfig config)
    {
        _logger = logger;
        _mediator = mediator;
        _config = config;

        // Init the parser with our builder
        _parser = new CommandParser(InitDefinitions());

        // Add Host command handlers.
        Svc.Commands.AddHandler(MainCommand, new CommandInfo(OnLocis) { HelpMessage = "Loci's CLI for commands. Use without args to toggle the UI." });
    }

    public void Dispose()
    {
        Svc.Commands.RemoveHandler(MainCommand);
    }

    private void OnLoci(string command, string args)
    {
        var splitArgs = args.ToLowerInvariant().Trim().Split(" ", StringSplitOptions.RemoveEmptyEntries);
        // if no arguments.
        if (splitArgs.Length == 0)
        {
            _mediator.Publish(new UiToggleMessage(typeof(MainUI)));
            return;
        }
        else if (string.Equals(splitArgs[0], "settings", StringComparison.OrdinalIgnoreCase))
        {
            // Handle opening the settings page in a better way
            _mediator.Publish(new UiToggleMessage(typeof(MainUI)));
            return;
        }
        else if (string.Equals(splitArgs[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            OnLocis(command, args);
            return;
        }
        
        Svc.Chat.Print(new SeStringBuilder().AddYellow(" -- Loci Commands --").BuiltString);
        Svc.Chat.Print(new SeStringBuilder().AddCommand("/loci help", "Toggle UI").BuiltString);
        Svc.Chat.Print(new SeStringBuilder().AddCommand("/loci", "CLI formatted commands for statuses, presets, and events.").BuiltString);
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
            case "status" when res.ParsedData.MatchedAction == "apply":
                HandleStatusLogic(res.ParsedData);
                break;
            case "status" when res.ParsedData.MatchedAction == "remove":
                HandleStatusLogic(res.ParsedData);
                break;
            case "status" when res.ParsedData.MatchedAction == "toggle":
                HandleStatusLogic(res.ParsedData);
                break;

            // Preset Logic
            case "preset":
                HandlePresetLogic(res.ParsedData);
                break;
            //case "preset" when res.ParsedData.MatchedAction == "apply":
            //    HandlePresetApply(res.ParsedData);
            //    break;
            //case "preset" when res.ParsedData.MatchedAction == "remove":
            //    HandlePresetRemove(res.ParsedData);
            //    break;
            //case "preset" when res.ParsedData.MatchedAction == "toggle":
            //    HandlePresetToggle(res.ParsedData);
            //    break;

            // Event Logic (TODO)
            case "events":
                HandleEventLogic(res.ParsedData);
                break;
        }
    }

    private void HandleStatusLogic(ParsedCommand parsed)
    {
        Svc.Chat.PrintError(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Logic not implemented yet.").BuiltString);
        //var targets = ResolveTargets(parsed.Positionals[0].ToLowerInvariant()).ToList();
        //if (targets.Count is 0)
        //    return;

        //var isTemp = !parsed.Flags.Contains("-aspermanent");
        //var message = parsed.Params.TryGetValue("-msg", out var msg) || parsed.Params.TryGetValue("-message", out msg) ? msg[0] : string.Empty;
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

    private void HandlePresetLogic(ParsedCommand parsed)
    {
        Svc.Chat.PrintError(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Logic not implemented yet.").BuiltString);
    }

    private void HandleEventLogic(ParsedCommand parsed)
    {
        Svc.Chat.PrintError(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Logic not implemented yet.").BuiltString);
    }

    #region Parsing and Help
    private void ShowCmdHelp(string arguments, ParseResult res)
    {
        var split = arguments.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? entity = split.Length > 0 ? split[0] : null;
        string? action = split.Length > 1 ? split[1] : null;

        if (string.IsNullOrEmpty(entity) && string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Valid args for ").AddText("/sund ", 527).AddText("are:").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("request", "automates the process of requests.").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("group", "automates various interactions with groups.").BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddCommand("folder", "automates interactions with GroupFolders.").BuiltString);
            return;
        }

        // Switch based on the entity type to show entity-level or action-level help.
        switch  (entity?.ToLowerInvariant())
        {
            case "status": ShowStatusHelp(action, res.ErrorMsg);break;
            case "preset": ShowPresetHelp(action, res.ErrorMsg);  break;
            case "event":  ShowEventHelp(action, res.ErrorMsg); break;
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
                .AddText("/loci status ", 527).AddYellow("<action> ").AddGreen("<name> ").AddBlue("[target params]").AddText("[Flags]", 537)
                .BuiltString);
            // Show possible actions
            Svc.Chat.Print(new SeStringBuilder().AddYellow("    》 Actions: ")
                .AddText("apply").AddText(", ", 527).AddText("remove").AddText(", ", 527).AddText("toggle").BuiltString);
            // Show possible paramaters.
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ")
                .AddText("The name or GUID of the status")
                .BuiltString);
            Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Target Params: ")
                .AddText("-t / -target, -ft / -focustarget, -nearby, -all, -player <name>, -players <names>")
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
                    .AddText($"status {action.ToLowerInvariant()}", 527).AddText("is missing args  》").AddGreen("<name> ").AddBlue("[target params]").AddText("[Flags]", 537).BuiltString);

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
            // Otherwise display nothing.
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
                .AddText("-t / -target, -ft / -focustarget, -nearby, -all, -player <name>, -players <names>")
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
                    .AddText($"preset {action.ToLowerInvariant()}", 527).AddText("is missing args  》").AddGreen("<name> ").AddBlue("[target params]").AddText("[Flags]", 537).BuiltString);

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
                // Otherwise display nothing.
        }
    }

    private void ShowEventHelp(string? action, string? badArg)
    {
        // Not yet implemented
        return;
        // For generic help
        if (string.IsNullOrEmpty(action))
        {
            Svc.Chat.Print(new SeStringBuilder()
                .AddText("Loci", 527, true)
                .AddText(" Generic Template: ")
                .AddText("/sund group ", 527).AddYellow("<action> ").AddGreen("<Name>").AddBlue("[params]").AddText("[Flags]", 537)
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
                    .AddText("/sund folder create ", 527).AddText(" missing args 》").AddGreen("<Name>").AddBlue("[params]").AddText("[Flags]", 537).BuiltString);

                Svc.Chat.Print(new SeStringBuilder().AddGreen("    》 Name: ").AddText("The name of the new folder").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddBlue("    》 Params: ").AddText("-parent").BuiltString);
                Svc.Chat.Print(new SeStringBuilder().AddText("    》 Flags: ", 537).AddText("-ensurefolder").BuiltString);
                break;

            case "add":
            case "remove":
                Svc.Chat.Print(new SeStringBuilder().AddText("Loci", 527, true).AddText(" Command ")
                    .AddText($"/sund folder {action} ", 527).AddText(" missing args 》").AddGreen("<Name> ").BuiltString);

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
        }
    }

    private Dictionary<string, CommandDefinition> InitDefinitions()
        => new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["status:apply"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["apply"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players",  },
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
                Entity = "status",
                Action = ["apply"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["preset:remove"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["remove"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
            ["preset:toggle"] = new CommandDefinition
            {
                Entity = "status",
                Action = ["toggle"],
                // Positional args here would be the status name / guid?
                Parameters = new HashSet<string> { "-t", "-target", "-ft", "-focusTarget", "-player", "-players", },
                Flags = new HashSet<string> { "-permanent", "-dispellable", "-nochain", "-novfx" }
            },
        };
    #endregion Parsing and Help
}

