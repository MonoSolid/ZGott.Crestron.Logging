using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZGott.Crestron.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that creates <see cref="CrestronLogger"/> instances writing
/// to the Crestron SimplSharp ErrorLog and CrestronConsole.
/// </summary>
[ProviderAlias("Crestron")]
public sealed class CrestronLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, CrestronLogger> loggers = new(StringComparer.Ordinal);
    private volatile IExternalScopeProvider configuredScopeProvider = new LoggerExternalScopeProvider();
    private volatile CrestronLoggerOptions currentOptions;
    private readonly IDisposable? optionsReloadToken;

    /// <summary>
    /// Creates a provider whose options are updated when the configuration changes.
    /// </summary>
    /// <param name="options">The option's monitor.</param>
    public CrestronLoggerProvider(IOptionsMonitor<CrestronLoggerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        currentOptions = options.CurrentValue;
        currentOptions.Validate();
        optionsReloadToken = options.OnChange(updated =>
            {
                updated.Validate();
                currentOptions = updated;
            }
        );
    }

    /// <summary>
    /// Creates a provider with the specified options.
    /// </summary>
    /// <param name="options">The provider options.</param>
    public CrestronLoggerProvider(CrestronLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        currentOptions = options;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        return loggers.GetOrAdd(
            categoryName,
            name => new CrestronLogger(
                name,
                () => currentOptions,
                () => configuredScopeProvider,
                CrestronLogOutput.Instance
            )
        );
    }

    /// <inheritdoc/>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);

        configuredScopeProvider = scopeProvider;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        optionsReloadToken?.Dispose();
        loggers.Clear();
    }
}