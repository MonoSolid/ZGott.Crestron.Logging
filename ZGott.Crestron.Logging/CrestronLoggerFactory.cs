using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ZGott.Crestron.Logging;

/// <summary>
/// Creates standalone logging factories configured for Crestron output.
/// </summary>
public static class CrestronLoggerFactory
{
    /// <summary>
    /// Creates a factory with Crestron logging and a default Microsoft minimum level of Trace.
    /// </summary>
    /// <param name="configure">Optional additional logging configuration, applied after the defaults.</param>
    /// <returns>A logging factory that the caller must dispose when the application shuts down.</returns>
    public static ILoggerFactory Create(Action<ILoggingBuilder>? configure = null) =>
        LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddCrestron();
                configure?.Invoke(builder);
            }
        );

    /// <summary>
    /// Creates a factory that reads filters and Crestron options from the root configuration's Logging section.
    /// Existing loggers update when the configuration source publishes reload notifications.
    /// </summary>
    /// <param name="configuration">The application's root configuration, not the Logging section.</param>
    /// <param name="configure">Optional additional logging configuration, applied after configuration binding.</param>
    /// <returns>A logging factory that the caller must dispose when the application shuts down.</returns>
    /// <remarks>
    /// The caller owns the configuration and must keep it alive for the lifetime of the factory.
    /// For JSON files, enable reloadOnChange when building the configuration.
    /// </remarks>
    public static ILoggerFactory Create(
        IConfiguration configuration,
        Action<ILoggingBuilder>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Create(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                configure?.Invoke(builder);
            }
        );
    }
}
