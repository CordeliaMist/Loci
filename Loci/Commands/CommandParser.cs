namespace Loci.Commands;

/// <summary>
///     Parses command strings via <see cref="CommandDefinition"/>'s, using their ENTITY:ACTION for lookup. <para />
///     Results are tokenized and returned as a <see cref="Loci.ParsedCommand"/>
/// </summary>
/// <remarks> 
///     This currently does not have any abstraction, but could easily be implemented 
///     if we wanted to make this possible for other CK Projects.
/// </remarks>
public sealed class CommandParser
{
    private Dictionary<string, CommandDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    // Pass in the completed definition.
    public CommandParser(Dictionary<string, CommandDefinition> definitions)
    {
        _definitions = definitions;
    }

    public CommandParser(Func<Dictionary<string, CommandDefinition>> definitionBuilder)
    {
        _definitions = definitionBuilder();
    }

    public IReadOnlyDictionary<string, CommandDefinition> Definitions => _definitions;

    // Update the complete definition dictionary
    public void SetDefinitions(Dictionary<string, CommandDefinition> definitions)
        => _definitions = definitions;


    // Adds a definition to the dictionary, with a entity action lookup
    public void AddDefinition(CommandDefinition definition)
    {
        // If there is only one action, add the single key, otherwise, add a key for each action.
        if (definition.Action.Count == 1)
        {
            var lookupKey = $"{definition.Entity.ToLowerInvariant()}:{definition.Action[0].ToLowerInvariant()}";
            _definitions[lookupKey] = definition;
        }
        else
        {
            foreach (var action in definition.Action)
            {
                var lookupKey = $"{definition.Entity.ToLowerInvariant()}:{action.ToLowerInvariant()}";
                _definitions[lookupKey] = definition;
            }
        }
    }

    public void RemoveDefinition(string entity, string action)
    {
        var lookupKey = $"{entity.ToLowerInvariant()}:{action.ToLowerInvariant()}";
        _definitions.Remove(lookupKey);
    }

    public void RemoveDefinition(CommandDefinition definition)
    {
        // Remove all kvp's with the value of the definition.
        var keysToRemove = _definitions.Where(kvp => kvp.Value == definition).Select(kvp => kvp.Key).ToList();
        foreach (var key in keysToRemove)
            _definitions.Remove(key);
    }

    public void ClearDefinitions()
        => _definitions.Clear();

    /// <summary>
    ///     Attempts to parse a command. Returns the parsed result, 
    ///     containing data about the outcome.
    /// </summary>
    public ParseResult ParseArguments(string commandArgs)
    {
        // Split the input into a maximum of 3 parts. This gives us the entity and the action.
        // ex: "group add My Group -user John" -> ["group", "add", "My Group -user John" ]
        var split = commandArgs.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // If the split is less than 3  we should show the appropriate help text.        
        switch (split.Length)
        {
            case 2: return new(ParseOutcome.NoParamaters, null);
            case 1: return new(ParseOutcome.NoAction);
            case 0: return new(ParseOutcome.NoEntity);
        }
        
        var entity = split[0]; // request, group, or folder
        var action = split[1]; // send, accept, reject, create, add, remove, move, merge
        var untokenizedArgs = split[2];

        // Try find the definition so we can associate it
        var lookupKey = $"{entity}:{action}";
        if (!_definitions.TryGetValue(lookupKey, out var definition))
            return new(ParseOutcome.NoDefinition);

        // Create the base parsed command
        var parsed = new ParsedCommand(definition) { MatchedAction = action };

        // Tokenize remainder (with quotes support)
        var tokens = TokenizeCommand(untokenizedArgs);
#if DEBUG
        Svc.Logger.Debug($"Tokenized Commands:\n{string.Join(" | ", tokens)}");
#endif
        int i = 0;
        while (i < tokens.Count)
        {
            // Get the correct token.
            var token = tokens[i];
            // If it was a flag, add the flag without worrying about anything else following it.
            if (definition.Flags.Contains(token))
            {
                parsed.Flags.Add(token);
                i++;
#if DEBUG
                Svc.Logger.Information($"Adding Flag: {token}");
#endif
            }
            // If a paramater, we need to wait until we fine another flag, token, or reach the end of the tokens.
            else if (definition.Parameters.Contains(token))
            {
                i++;
                var values = new List<string>();
                // Continue until we reach one of the above mentioned points, adding tokens into the values until we do.
                while (i < tokens.Count && !definition.Flags.Contains(tokens[i]) && !definition.Parameters.Contains(tokens[i]))
                {
                    values.Add(tokens[i]);
                    i++;
                }
                // Insert the values of the token.
                parsed.Params[token] = values;
#if DEBUG
                Svc.Logger.Information($"Applying Paramater: {string.Join(", ", values)}");
#endif
            }
            // Only add to the positional args if they exist and are contained.
            else
            {
                if (definition.PositionalArgs.Count > 0 && !definition.PositionalArgs.Contains(token, StringComparer.OrdinalIgnoreCase))
                    return new(ParseOutcome.ArgError, parsed, "Bad Target Type");
#if DEBUG
                Svc.Logger.Information($"Added PositionalArg: {token}");
#endif
                parsed.Positionals.Add(token);
                i++;
            }
        }

        return new(ParseOutcome.Success, parsed);
    }

    /// <summary>
    ///     Takes a CLI formatted command and tokenizes the outcome. <br />
    ///     Public for uses beyond parsing commands, but should generally be
    ///     called with the ENTITY and ACTION stripped from 
    ///     the string to tokenize correctly
    /// </summary>
    /// <param name="input"> the string to be tokenized </param>
    /// <returns> The tokenized string </returns>
    public static List<string> TokenizeCommand(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue; // skip quote character
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}

