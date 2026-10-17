using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The test payment method confirms every payment without moving money, so enabling it by mistake on a production
/// site must not let anyone "pay" without paying. It is registered in Development, or wherever configuration
/// explicitly allows it, and nowhere else.
/// </summary>
public sealed class TestProviderStartupTests
{
    [Fact]
    public void ConfigureServices_InDevelopment_RegistersTheProviderAndItsPanel()
    {
        // Arrange
        var logger = new RecordingLogger();
        var startup = CreateStartup(Environments.Development, allowOutsideDevelopment: null, logger);
        var services = new ServiceCollection();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentRefundProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckoutSavedPaymentMethodProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckoutRecurringPaymentProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<CheckoutFlowPaymentMethod>));
        Assert.Equal(0, logger.Count(LogLevel.Warning));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ConfigureServices_OutsideDevelopment_RegistersNothingAndWarns(string environmentName)
    {
        // Arrange
        var logger = new RecordingLogger();
        var startup = CreateStartup(environmentName, allowOutsideDevelopment: null, logger);
        var services = new ServiceCollection();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentRefundProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<CheckoutFlowPaymentMethod>));
        Assert.Equal(1, logger.Count(LogLevel.Warning));
    }

    [Fact]
    public void ConfigureServices_InProduction_WhenTheFlagIsFalse_RegistersNothing()
    {
        // Arrange
        var startup = CreateStartup(Environments.Production, allowOutsideDevelopment: "false", new RecordingLogger());
        var services = new ServiceCollection();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentProvider));
    }

    [Fact]
    public void ConfigureServices_InProduction_WhenConfigurationAllowsIt_RegistersTheProviderAndWarns()
    {
        // Arrange
        var logger = new RecordingLogger();
        var startup = CreateStartup(Environments.Production, allowOutsideDevelopment: "true", logger);
        var services = new ServiceCollection();

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICheckoutPaymentProvider));
        Assert.Equal(1, logger.Count(LogLevel.Warning));
    }

    [Fact]
    public void Options_AreActiveOnlyInDevelopmentUnlessAllowed()
    {
        // Arrange
        var production = CreateEnvironment(Environments.Production);
        var development = CreateEnvironment(Environments.Development);

        // Act & Assert
        Assert.True(new TestPaymentProviderOptions().IsActive(development));
        Assert.False(new TestPaymentProviderOptions().IsActive(production));
        Assert.True(new TestPaymentProviderOptions { AllowOutsideDevelopment = true }.IsActive(production));
    }

    private static TestProviderStartup CreateStartup(string environmentName, string allowOutsideDevelopment, RecordingLogger logger)
    {
        var values = new Dictionary<string, string>();

        if (allowOutsideDevelopment is not null)
        {
            values[TestPaymentProviderOptions.AllowOutsideDevelopmentKey] = allowOutsideDevelopment;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(config => config.GetSection(It.IsAny<string>()))
            .Returns((string key) => configuration.GetSection(key));

        return new TestProviderStartup(CreateEnvironment(environmentName), shellConfiguration.Object, logger);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(env => env.EnvironmentName).Returns(environmentName);

        return environment.Object;
    }

    private sealed class RecordingLogger : ILogger<TestProviderStartup>
    {
        private readonly List<LogLevel> _levels = [];

        public int Count(LogLevel level)
            => _levels.Count(logged => logged == level);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            _levels.Add(logLevel);
        }
    }
}
