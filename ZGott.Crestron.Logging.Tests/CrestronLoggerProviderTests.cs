using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ZGott.Crestron.Logging.Tests;

public sealed class CrestronLoggerProviderTests
{
    [Fact]
    public void RegistrationIsIdempotentAndReturnsBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
            {
                Assert.Same(builder, builder.AddCrestron());
                Assert.Same(builder, builder.AddCrestron(options => options.IncludeCategory = false));
            }
        );
        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<CrestronLoggerProvider>(Assert.Single(serviceProvider.GetServices<ILoggerProvider>()));
        Assert.False(
            serviceProvider.GetRequiredService<IOptions<CrestronLoggerOptions>>()
                .Value.IncludeCategory
        );
    }

    [Fact]
    public void ConfigurationBindsProviderAliasAndReloadsExistingLoggers()
    {
        var data = new Dictionary<string, string?>
        {
            ["Logging:Crestron:MinimumLogLevel"] = "Warning",
            ["Logging:Crestron:IncludeScopes"] = "false",
            ["Logging:Crestron:LogToConsole"] = "false",
            ["Logging:Crestron:ErrorLogMinimumLevel"] = "Warning"
        };
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(data);
        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .AddConfiguration(configuration.GetSection("Logging"))
            .AddCrestron()
        );
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<CrestronLoggerOptions>>();
        var provider = serviceProvider
            .GetServices<ILoggerProvider>()
            .OfType<CrestronLoggerProvider>()
            .Single();
        var logger = provider.CreateLogger("Test");

        Assert.False(options.CurrentValue.IncludeScopes);
        Assert.False(options.CurrentValue.LogToConsole);
        Assert.True(logger.IsEnabled(LogLevel.Warning));

        configuration["Logging:Crestron:MinimumLogLevel"] = "Critical";
        ((IConfigurationRoot)configuration).Reload();

        Assert.False(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void MicrosoftFiltersAndProviderMinimumBothApply()
    {
        using var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddFilter<CrestronLoggerProvider>("Test", LogLevel.Warning);
                builder.AddCrestron(options => options.MinimumLogLevel = LogLevel.Error);
            }
        );
        var logger = factory.CreateLogger("Test");

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidConfiguredLevelsAreRejected(bool invalidMinimum)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddCrestron(options =>
                {
                    if (invalidMinimum)
                    {
                        options.MinimumLogLevel = (LogLevel)99;
                    }
                    else
                    {
                        options.ErrorLogMinimumLevel = (LogLevel)99;
                    }
                }
            )
        );
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => serviceProvider
            .GetRequiredService<IOptionsMonitor<CrestronLoggerOptions>>()
            .CurrentValue
        );
    }

    [Fact]
    public void ReplacingScopeProviderUpdatesExistingLoggers()
    {
        using var provider = new CrestronLoggerProvider(new CrestronLoggerOptions());
        var logger = provider.CreateLogger("Test");
        var scopes = new LoggerExternalScopeProvider();
        provider.SetScopeProvider(scopes);

        using var scope = logger.BeginScope("NewScope");
        var values = new List<object?>();
        scopes.ForEachScope(
            (
                state,
                list
            ) => list.Add(state),
            values
        );

        Assert.Equal("NewScope", Assert.Single(values));
        Assert.Same(logger, provider.CreateLogger("Test"));
    }

    [Fact]
    public void CategoriesAreCachedWithOrdinalComparison()
    {
        using var provider = new CrestronLoggerProvider(new CrestronLoggerOptions());

        Assert.Same(provider.CreateLogger("Test"), provider.CreateLogger("Test"));
        Assert.NotSame(provider.CreateLogger("Test"), provider.CreateLogger("test"));
    }

    [Fact]
    public void DisposingProviderUnsubscribesFromOptionsReloads()
    {
        var monitor = new TestOptionsMonitor();
        var provider = new CrestronLoggerProvider(monitor);
        var logger = provider.CreateLogger("Test");
        monitor.Update(new CrestronLoggerOptions { MinimumLogLevel = LogLevel.Critical });
        Assert.False(logger.IsEnabled(LogLevel.Error));

        provider.Dispose();
        monitor.Update(new CrestronLoggerOptions { MinimumLogLevel = LogLevel.Trace });

        Assert.True(monitor.SubscriptionDisposed);
        Assert.False(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void NullRegistrationAndProviderArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CrestronLoggerExtensions.AddCrestron(null!));
        var services = new ServiceCollection();
        services.AddLogging(builder => Assert.Throws<ArgumentNullException>(() => builder.AddCrestron(null!)));
        Assert.Throws<ArgumentNullException>(() => new CrestronLoggerProvider((CrestronLoggerOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new CrestronLoggerProvider(
                (IOptionsMonitor<CrestronLoggerOptions>)null!
            )
        );
        using var provider = new CrestronLoggerProvider(new CrestronLoggerOptions());
        Assert.Throws<ArgumentNullException>(() => provider.SetScopeProvider(null!));
        Assert.Throws<ArgumentNullException>(() => provider.CreateLogger(null!));
    }

    [Fact]
    public void LegacyAliasesConfigureCanonicalOptions()
    {
#pragma warning disable CS0618
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddCrestronLogger(options => options.MinLevel = LogLevel.Warning));
#pragma warning restore CS0618
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(
            LogLevel.Warning,
            serviceProvider.GetRequiredService<IOptions<CrestronLoggerOptions>>()
                .Value.MinimumLogLevel
        );
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<CrestronLoggerOptions>
    {
        private Action<CrestronLoggerOptions, string?>? onChange;

        public CrestronLoggerOptions CurrentValue { get; private set; } = new();
        public bool SubscriptionDisposed { get; private set; }

        public CrestronLoggerOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<CrestronLoggerOptions, string?> listener)
        {
            onChange = listener;
            return new Subscription(this);
        }

        public void Update(CrestronLoggerOptions options)
        {
            CurrentValue = options;
            onChange?.Invoke(options, null);
        }

        private sealed class Subscription(TestOptionsMonitor monitor) : IDisposable
        {
            public void Dispose()
            {
                monitor.SubscriptionDisposed = true;
                monitor.onChange = null;
            }
        }
    }
}