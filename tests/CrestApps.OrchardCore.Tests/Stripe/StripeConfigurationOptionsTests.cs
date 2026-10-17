using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OrchardCore.Environment.Shell.Configuration;
using StripeStartup = CrestApps.OrchardCore.Stripe.Startup;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// Stripe can be configured from the tenant's application configuration (the <c>CrestApps_Stripe</c> section) as well
/// as from <b>Settings &gt; Payments &gt; Stripe</b>. A value present in the configuration wins over the stored one, one
/// value at a time, so a tenant that only has environment variables can take payments.
/// </summary>
public sealed class StripeConfigurationOptionsTests
{
    private const string TestPublishableKey = "pk_test_configured";
    private const string TestSecretKey = "sk_test_configured";
    private const string TestWebhookSecret = "whsec_configured";

    [Fact]
    public void Startup_BindsTheCrestAppsStripeSectionFromTheTenantConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new StripeStartup(CreateShellConfiguration(new Dictionary<string, string>
        {
            ["CrestApps_Stripe:IsLive"] = "true",
            ["CrestApps_Stripe:Test:PublishableKey"] = TestPublishableKey,
            ["CrestApps_Stripe:Test:SecretKey"] = TestSecretKey,
            ["CrestApps_Stripe:Test:WebhookSecret"] = TestWebhookSecret,
            ["CrestApps_Stripe:Live:PublishableKey"] = "pk_live_configured",
            ["CrestApps_Stripe:Live:SecretKey"] = "sk_live_configured",
            ["CrestApps_Stripe:Live:WebhookSecret"] = "whsec_live_configured",
        }));

        // Act
        startup.ConfigureServices(services);

        var options = services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<StripeConfigurationOptions>>()
            .Value;

