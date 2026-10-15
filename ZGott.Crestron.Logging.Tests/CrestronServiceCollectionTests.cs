using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ZGott.Crestron.Logging.Tests;

public sealed class CrestronServiceCollectionTests
{
    [Fact]
    public void RegistrationAddsLoggingServicesAndReturnsCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddCrestron());
        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<CrestronServiceCollectionTests>>();

        Assert.NotNull(serviceProvider.GetRequiredService<ILoggerFactory>());
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.Equal(LogLevel.Trace,
            serviceProvider.GetRequiredService<IOptions<CrestronLoggerOptions>>().Value.MinimumLogLevel
        );
    }

    [Fact]
    public void MixedRegistrationsAreIdempotentAndConfigureOptions()
    {
        var services = new ServiceCollection();
        services.AddCrestron();
        services.AddLogging(builder => builder.AddCrestron());
        Assert.Same(services, services.AddCrestron(options => options.MinimumLogLevel = LogLevel.Error));
        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<CrestronServiceCollectionTests>>();

        Assert.IsType<CrestronLoggerProvider>(Assert.Single(serviceProvider.GetServices<ILoggerProvider>()));
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void RegistrationPreservesExistingProvidersAndFilters()
    {
        var services = new ServiceCollection();
        using var existingProvider = new TestLoggerProvider();
        services.AddLogging(builder => builder
            .AddProvider(existingProvider)
            .SetMinimumLevel(LogLevel.Critical)
        );
        services.AddCrestron();
        using var serviceProvider = services.BuildServiceProvider();
        var providers = serviceProvider.GetServices<ILoggerProvider>().ToArray();
        var logger = serviceProvider.GetRequiredService<ILogger<CrestronServiceCollectionTests>>();

        Assert.Equal(2, providers.Length);
        Assert.Contains(existingProvider, providers);
        Assert.Single(providers.OfType<CrestronLoggerProvider>());
        Assert.False(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HostConfigurationReloadsExistingLoggers(bool registerCrestronFirst)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Error",
                ["Logging:Crestron:LogLevel:MyApp"] = "Warning",
                ["Logging:Crestron:MinimumLogLevel"] = "Error"
            }
        );
        var services = new ServiceCollection();
        if (registerCrestronFirst)
        {
            services.AddCrestron();
        }

        services.AddLogging(builder => builder.AddConfiguration(configuration.GetSection("Logging")));
        if (!registerCrestronFirst)
        {
            services.AddCrestron();
        }

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.ControlSystem");

        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));

        configuration["Logging:Crestron:MinimumLogLevel"] = "Trace";
        configuration["Logging:Crestron:LogLevel:MyApp"] = "Debug";
        ((IConfigurationRoot)configuration).Reload();

        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));

        configuration["Logging:Crestron:LogLevel:MyApp"] = "Trace";
        ((IConfigurationRoot)configuration).Reload();

        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        var services = new ServiceCollection();
        services.AddCrestron(options => options.MinimumLogLevel = (LogLevel)99);
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptionsMonitor<CrestronLoggerOptions>>().CurrentValue
        );
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCrestron());
        Assert.Throws<ArgumentNullException>(() => services.AddCrestron(_ => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddCrestron(null!));
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

        public void Dispose() { }
    }
}
