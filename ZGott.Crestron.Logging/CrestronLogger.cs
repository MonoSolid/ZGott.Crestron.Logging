using Microsoft.Extensions.Logging;

namespace ZGott.Crestron.Logging;

/// <summary>
/// An <see cref="ILogger"/> implementation that writes to the Crestron SIMPL# ErrorLog and CrestronConsole.
/// </summary>
public sealed class CrestronLogger : ILogger
{
    private readonly string categoryName;
    private readonly Func<CrestronLoggerOptions> getCurrentOptions;
    private readonly Func<IExternalScopeProvider> getScopeProvider;
    private readonly ICrestronLogOutput output;

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="getCurrentOptions">A function returning the current options.</param>
    /// <param name="scopeProvider">The scope provider.</param>
    public CrestronLogger(
        string categoryName,
        Func<CrestronLoggerOptions> getCurrentOptions,
        IExternalScopeProvider scopeProvider
    ) : this(
        categoryName,
        getCurrentOptions,
        () => scopeProvider,
        CrestronLogOutput.Instance
    )
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);
    }

    internal CrestronLogger(
        string categoryName,
        Func<CrestronLoggerOptions> getCurrentOptions,
        Func<IExternalScopeProvider> getScopeProvider,
        ICrestronLogOutput output
    )
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        ArgumentNullException.ThrowIfNull(getCurrentOptions);
        ArgumentNullException.ThrowIfNull(getScopeProvider);
        ArgumentNullException.ThrowIfNull(output);

        this.categoryName = categoryName;
        this.getCurrentOptions = getCurrentOptions;
        this.getScopeProvider = getScopeProvider;
        this.output = output;
    }

    /// <inheritdoc/>
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        getScopeProvider()
            .Push(state);

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => IsEnabled(logLevel, getCurrentOptions());

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var options = getCurrentOptions();
        if (!IsEnabled(logLevel, options))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var scopes = options.IncludeScopes ? GetScopeText() : null;
        foreach (var line in FormatLines(
                     logLevel,
                     eventId,
                     message,
                     exception,
                     options,
                     scopes
                 ))
        {
            if (options.LogToConsole)
            {
                output.WriteToConsole(line);
            }

            if (options.LogToErrorLog && logLevel >= options.ErrorLogMinimumLevel)
            {
                output.WriteToErrorLog(logLevel, line);
            }
        }
    }

    private static bool IsEnabled(
        LogLevel logLevel,
        CrestronLoggerOptions options
    )
    {
        if (!Enum.IsDefined(logLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, "A defined LogLevel is required.");
        }

        options.Validate();
        return logLevel != LogLevel.None && logLevel >= options.MinimumLogLevel && (options.LogToConsole ||
            (options.LogToErrorLog && logLevel >= options.ErrorLogMinimumLevel));
    }

    private string? GetScopeText()
    {
        var values = new List<string>();
        getScopeProvider()
            .ForEachScope(
                static (
                    scope,
                    scopes
                ) =>
                {
                    if (scope is not null)
                    {
                        scopes.Add(scope.ToString() ?? string.Empty);
                    }
                },
                values
            );

        return values.Count == 0 ? null : string.Join(" ", values);
    }

    private IEnumerable<string> FormatLines(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception,
        CrestronLoggerOptions options,
        string? scopes
    )
    {
        var threadText = options.IncludeThreadId ? $"@t.{Environment.CurrentManagedThreadId:0000} " : string.Empty;
        var scopeText = string.IsNullOrWhiteSpace(scopes) ? string.Empty : $"[ {scopes} ] ";
        var levelText = $"[ {GetLogLevelText(logLevel)} ] ";
        var eventText = options.IncludeEventId && eventId.Id != 0 ? $"#{eventId.Id} " : string.Empty;
        var category = options.IncludeCategory ? $"< {categoryName} > " : string.Empty;

        yield return $"{threadText}{scopeText}{levelText}{eventText}{category}| {message}";

        if (exception is null) yield break;
        foreach (var line in exception
                     .ToString()
                     .Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            yield return line;
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