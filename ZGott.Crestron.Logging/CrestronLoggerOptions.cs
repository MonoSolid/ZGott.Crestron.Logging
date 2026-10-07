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
    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Whether the logger category name is included in the formatted output. Defaults to true.
    /// </summary>
    public bool IncludeCategory { get; set; } = true;
}