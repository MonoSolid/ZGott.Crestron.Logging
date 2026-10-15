using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ZGott.Crestron.Logging.Tests;

public sealed class CrestronLoggerFactoryTests
{
    [Fact]
    public void DefaultFactoryEnablesTrace()
    {
        using var factory = CrestronLoggerFactory.Create();
        var logger = factory.CreateLogger("Test");

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void BuilderCallbackCanOverrideDefaults()
    {
        using var factory = CrestronLoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddCrestron(options => options.MinimumLogLevel = LogLevel.Error);
            }
        );
        var logger = factory.CreateLogger("Test");

        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void ConfigurationBindsOptionsAndFiltersAndReloadsBoth()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Error",
                ["Logging:Crestron:LogLevel:Default"] = "Debug",
                ["Logging:Crestron:MinimumLogLevel"] = "Warning"
            }
        );
        using var factory = CrestronLoggerFactory.Create(configuration);
        var logger = factory.CreateLogger("Test");

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Warning));

        configuration["Logging:Crestron:MinimumLogLevel"] = "Trace";
        ((IConfigurationRoot)configuration).Reload();

        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));

        configuration["Logging:Crestron:LogLevel:Default"] = "Trace";
        ((IConfigurationRoot)configuration).Reload();

        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void ConfigurationCallbackCanConfigureProviderOptions()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Trace",
                ["Logging:Crestron:MinimumLogLevel"] = "Warning"
            }
        );
        using var factory = CrestronLoggerFactory.Create(configuration,
            builder => builder.AddCrestron(options => options.MinimumLogLevel = LogLevel.Debug)
        );
        var logger = factory.CreateLogger("Test");

        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void NullConfigurationIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CrestronLoggerFactory.Create((IConfiguration)null!));
    }

}

