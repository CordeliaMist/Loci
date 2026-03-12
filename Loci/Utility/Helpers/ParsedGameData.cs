using Lumina.Excel.Sheets;
using System.Collections.Immutable;

namespace Loci.Data;

public readonly record struct ParsedEmote : IEquatable<ParsedEmote>
{
    public readonly uint RowId;
    public readonly uint IconId;
    public readonly string Name;
    public readonly byte EmoteConditionMode;

    public readonly ImmutableArray<string> EmoteCommands;

    public IEnumerable<string> CommandsSafe 
        => EmoteCommands.IsDefault ? Enumerable.Empty<string>() : EmoteCommands;

    public ParsedEmote()
    {
        RowId = 0;
        IconId = 450;
        Name = string.Empty;
        EmoteConditionMode = 0;
        EmoteCommands = ImmutableArray<string>.Empty;
    }

    public ParsedEmote(Emote emote)
    {
        RowId = emote.RowId;
        IconId = (emote.Icon == 64350 ? 405 : emote.Icon);
        Name = emote.Name.ToString();
        EmoteConditionMode = emote.EmoteMode.Value.ConditionMode;

        // Deal with Lumina ValueNullable voodoo.
        var commands = emote.TextCommand.ValueNullable;
        EmoteCommands = commands.HasValue
            ? new[]
            {
                commands.Value.Command.ToString().TrimStart('/'),
                commands.Value.ShortCommand.ToString().TrimStart('/'),
                commands.Value.Alias.ToString().TrimStart('/'),
                commands.Value.ShortAlias.ToString().TrimStart('/')
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToImmutableArray()
            : ImmutableArray<string>.Empty;
    }

    public string InfoString
        => $"Emote: {Name} ({RowId})(Icon {IconId})-(Cond.{EmoteConditionMode}) " +
           $"--> Cmds: {string.Join(", ", EmoteCommands.IsDefault ? Enumerable.Empty<string>() : EmoteCommands)}\n";

    /// <inheritdoc/>
    public override string ToString()
        => Name;

    /// <inheritdoc/>
    public bool Equals(ParsedEmote other)
        => RowId == other.RowId;

    /// <inheritdoc/>
    public override int GetHashCode()
        => RowId.GetHashCode();
}

public readonly record struct ParsedOnlineStatus : IEquatable<ParsedOnlineStatus>
{
    public readonly uint RowId;
    public readonly string Name;
    public readonly uint IconId;
    public ParsedOnlineStatus()
    {
        RowId = 0;
        Name = string.Empty;
        IconId = 450;
    }
    public ParsedOnlineStatus(OnlineStatus status)
    {
        RowId = status.RowId;
        Name = status.Name.ToString();
        IconId = status.Icon;
    }
    public string InfoString
        => $"Online Status: {Name} ({RowId})(Icon {IconId})\n";
    /// <inheritdoc/>
    public override string ToString()
        => Name;

    /// <inheritdoc/>
    public bool Equals(ParsedOnlineStatus other)
        => RowId == other.RowId;
    
    /// <inheritdoc/>
    public override int GetHashCode()
        => RowId.GetHashCode();
}