        // Assert
        Assert.True(options.IsLive);
        Assert.Equal(TestPublishableKey, options.Test.PublishableKey);
        Assert.Equal(TestSecretKey, options.Test.SecretKey);
        Assert.Equal(TestWebhookSecret, options.Test.WebhookSecret);
        Assert.Equal("pk_live_configured", options.Live.PublishableKey);
        Assert.Equal("sk_live_configured", options.Live.SecretKey);
        Assert.Equal("whsec_live_configured", options.Live.WebhookSecret);
    }

    [Fact]
    public void Startup_WithoutTheSection_LeavesEveryValueUnset()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new StripeStartup(CreateShellConfiguration(new Dictionary<string, string>()));

        // Act
        startup.ConfigureServices(services);

        var options = services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<StripeConfigurationOptions>>()
            .Value;

        // Assert
        Assert.Null(options.IsLive);
        Assert.False(options.GetKeys(isLive: false).HasAny);
        Assert.False(options.GetKeys(isLive: true).HasAny);
    }

    [Fact]
    public void Configure_WithOnlyConfiguration_ProducesAUsableTestModeConnection()
    {
        // Arrange
        // A throwaway tenant with environment variables only: nothing was ever saved on the settings page.
        var configuration = new StripeConfigurationOptions
        {
            Test = new StripeConfigurationKeys
            {
                PublishableKey = TestPublishableKey,
                SecretKey = TestSecretKey,
                WebhookSecret = TestWebhookSecret,
            },
        };

        // Act
        var options = Configure(new StripeSettings(), configuration, new EphemeralDataProtectionProvider());

        // Assert
        Assert.False(options.IsLive);
        Assert.True(options.IsConfigured);
        Assert.Equal(TestPublishableKey, options.PublishableKey);
        Assert.Equal(TestSecretKey, options.ApiKey);
        Assert.Equal(TestWebhookSecret, options.WebhookSecret);
    }

    [Fact]
    public void Configure_ConfigurationWinsOverTheStoredValue_OneValueAtATime()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings
        {
            TestPublishableKey = "pk_test_stored",
            TestPrivateSecret = protector.Protect("sk_test_stored"),
            TestWebhookSecret = protector.Protect("whsec_stored"),
        };

        // Only the secret key comes from the configuration.
        var configuration = new StripeConfigurationOptions
        {
            Test = new StripeConfigurationKeys
            {
                SecretKey = $"  {TestSecretKey}  ",
            },
        };

        // Act
        var options = Configure(settings, configuration, dataProtectionProvider);

        // Assert
        Assert.Equal(TestSecretKey, options.ApiKey);
        Assert.Equal("pk_test_stored", options.PublishableKey);
        Assert.Equal("whsec_stored", options.WebhookSecret);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public void Configure_TheConfiguredModeWinsOverTheStoredMode(bool? configuredIsLive, bool storedIsLive, bool expectedIsLive)
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings
        {
            IsLive = storedIsLive,
            LivePublishableKey = "pk_live_stored",
            LivePrivateSecret = protector.Protect("sk_live_stored"),
            TestPublishableKey = "pk_test_stored",
            TestPrivateSecret = protector.Protect("sk_test_stored"),
        };

        var configuration = new StripeConfigurationOptions
        {
            IsLive = configuredIsLive,
        };

        // Act
        var options = Configure(settings, configuration, dataProtectionProvider);

        // Assert
        Assert.Equal(expectedIsLive, options.IsLive);
        Assert.Equal(expectedIsLive ? "sk_live_stored" : "sk_test_stored", options.ApiKey);
        Assert.Equal(expectedIsLive ? "pk_live_stored" : "pk_test_stored", options.PublishableKey);
    }

    [Fact]
    public void Configure_WhenTheConfigurationSuppliesTheSecrets_NeverReadsTheStoredOnes()
    {
        // Arrange
        // The stored secrets were protected by another key ring, so reading them would throw. A tenant whose secrets
        // come from the configuration must not depend on the database holding readable ones.
        var otherKeyRing = new EphemeralDataProtectionProvider().CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings
        {
            TestPrivateSecret = otherKeyRing.Protect("sk_test_stored"),
            TestWebhookSecret = otherKeyRing.Protect("whsec_stored"),
        };

        var configuration = new StripeConfigurationOptions
        {
            Test = new StripeConfigurationKeys
            {
                SecretKey = TestSecretKey,
                WebhookSecret = TestWebhookSecret,
            },
        };

        // Act
        var options = Configure(settings, configuration, new EphemeralDataProtectionProvider());

        // Assert
        Assert.Equal(TestSecretKey, options.ApiKey);
        Assert.Equal(TestWebhookSecret, options.WebhookSecret);
    }

    [Fact]
    public void Configure_WithoutConfiguration_UsesTheStoredSettings()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings
        {
            TestPublishableKey = "pk_test_stored",
            TestPrivateSecret = protector.Protect("sk_test_stored"),
            TestWebhookSecret = protector.Protect("whsec_stored"),
        };

        // Act
        var options = Configure(settings, new StripeConfigurationOptions(), dataProtectionProvider);

        // Assert
        Assert.False(options.IsLive);
        Assert.Equal("pk_test_stored", options.PublishableKey);
        Assert.Equal("sk_test_stored", options.ApiKey);
        Assert.Equal("whsec_stored", options.WebhookSecret);
    }

    private static StripeOptions Configure(
        StripeSettings settings,
        StripeConfigurationOptions configuration,
        IDataProtectionProvider dataProtectionProvider)
    {
        var options = new StripeOptions();

        new StripeOptionsConfiguration(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            Options.Create(configuration))
            .Configure(options);

        return options;
    }

    private static TestShellConfiguration CreateShellConfiguration(Dictionary<string, string> values)
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build());

    private sealed class TestShellConfiguration : IShellConfiguration
    {
        private readonly IConfiguration _configuration;

        public TestShellConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
            => _configuration.GetChildren();

        public IChangeToken GetReloadToken()
            => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key)
            => _configuration.GetSection(key);
    }
}
