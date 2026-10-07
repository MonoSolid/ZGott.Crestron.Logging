using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace ZGott.Crestron.Logging;

/// <summary>
/// Extension methods for registering the <see cref="CrestronLoggerProvider"/> with an
/// <see cref="ILoggingBuilder"/>.
/// </summary>
public static class CrestronLoggerExtensions
{
    /// <summary>
    /// Adds a logger that writes to the Crestron SimplSharp ErrorLog and CrestronConsole.
    /// </summary>
    public static ILoggingBuilder AddCrestronLogger(this ILoggingBuilder builder)
    {
        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CrestronLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions<CrestronLoggerOptions, CrestronLoggerProvider>(builder.Services);

        return builder;
    }

    /// <summary>
    /// Adds a logger that writes to the Crestron SimplSharp ErrorLog and CrestronConsole,
    /// configuring the <see cref="CrestronLoggerOptions"/> with the given delegate.
    /// </summary>
    public static ILoggingBuilder AddCrestronLogger(
        this ILoggingBuilder builder,
        Action<CrestronLoggerOptions> configure
    )
    {
        builder.AddCrestronLogger();
        builder.Services.Configure(configure);

        return builder;
    }
}