using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C2: the new-site step is checked again just before payment, so a step saved before the rules existed (with a
/// reserved prefix, a recipe of the buyer's choosing, or no feature profile on the plan) can't be paid for.
/// </summary>
public sealed class TenantProvisioningPaymentGateTests
{
    [Fact]
    public async Task BeginningAsync_AStepSavedWithAReservedPrefix_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "admin", RecipeName = "Blog" }, planFeatureProfile: "sold");

        await CreateGate(allowCustomPrefix: true).BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task BeginningAsync_AStepSavedWithAnotherRecipe_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "SaaS" }, planFeatureProfile: "sold");

        await CreateGate().BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task BeginningAsync_APlanWithoutAFeatureProfile_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog", FeatureProfile = "sold" }, planFeatureProfile: null);

        await CreateGate().BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    /// <summary>
    /// SEC-C18: a plan whose profile was renamed or deleted under Tenants, Feature Profiles, or that arrived by recipe,
    /// import or API naming one that never existed, would sell a site Orchard treats as allowed every feature.
    /// </summary>
    [Fact]
    public async Task BeginningAsync_APlanWhoseFeatureProfileDoesNotExist_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "deleted");

        await CreateGate(knownProfiles: ["sold"]).BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task BeginningAsync_WithoutTheFeatureProfilesFeature_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "sold");

        await CreateGate(featureProfilesOn: false).BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task BeginningAsync_AValidStep_IsAllowed()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "sold");

        await CreateGate().BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.IsRefused, context.ErrorMessage);
    }

    private static TenantProvisioningPaymentGate CreateGate(bool allowCustomPrefix = false, string[] knownProfiles = null, bool featureProfilesOn = true)
    {
        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns([new ShellSettings { Name = ShellSettings.DefaultShellName }]);

        var catalog = new Mock<ITenantFeatureProfileCatalog>();
        catalog.Setup(value => value.GetProfileIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureProfilesOn ? knownProfiles ?? ["sold"] : null);

        // The real rules provider, so the gate is tested with the rules it is given in a running site.
        var rules = new TenantPurchaseRulesProvider(
            SiteServiceFactory.Create(new SubscriptionOnboardingSettings { AllowCustomPrefix = allowCustomPrefix }),
            shellHost.Object,
            new HttpContextAccessor(),
            Options.Create(new AdminOptions()),
            catalog.Object,
            Mock.Of<ICheckoutSessionStore>());

        var localizer = new Mock<IStringLocalizer<TenantProvisioningPaymentGate>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        return new TenantProvisioningPaymentGate(rules, shellHost.Object, Options.Create(new DocumentJsonSerializerOptions()), localizer.Object);
    }

    private static CheckoutPaymentBeginningContext CreateContext(TenantProvisioningStep saved, string planFeatureProfile)
    {
        var session = new CheckoutSession { SessionId = "session-1" };
        var step = new CheckoutFlowStep { Key = SubscriptionConstants.StepKey.TenantProvisioning, CollectData = true };
        step.Data["RecipeName"] = "Blog";
        step.Data["FeatureProfile"] = planFeatureProfile;
        session.Steps.Add(step);
        session.SavedSteps[SubscriptionConstants.StepKey.TenantProvisioning] = JsonSerializer.SerializeToNode(saved);

        return new CheckoutPaymentBeginningContext(new CheckoutFlow(session), "test");
    }
}
