using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Selling-sites Mild (fix verification): the rules provider tells the validator which sites the buyer owns, from the
/// provisioning jobs, so the names around the buyer's own hosts are theirs to take. A name sold again after its first site
/// was removed belongs to its newest buyer.
/// </summary>
public sealed class TenantPurchaseOwnedSitesTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ShellSettings[] _tenants =
    [
        new ShellSettings { Name = ShellSettings.DefaultShellName },
        new ShellSettings { Name = "Acme", RequestUrlHost = "acme.example.org" },
    ];

    [Fact]
    public async Task ValidateAsync_ANameBesideTheBuyersOwnSite_IsAllowed()
    {
        var provider = CreateProvider(new TenantProvisioningJob { ItemId = "job-1", TenantName = "Acme", OwnerId = "user-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        var errors = await provider.ValidateAsync(Request("user-1"), Rules(), _tenants, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateAsync_ANameBesideAnotherBuyersSite_IsRefused()
    {
        var provider = CreateProvider(new TenantProvisioningJob { ItemId = "job-1", TenantName = "Acme", OwnerId = "user-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        var errors = await provider.ValidateAsync(Request("user-2"), Rules(), _tenants, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    [Fact]
    public async Task ValidateAsync_TheFormerOwnerOfANameSoldAgain_DoesNotOwnTheNewSite()
    {
        var provider = CreateProvider(
            new TenantProvisioningJob { ItemId = "job-old", TenantName = "Acme", OwnerId = "user-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now.AddYears(-1) },
            new TenantProvisioningJob { ItemId = "job-new", TenantName = "Acme", OwnerId = "user-2", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        var errors = await provider.ValidateAsync(Request("user-1"), Rules(), _tenants, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    private static TenantPurchaseRequest Request(string ownerId)
        => new()
        {
            TenantName = "Shop1",
            Domains = ["shop.acme.example.org"],
            RecipeName = "Blog",
            FeatureProfile = "sold-tenant",
            OwnerId = ownerId,
        };

    private static TenantPurchaseRules Rules()
        => new()
        {
            PlanRecipeName = "Blog",
            AllowCustomDomains = true,
            ReservedPrefixes = SubscriptionOnboardingSettings.DefaultReservedPrefixes,
            AdminPrefix = "Admin",
            KnownFeatureProfiles = ["sold-tenant"],
        };

    private static TenantPurchaseRulesProvider CreateProvider(params TenantProvisioningJob[] jobs)
        => new(
            SiteServiceFactory.Create(new SubscriptionOnboardingSettings { AllowCustomDomains = true }),
            Mock.Of<IShellHost>(),
            new HttpContextAccessor(),
            Options.Create(new AdminOptions()),
            Mock.Of<ITenantFeatureProfileCatalog>(),
            Mock.Of<ICheckoutSessionStore>(),
            StoreRoutes.Create(),
            new InMemoryTenantProvisioningJobStore(jobs));
}
