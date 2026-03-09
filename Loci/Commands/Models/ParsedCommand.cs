namespace Loci;

/// <summary>
///     Helps indicate the result of a command parse.
/// </summary>
public enum ParseOutcome
{
    /// <summary>
    ///     The command was parsed correctly.
    /// </summary>
    Success,

    /// <summary>
    ///     No ENTITY element was provided.
    /// </summary>
    NoEntity,

    /// <summary>
    ///     No ACTION element was provided.
    /// </summary>
    NoAction,

    /// <summary>
    ///     There was no Definition in the lookup table for the ENTITY ACTION pair.
    /// </summary>
    NoDefinition,

    /// <summary>
    ///     No additional paramaters were given to be parsed.
    /// </summary>
    NoParamaters,

    /// <summary>
    ///     There was an error in how the arguments were parsed.
    /// </summary>
    ArgError
}

/// <summary>
///     The result of an attempted parse.
/// </summary>
/// <param name="Result"> Indicates if the result was valid, and if not, why. </param>
/// <param name="ErrorMsg"> What about the command failed. </param>
public record ParseResult(ParseOutcome Result, ParsedCommand? ParsedData = null, string? ErrorMsg = null);

// Stores the data from parsed command strings.
public record ParsedCommand(CommandDefinition Definition)
{
    public string MatchedAction { get; set; } = string.Empty;
    public List<string> Positionals { get; } = new();
    public Dictionary<string, List<string>> Params { get; } = new();
    public HashSet<string> Flags { get; } = new();
}
