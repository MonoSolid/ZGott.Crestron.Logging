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
    private IExternalScopeProvider configuredScopeProvider = new LoggerExternalScopeProvider();
    private CrestronLoggerOptions currentOptions;
    private readonly IDisposable? optionsReloadToken;

    public CrestronLoggerProvider(IOptionsMonitor<CrestronLoggerOptions> options)
    {
        currentOptions = options.CurrentValue;
        optionsReloadToken = options.OnChange(updated => currentOptions = updated);
    }

    public CrestronLoggerProvider(CrestronLoggerOptions options)
    {
        currentOptions = options;
    }

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new CrestronLogger(name, () => currentOptions, configuredScopeProvider));

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);

        configuredScopeProvider = scopeProvider;
        loggers.Clear();
    }

    public void Dispose()
    {
        optionsReloadToken?.Dispose();
        loggers.Clear();
    }
}