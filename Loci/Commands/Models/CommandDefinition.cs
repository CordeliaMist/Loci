namespace Loci;

/// <summary>
///     The Command definition outlines the CLI structure of: <para />
///     
///     /PREFIX <Entity> <Action> <PositionalArgs> [--Parameter] [-Flag]
/// </summary>
public sealed class CommandDefinition
{
    public string Entity { get; init; } = string.Empty; // "status", "preset", "event"
    public IReadOnlyList<string> Action { get; init; } = Array.Empty<string>(); // "add", "remove", "toggle", etc.
    public IReadOnlyList<string> PositionalArgs { get; init; } = Array.Empty<string>(); // Can serve as a placeholder for the name field
    public IReadOnlySet<string> Parameters { get; init; } = new HashSet<string>(); // define targets, names, or other params
    public IReadOnlySet<string> Flags { get; init; } = new HashSet<string>(); // Modifiers to be placed on the command
}