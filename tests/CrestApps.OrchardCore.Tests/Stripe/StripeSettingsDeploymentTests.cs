using System.IO;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Deployments;
using CrestApps.OrchardCore.Stripe.Deployments.Sources;
using CrestApps.OrchardCore.Stripe.Deployments.Steps;
using CrestApps.OrchardCore.Stripe.Drivers;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.Recipes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// The Stripe settings travel between environments through a step of their own. The export never carries a secret or
/// this site's connection to Stripe, and the import protects the clear-text secrets a hand-written recipe supplies.
/// </summary>
public sealed class StripeSettingsDeploymentTests
{
    [Fact]
    public async Task Export_LeavesOutEverySecretAndTheConnection()
    {
        // Arrange
        var protector = new EphemeralDataProtectionProvider().CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings
        {
            IsLive = true,
            LivePublishableKey = "pk_live_exported",
            LivePrivateSecret = protector.Protect("sk_live_secret"),
            LiveWebhookSecret = protector.Protect("whsec_live_secret"),
            LiveAccountId = "acct_live",
            LiveWebhookId = "we_live",
            TestPublishableKey = "pk_test_exported",
            TestPrivateSecret = protector.Protect("sk_test_secret"),
            TestWebhookSecret = protector.Protect("whsec_test_secret"),
            TestAccountId = "acct_test",
            TestWebhookId = "we_test",
        };

        var source = new StripeSettingsDeploymentSource(CreateSiteService(settings, out _));
        var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new StripeSettingsDeploymentStep(), result);

        // Assert
        var step = Assert.IsType<JsonObject>(Assert.Single(result.Steps));
        Assert.Equal(StripeDeploymentSteps.Settings, step["name"]?.GetValue<string>());

        var exported = Assert.IsType<JsonObject>(step["Settings"]);
        Assert.True(exported[nameof(StripeSettings.IsLive)]?.GetValue<bool>());
        Assert.Equal("pk_live_exported", exported[nameof(StripeSettings.LivePublishableKey)]?.GetValue<string>());
        Assert.Equal("pk_test_exported", exported[nameof(StripeSettings.TestPublishableKey)]?.GetValue<string>());

        foreach (var member in StripeDeploymentSteps.ProtectedMembers.Keys.Concat(StripeDeploymentSteps.EnvironmentOwnedMembers))
        {
            Assert.False(exported.ContainsKey(member), $"The export must not carry '{member}'.");
        }

        var json = step.ToJsonString();

        Assert.DoesNotContain(settings.LivePrivateSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(settings.TestPrivateSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(settings.LiveWebhookSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(settings.TestWebhookSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("sk_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("whsec_", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_ProtectsClearTextSecrets()
    {
        // Arrange
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var settings = new StripeSettings();
        var siteService = CreateSiteService(settings, out var siteServiceMock);
        var step = CreateStep(siteService, dataProtectionProvider, out var optionsUpdateNotifier);
        var context = CreateContext(new JsonObject
        {
            [nameof(StripeSettings.IsLive)] = false,
            [nameof(StripeSettings.TestPublishableKey)] = "pk_test_imported",
            [nameof(StripeSettings.TestPrivateSecret)] = "sk_test_imported",
            [nameof(StripeSettings.TestWebhookSecret)] = "whsec_imported",
        });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal("pk_test_imported", settings.TestPublishableKey);
        Assert.NotEqual("sk_test_imported", settings.TestPrivateSecret);
        Assert.NotEqual("whsec_imported", settings.TestWebhookSecret);
        Assert.Equal("sk_test_imported", protector.Unprotect(settings.TestPrivateSecret));
        Assert.Equal("whsec_imported", protector.Unprotect(settings.TestWebhookSecret));
        siteServiceMock.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Once);
        optionsUpdateNotifier.Verify(notifier => notifier.RequestUpdate<StripeOptions>(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Import_WithoutASecret_KeepsTheStoredOne_AndIgnoresTheConnection()
    {
        // Arrange
        // Replaying an exported plan carries no secret and must not wipe the destination's own credentials or adopt
        // the source site's account and webhook.
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(StripeSettingsDisplayDriver.ProtectionPurpose);
        var storedSecret = protector.Protect("sk_test_destination");
        var settings = new StripeSettings
        {
            TestPrivateSecret = storedSecret,
            TestAccountId = "acct_destination",
            TestWebhookId = "we_destination",
        };

        var step = CreateStep(CreateSiteService(settings, out _), dataProtectionProvider, out _);
        var context = CreateContext(new JsonObject
        {
            [nameof(StripeSettings.TestPublishableKey)] = "pk_test_imported",
            [nameof(StripeSettings.TestPrivateSecret)] = null,
            [nameof(StripeSettings.TestAccountId)] = "acct_source",
            [nameof(StripeSettings.TestWebhookId)] = "we_source",
        });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal("pk_test_imported", settings.TestPublishableKey);
        Assert.Equal(storedSecret, settings.TestPrivateSecret);
        Assert.Equal("acct_destination", settings.TestAccountId);
        Assert.Equal("we_destination", settings.TestWebhookId);
    }

    [Theory]
    [InlineData(nameof(StripeSettings.TestPrivateSecret), "sk_live_wrong_mode")]
    [InlineData(nameof(StripeSettings.LivePrivateSecret), "CfDJ8-an-already-protected-value")]
    [InlineData(nameof(StripeSettings.TestWebhookSecret), "not-a-signing-secret")]
    [InlineData(nameof(StripeSettings.LivePublishableKey), "pk_test_wrong_mode")]
    public async Task Import_WithAValueOfTheWrongShape_FailsWithoutStoringAnything(string member, string value)
    {
        // Arrange
        var settings = new StripeSettings();
        var siteService = CreateSiteService(settings, out var siteServiceMock);
        var step = CreateStep(siteService, new EphemeralDataProtectionProvider(), out var optionsUpdateNotifier);
        var context = CreateContext(new JsonObject
        {
            [nameof(StripeSettings.TestPublishableKey)] = "pk_test_imported",
            [member] = value,
        });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);
        Assert.Null(settings.TestPublishableKey);
        siteServiceMock.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
        optionsUpdateNotifier.Verify(notifier => notifier.RequestUpdate<StripeOptions>(It.IsAny<string>()), Times.Never);
    }

    private static StripeSettingsStep CreateStep(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        out Mock<IOptionsUpdateNotifier> optionsUpdateNotifier)
    {
        optionsUpdateNotifier = new Mock<IOptionsUpdateNotifier>();

        // The step is internal, so its localizer can't be mocked: Moq can't proxy an interface over an internal type from a
        // strong-named assembly.
        return new StripeSettingsStep(siteService, dataProtectionProvider, optionsUpdateNotifier.Object, new PassThroughStringLocalizer<StripeSettingsStep>());
    }

    private static RecipeExecutionContext CreateContext(JsonObject settings)
        => new()
        {
            Name = StripeDeploymentSteps.Settings,
            Step = new JsonObject
            {
                ["name"] = StripeDeploymentSteps.Settings,
                ["Settings"] = settings,
            },
        };

    private static ISiteService CreateSiteService(StripeSettings settings, out Mock<ISiteService> siteService)
    {
        var site = new Mock<ISite>();
        site.Setup(site => site.GetOrCreate<StripeSettings>())
            .Returns(settings);
        site.SetupGet(site => site.Properties)
            .Returns(new JsonObject());

        siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);
        siteService.Setup(service => service.LoadSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private sealed class NullFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
        {
            return Task.CompletedTask;
        }
    }
}
