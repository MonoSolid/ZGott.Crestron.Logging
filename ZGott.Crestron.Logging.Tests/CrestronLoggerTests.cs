using Microsoft.Extensions.Logging;
using Xunit;

namespace ZGott.Crestron.Logging.Tests;

public sealed class CrestronLoggerTests
{
    [Fact]
    public void DefaultOptionsPreserveExistingOutput()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions(), output);
        using var scope = logger.BeginScope("Room-101");

        logger.LogInformation(new EventId(42), "Connected");

        var line = Assert.Single(output.ConsoleLines);
        Assert.Matches(@"^@t\.[0-9]{4,} \[ Room-101 \] \[ info \] #42 < TestCategory > \| Connected$", line);
        Assert.Empty(output.ErrorLines);
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void DefaultErrorLogThresholdIsError(
        LogLevel level,
        bool writesErrorLog
    )
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions { MinimumLogLevel = LogLevel.Trace }, output);

        logger.Log(level, "Message");

        Assert.Single(output.ConsoleLines);
        Assert.Equal(writesErrorLog ? 1 : 0, output.ErrorLines.Count);
        if (writesErrorLog)
        {
            Assert.Equal((level, output.ConsoleLines[0]), output.ErrorLines[0]);
        }
    }

    [Fact]
    public void FormattingOptionsCanDisableAllOptionalFields()
    {
        var output = new RecordingOutput();
        var options = new CrestronLoggerOptions
        {
            IncludeCategory = false,
            IncludeScopes = false,
            IncludeThreadId = false,
            IncludeEventId = false
        };
        var logger = CreateLogger(options, output);
        using var scope = logger.BeginScope("HiddenScope");

        logger.LogInformation(new EventId(42), "Message");

        Assert.Equal("[ info ] | Message", Assert.Single(output.ConsoleLines));
    }

    [Fact]
    public void NestedScopesAreIncludedOutermostFirst()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions { IncludeThreadId = false }, output);
        using var outer = logger.BeginScope("Outer");
        using var inner = logger.BeginScope("Inner");

        logger.LogInformation("Message");

        Assert.StartsWith("[ Outer Inner ] [ info ]", Assert.Single(output.ConsoleLines));
    }

    [Fact]
    public void ErrorLogOnlyUsesItsOwnThreshold()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(
            new CrestronLoggerOptions
            {
                LogToConsole = false,
                ErrorLogMinimumLevel = LogLevel.Warning
            },
            output
        );

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        logger.LogWarning("Warning");
        logger.LogError("Error");

        Assert.Empty(output.ConsoleLines);
        Assert.Equal(new[] { LogLevel.Warning, LogLevel.Error }, output.ErrorLines.Select(entry => entry.Level));
    }

    [Fact]
    public void ProviderMinimumStillAppliesToErrorLog()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(
            new CrestronLoggerOptions
            {
                MinimumLogLevel = LogLevel.Critical,
                ErrorLogMinimumLevel = LogLevel.Trace
            },
            output
        );

        logger.LogError("Filtered");
        logger.LogCritical("Critical");

        Assert.Single(output.ConsoleLines);
        Assert.Equal(
            LogLevel.Critical,
            Assert.Single(output.ErrorLines)
                .Level
        );
    }

    [Fact]
    public void ConsoleOnlyDoesNotWriteErrorLog()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions { LogToErrorLog = false }, output);

        logger.LogCritical("Message");

        Assert.Single(output.ConsoleLines);
        Assert.Empty(output.ErrorLines);
    }

    [Fact]
    public void ErrorLogNoneDisablesOnlyThatDestination()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions { ErrorLogMinimumLevel = LogLevel.None }, output);

        logger.LogCritical("Message");

        Assert.Single(output.ConsoleLines);
        Assert.Empty(output.ErrorLines);
    }

    [Fact]
    public void DisabledDestinationsDoNotInvokeFormatter()
    {
        var logger = CreateLogger(
            new CrestronLoggerOptions
            {
                LogToConsole = false,
                LogToErrorLog = false
            },
            new RecordingOutput()
        );

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        logger.Log(
            LogLevel.Critical,
            default,
            "Message",
            null,
            (
                _,
                _
            ) => throw new InvalidOperationException("Formatter should not run.")
        );
    }

    [Fact]
    public void MinimumNoneDisablesAllLevels()
    {
        var logger = CreateLogger(new CrestronLoggerOptions { MinimumLogLevel = LogLevel.None }, new RecordingOutput());

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void ExceptionsAreSplitAcrossPlatformIndependentNewlines()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions(), output);

        logger.LogError(new MixedNewlineException(), "Failure");

        Assert.Equal(new[] { "First", "Second", "Third", "Fourth" }, output.ConsoleLines.Skip(1));
        Assert.Equal(output.ConsoleLines, output.ErrorLines.Select(entry => entry.Line));
    }

    [Fact]
    public void EmptyMessagesAreSkippedUnlessAnExceptionIsPresent()
    {
        var output = new RecordingOutput();
        var logger = CreateLogger(new CrestronLoggerOptions(), output);

        logger.LogInformation(string.Empty);
        Assert.Empty(output.ConsoleLines);

        logger.LogError(new InvalidOperationException("Failure"), string.Empty);
        Assert.Equal(2, output.ConsoleLines.Count);
        Assert.Contains("Failure", output.ConsoleLines[1]);
    }

    [Fact]
    public void LogReadsOneOptionsSnapshot()
    {
        var output = new RecordingOutput();
        var reads = 0;
        var logger = new CrestronLogger(
            "Test",
            () =>
            {
                reads++;
                return new CrestronLoggerOptions { LogToConsole = reads == 1 };
            },
            () => new LoggerExternalScopeProvider(),
            output
        );

        logger.LogInformation("Message");

        Assert.Equal(1, reads);
        Assert.Single(output.ConsoleLines);
    }

    [Fact]
    public void InvalidOptionLevelsFailExplicitly()
    {
        var options = new CrestronLoggerOptions { MinimumLogLevel = (LogLevel)99 };
        var logger = CreateLogger(options, new RecordingOutput());

        Assert.Throws<ArgumentOutOfRangeException>(() => logger.IsEnabled(LogLevel.Information));
        options.MinimumLogLevel = LogLevel.Information;
        options.ErrorLogMinimumLevel = (LogLevel)(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void NullPublicConstructorArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CrestronLogger(
                null!,
                () => new CrestronLoggerOptions(),
                new LoggerExternalScopeProvider()
            )
        );
        Assert.Throws<ArgumentNullException>(() => new CrestronLogger(
                "Test",
                null!,
                new LoggerExternalScopeProvider()
            )
        );
        Assert.Throws<ArgumentNullException>(() => new CrestronLogger(
                "Test",
                () => new CrestronLoggerOptions(),
                null!
            )
        );
    }

    [Fact]
    public void NullFormatterIsRejectedForEnabledMessages()
    {
        var logger = CreateLogger(new CrestronLoggerOptions(), new RecordingOutput());

        Assert.Throws<ArgumentNullException>(() => logger.Log(
                LogLevel.Information,
                default,
                "Message",
                null,
                null!
            )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void UndefinedMessageLevelsAreRejected(int level)
    {
        var logger = CreateLogger(new CrestronLoggerOptions(), new RecordingOutput());

        Assert.Throws<ArgumentOutOfRangeException>(() => logger.IsEnabled((LogLevel)level));
    }

    private static CrestronLogger CreateLogger(
        CrestronLoggerOptions options,
        RecordingOutput output
    )
    {
        var scopes = new LoggerExternalScopeProvider();
        return new CrestronLogger(
            "TestCategory",
            () => options,
            () => scopes,
            output
        );
    }

    private sealed class RecordingOutput : ICrestronLogOutput
    {
        public List<string> ConsoleLines { get; } = [];
        public List<(LogLevel Level, string Line)> ErrorLines { get; } = [];

        public void WriteToConsole(string line) => ConsoleLines.Add(line);

        public void WriteToErrorLog(
            LogLevel logLevel,
            string line
        ) =>
            ErrorLines.Add((logLevel, line));
    }

    private sealed class MixedNewlineException : Exception
    {
        public override string ToString() => "First\r\nSecond\nThird\rFourth";
    }
}