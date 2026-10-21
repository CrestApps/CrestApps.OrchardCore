using System.Security.Claims;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A value the application configuration supplies is shown read-only on <b>Settings &gt; Payments &gt; Stripe</b> and
/// wins at runtime, so saving the page must never replace the stored value with whatever the form posts for it.
/// </summary>
public sealed class StripeSettingsDisplayDriverConfigurationTests
{
    [Fact]
    public async Task UpdateAsync_LeavesConfiguredValuesAlone_AndSavesTheOthers()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var storedTestSecret = protector.Protect("sk_test_stored");
        var settings = new StripeSettings
        {
            IsLive = false,
            TestPublishableKey = "pk_test_stored",
            TestPrivateSecret = storedTestSecret,
        };

        // The test publishable and secret keys and the mode come from the configuration; the webhook secret does not.
        var configuration = new StripeConfigurationOptions
        {
            IsLive = false,
            Test = new StripeConfigurationKeys
            {
                PublishableKey = "pk_test_configured",
                SecretKey = "sk_test_configured",
            },
        };

        var context = CreateContext(new StripeSettingsViewModel
        {
            IsLive = true,
            TestPublishableKey = "pk_test_posted",
            TestPrivateSecret = "sk_test_posted",
            TestWebhookSecret = "whsec_posted",
        });

        var driver = CreateDriver(dataProtectionProvider, configuration, out _);

        // Act
        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.False(settings.IsLive);
        Assert.Equal("pk_test_stored", settings.TestPublishableKey);
        Assert.Equal(storedTestSecret, settings.TestPrivateSecret);
        Assert.Equal("whsec_posted", protector.Unprotect(settings.TestWebhookSecret));
    }

    [Fact]
    public async Task UpdateAsync_ConfiguredPublishableKey_IsNotClearedByTheDisabledFieldThatIsNotPosted()
    {
        // Arrange
        // A disabled field is not posted, so the bound model carries no publishable key at all.
        var settings = new StripeSettings
        {
            TestPublishableKey = "pk_test_stored",
        };

        var configuration = new StripeConfigurationOptions
        {
            Test = new StripeConfigurationKeys
            {
                PublishableKey = "pk_test_configured",
            },
        };

        var context = CreateContext(new StripeSettingsViewModel());
        var driver = CreateDriver(new EphemeralDataProtectionProvider(), configuration, out var optionsUpdateNotifier);

        // Act
        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.Equal("pk_test_stored", settings.TestPublishableKey);
        optionsUpdateNotifier.Verify(notifier => notifier.RequestUpdate<StripeOptions>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ConfiguredSecret_IsNotValidatedOrStoredFromThePost()
    {
        // Arrange
        // Even a malformed posted value for a configured secret is ignored rather than rejected.
        var settings = new StripeSettings();
        var configuration = new StripeConfigurationOptions
        {
            Live = new StripeConfigurationKeys
            {
                SecretKey = "sk_live_configured",
                WebhookSecret = "whsec_configured",
            },
        };

        var context = CreateContext(new StripeSettingsViewModel
        {
            LivePrivateSecret = "not-a-key",
            LiveWebhookSecret = "not-a-secret",
        });

        var driver = CreateDriver(new EphemeralDataProtectionProvider(), configuration, out _);

        // Act
        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Null(settings.LivePrivateSecret);
        Assert.Null(settings.LiveWebhookSecret);
    }

    [Fact]
    public async Task UpdateAsync_WithoutConfiguration_SavesThePostedValues()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings();
        var context = CreateContext(new StripeSettingsViewModel
        {
            IsLive = true,
            LivePublishableKey = "pk_live_posted",
            LivePrivateSecret = "sk_live_posted",
        });

        var driver = CreateDriver(dataProtectionProvider, new StripeConfigurationOptions(), out var optionsUpdateNotifier);

        // Act
        await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(settings.IsLive);
        Assert.Equal("pk_live_posted", settings.LivePublishableKey);
        Assert.Equal("sk_live_posted", protector.Unprotect(settings.LivePrivateSecret));
        optionsUpdateNotifier.Verify(notifier => notifier.RequestUpdate<StripeOptions>(It.IsAny<string>()), Times.Once);
    }

    private static StripeSettingsDisplayDriver CreateDriver(
        IDataProtectionProvider dataProtectionProvider,
        StripeConfigurationOptions configuration,
        out Mock<IOptionsUpdateNotifier> optionsUpdateNotifier)
    {
        optionsUpdateNotifier = new Mock<IOptionsUpdateNotifier>();

        return new StripeSettingsDisplayDriver(
            CreateAuthorizationService(),
            CreateHttpContextAccessor(),
            dataProtectionProvider,
            optionsUpdateNotifier.Object,
            Options.Create(configuration),
            CreateLocalizer());
    }

    private static UpdateEditorContext CreateContext(StripeSettingsViewModel posted)
        => new(
            new Shape(),
            groupId: StripeSettingsDisplayDriver.GroupId,
            false,
            htmlFieldPrefix: string.Empty,
            Mock.Of<IShapeFactory>(),
            layout: null,
            new PostedFormUpdateModel(posted));

    private static HttpContextAccessor CreateHttpContextAccessor()
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "test")),
            },
        };

    private static IAuthorizationService CreateAuthorizationService()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return authorizationService.Object;
    }

    private static IStringLocalizer<StripeSettingsDisplayDriver> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<StripeSettingsDisplayDriver>>();

        localizer
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));
        localizer
            .Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] _) => new LocalizedString(name, name));

        return localizer.Object;
    }
}
