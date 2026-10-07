using Crestron.SimplSharp;
using Microsoft.Extensions.Logging;

namespace ZGott.Crestron.Logging;

internal interface ICrestronLogOutput
{
    void WriteToConsole(string line);

    void WriteToErrorLog(
        LogLevel logLevel,
        string line
    );
}

internal sealed class CrestronLogOutput : ICrestronLogOutput
{
    internal static CrestronLogOutput Instance { get; } = new();

    public void WriteToConsole(string line) => CrestronConsole.PrintLine(line);

    public void WriteToErrorLog(
        LogLevel logLevel,
        string line
    )
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
                ErrorLog.Notice(line);
                break;
            case LogLevel.Warning:
                ErrorLog.Warn(line);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                ErrorLog.Error(line);
                break;
            case LogLevel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, "An enabled log level is required.");
        }
    }
}