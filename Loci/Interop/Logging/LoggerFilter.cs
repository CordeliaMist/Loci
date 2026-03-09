using Loci.Data;
using System.Runtime.CompilerServices;

namespace Loci;

public static class LoggerFilter
{
    public static LoggerType FilteredLogTypes => MainConfig.LoggerFilters;

    /// <summary>
    ///     Perform a bitwise check for validation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldLog(LoggerType category)
        => (FilteredLogTypes & category) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogTrace(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (type is 0 || ShouldLog(type))
            logger.Log(LogLevel.Trace, message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDebug(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (type is 0 || ShouldLog(type))
            logger.Log(LogLevel.Debug, message);
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (type is 0 || ShouldLog(type))
            logger.Log(LogLevel.Information, message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWarning(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (type is 0 || ShouldLog(type))
            logger.Log(LogLevel.Warning, message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (type is 0 || ShouldLog(type))
            logger.Log(LogLevel.Error, message);
    }
}
