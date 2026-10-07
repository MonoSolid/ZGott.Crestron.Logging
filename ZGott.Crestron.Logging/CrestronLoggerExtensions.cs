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
    /// <param name="builder">The logging builder.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddCrestron(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CrestronLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions<CrestronLoggerOptions, CrestronLoggerProvider>(builder.Services);
        builder
            .Services
            .AddOptions<CrestronLoggerOptions>()
            .Validate(options => Enum.IsDefined(options.MinimumLogLevel), "MinimumLogLevel must be a defined LogLevel.")
            .Validate(
                options => Enum.IsDefined(options.ErrorLogMinimumLevel),
                "ErrorLogMinimumLevel must be a defined LogLevel."
            );

        return builder;
    }

    /// <summary>
    /// Adds a logger that writes to the Crestron SimplSharp ErrorLog and CrestronConsole,
    /// configuring the <see cref="CrestronLoggerOptions"/> with the given delegate.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">The option's configuration delegate.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddCrestron(
        this ILoggingBuilder builder,
        Action<CrestronLoggerOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddCrestron();
        builder.Services.Configure(configure);

        return builder;
    }

    /// <summary>
    /// Adds and configures Crestron logging. Use <see cref="AddCrestron(ILoggingBuilder, Action{CrestronLoggerOptions})"/> instead.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">The option's configuration delegate.</param>
    /// <returns>The logging builder for chaining.</returns>
    [Obsolete("Use AddCrestron instead.")]
    public static ILoggingBuilder AddCrestronLogger(
        this ILoggingBuilder builder,
        Action<CrestronLoggerOptions> configure
    ) =>
        builder.AddCrestron(configure);
}