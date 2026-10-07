using Microsoft.Extensions.Logging;

namespace ZGott.Crestron.Logging;

/// <summary>
/// Options for the <see cref="CrestronLoggerProvider"/>.
/// </summary>
public class CrestronLoggerOptions
{
    /// <summary>
    /// The minimum <see cref="LogLevel"/> that will be logged. Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the minimum log level. Use <see cref="MinimumLogLevel"/> instead.
    /// </summary>
    [Obsolete("Use MinimumLogLevel instead.")]
    public LogLevel MinLevel
    {
        get => MinimumLogLevel;
        set => MinimumLogLevel = value;
    }

    /// <summary>
    /// Whether the logger category name is included in the formatted output. Defaults to true.
    /// </summary>
    public bool IncludeCategory { get; set; } = true;

    /// <summary>
    /// Gets or sets whether logging scopes are included. Defaults to true.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the managed thread ID is included. Defaults to true.
    /// </summary>
    public bool IncludeThreadId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether nonzero event IDs are included. Defaults to true.
    /// </summary>
    public bool IncludeEventId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether enabled messages are written to CrestronConsole. Defaults to true.
    /// </summary>
    public bool LogToConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets whether enabled messages are written to the processor error log. Defaults to true.
    /// </summary>
    public bool LogToErrorLog { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum level written to the processor error log.
    /// Defaults to <see cref="LogLevel.Error"/>. <see cref="LogLevel.None"/> disables this destination.
    /// </summary>
    public LogLevel ErrorLogMinimumLevel { get; set; } = LogLevel.Error;

    internal void Validate()
    {
        if (!Enum.IsDefined(MinimumLogLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumLogLevel),
                MinimumLogLevel,
                "A defined LogLevel is required."
            );
        }

        if (!Enum.IsDefined(ErrorLogMinimumLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ErrorLogMinimumLevel),
                ErrorLogMinimumLevel,
                "A defined LogLevel is required."
            );
        }
    }
}