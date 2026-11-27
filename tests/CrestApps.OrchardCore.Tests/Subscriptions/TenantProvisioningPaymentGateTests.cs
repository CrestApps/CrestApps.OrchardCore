using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
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

    /// <summary>
    /// H2 (final security review): a site named after one of the store's own first URL segments took every URL under it,
    /// such as the customer account pages under <c>/account</c>.
    /// </summary>
    [Fact]
    public async Task BeginningAsync_APrefixTheStoreServesItself_IsRefused()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "sold");

        await CreateGate(storeRoutes: ["Shop1/details", "cart"]).BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    /// <summary>
    /// M10 (final security review): a site on a prefix of the store's own address runs in the store's origin, so it is
    /// sold only when the operator chose that.
    /// </summary>
    [Fact]
    public async Task BeginningAsync_APrefixSiteOnTheStoresAddress_IsRefusedUnlessTheOperatorChoseIt()
    {
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "sold");

        await CreateGate(allowPrefixOnStoreHost: false).BeginningAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.IsRefused);
    }

    /// <summary>
    /// Round 3 (Medium): a site waiting for its payment wasn't a tenant yet, so its name and prefix were free to another
    /// buyer during the payment terms, and the first buyer's paid job was then abandoned. They are held until it is built
    /// or given up.
    /// </summary>
    [Theory]
    [InlineData(TenantProvisioningStatus.AwaitingPayment, true)]
    [InlineData(TenantProvisioningStatus.Pending, true)]
    [InlineData(TenantProvisioningStatus.Failed, true)]
    [InlineData(TenantProvisioningStatus.Abandoned, false)]
    public async Task BeginningAsync_ASiteNameBoughtEarlierAndNotBuilt_IsHeld(TenantProvisioningStatus status, bool refused)
    {
        // Arrange
        var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-earlier",
            TenantName = "Shop1",
            Prefix = "shop1",
            OwnerId = "first-buyer",
            Status = status,
        });
        var context = CreateContext(new TenantProvisioningStep { TenantName = "Shop1", Prefix = "shop1", RecipeName = "Blog" }, planFeatureProfile: "sold");

        // Act
        await CreateGate(jobs: jobs).BeginningAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(refused, context.IsRefused);
    }

    [Fact]
    public async Task BeginningAsync_AHostOfASiteWaitingForPayment_IsHeld()
    {
        // Arrange
        var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-earlier",
            TenantName = "Other",
            Domains = ["shop.example.org"],
            Status = TenantProvisioningStatus.AwaitingPayment,
        });

        // Act
        var errors = await CreateRules(jobs, allowCustomDomains: true).ValidateAsync(
            new TenantPurchaseRequest { TenantName = "Shop1", Domains = ["shop.example.org"], RecipeName = "Blog", FeatureProfile = "sold" },
            new TenantPurchaseRules { PlanRecipeName = "Blog", AllowCustomDomains = true, KnownFeatureProfiles = ["sold"] },
            [new ShellSettings { Name = ShellSettings.DefaultShellName }],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    private static TenantProvisioningPaymentGate CreateGate(
        bool allowCustomPrefix = false,
        string[] knownProfiles = null,
        bool featureProfilesOn = true,
        bool allowPrefixOnStoreHost = true,
        string[] storeRoutes = null,
        InMemoryTenantProvisioningJobStore jobs = null)
    {
        var shellHost = CreateShellHost();

        // The real rules provider, so the gate is tested with the rules it is given in a running site.
        var rules = CreateRules(jobs, allowCustomPrefix, knownProfiles, featureProfilesOn, allowPrefixOnStoreHost, storeRoutes, shellHost: shellHost);

        var localizer = new Mock<IStringLocalizer<TenantProvisioningPaymentGate>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        return new TenantProvisioningPaymentGate(rules, shellHost.Object, Options.Create(new DocumentJsonSerializerOptions()), localizer.Object);
    }

    private static TenantPurchaseRulesProvider CreateRules(
        InMemoryTenantProvisioningJobStore jobs,
        bool allowCustomPrefix = false,
        string[] knownProfiles = null,
        bool featureProfilesOn = true,
        bool allowPrefixOnStoreHost = true,
        string[] storeRoutes = null,
        bool allowCustomDomains = false,
        Mock<IShellHost> shellHost = null)
    {
        var catalog = new Mock<ITenantFeatureProfileCatalog>();
        catalog.Setup(value => value.GetProfileIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureProfilesOn ? knownProfiles ?? ["sold"] : null);

        return new TenantPurchaseRulesProvider(
            SiteServiceFactory.Create(new SubscriptionOnboardingSettings { AllowCustomPrefix = allowCustomPrefix, AllowPrefixOnStoreHost = allowPrefixOnStoreHost, AllowCustomDomains = allowCustomDomains }),
            (shellHost ?? CreateShellHost()).Object,
            new HttpContextAccessor(),
            Options.Create(new AdminOptions()),
            catalog.Object,
            Mock.Of<ICheckoutSessionStore>(),
            StoreRoutes.Create(storeRoutes ?? ["account/addresses", "cart"]),
            jobs ?? new InMemoryTenantProvisioningJobStore());
    }

    private static Mock<IShellHost> CreateShellHost()
    {
        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns([new ShellSettings { Name = ShellSettings.DefaultShellName }]);

        return shellHost;
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
