using Crestron.SimplSharp;
using Microsoft.Extensions.Logging;

namespace ZGott.Crestron.Logging;

/// <summary>
/// An <see cref="ILogger"/> implementation that writes to the Crestron SimplSharp ErrorLog and CrestronConsole.
/// </summary>
/// <param name="categoryName">Name of the category.</param>
/// <param name="getCurrentOptions">A function to get the current <see cref="CrestronLoggerOptions"/>.</param>
/// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
public sealed class CrestronLogger(
    string categoryName,
    Func<CrestronLoggerOptions> getCurrentOptions,
    IExternalScopeProvider scopeProvider
) : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel)
    {
        var options = getCurrentOptions();
        return logLevel != LogLevel.None && logLevel >= options.MinLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        var options = getCurrentOptions();
        var scopes = GetScopeText();

        foreach (var line in FormatLines(
                     logLevel,
                     eventId,
                     message,
                     exception,
                     options,
                     scopes
                 ))
        {
            WriteToConsole(line);
            WriteToErrorLog(logLevel, line);
        }
    }

    private string? GetScopeText()
    {
        var state = new ScopeTextState();

        scopeProvider.ForEachScope(
            static (
                scope,
                state
            ) =>
            {
                if (scope is null)
                {
                    return;
                }

                state.Values.Add(scope.ToString() ?? string.Empty);
            },
            state
        );

        return state.Values.Count == 0 ? null : string.Join(" ", state.Values);
    }

    private sealed class ScopeTextState
    {
        public List<string> Values { get; } = [];
    }

    private string[] FormatLines(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception,
        CrestronLoggerOptions options,
        string? scopes
    )
    {
        List<string> lines = [];
        var threadId = Environment.CurrentManagedThreadId;
        var scopeText = string.IsNullOrWhiteSpace(scopes) ? string.Empty : $"[ {scopes} ] ";
        var levelText = $"[ {GetLogLevelText(logLevel)} ] ";
        var eventText = eventId.Id != 0 ? $"#{eventId.Id} " : string.Empty;
        var category = options.IncludeCategory ? $"< {categoryName} > " : string.Empty;

        var line = $"@t.{threadId:0000} {scopeText}{levelText}{eventText}{category}| {message}";
        lines.Add(line);


        if (exception == null) return [.. lines];
        var exceptionLines = exception
            .ToString()
            .Split(Environment.NewLine);
        lines.AddRange(exceptionLines);

        return [.. lines];
    }

    private static void WriteToConsole(string line)
    {
        CrestronConsole.PrintLine(line);
    }

    private static void WriteToErrorLog(
        LogLevel logLevel,
        string line
    )
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
            case LogLevel.Warning:
                // Not written to the processor error log to avoid noise; console output above still shows them.
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                ErrorLog.Error(line);
                break;
            case LogLevel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }

    private static string GetLogLevelText(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            var _ => logLevel.ToString()
        };
}