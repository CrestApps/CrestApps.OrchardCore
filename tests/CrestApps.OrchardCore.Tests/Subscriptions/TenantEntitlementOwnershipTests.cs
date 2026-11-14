using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Final security review (Lows), against Orchard Core's real <see cref="ShellHost"/>: an old subscription could disable
/// the site of whoever bought its name after the first site was removed, and a subscriber could switch back on a site an
/// operator had disabled, by pressing Cancel at period end, which keeps the agreement current.
/// </summary>
public sealed class TenantEntitlementOwnershipTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RevokeAsync_AfterTheNameWasSoldAgain_LeavesTheNewSiteRunning()
    {
        var host = await Host.CreateAsync(TenantState.Running,
            new TenantProvisioningJob { ItemId = "job-old", TenantName = "Shop1", SubscriptionId = "sub-old", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now.AddYears(-1) },
            new TenantProvisioningJob { ItemId = "job-new", TenantName = "Shop1", SubscriptionId = "sub-new", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.RevokeAsync(Context("sub-old", "job-old", tenantId: null));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Running, site.State);
    }

    [Fact]
    public async Task RevokeAsync_ForAnotherTenantId_LeavesTheSiteRunning()
    {
        var host = await Host.CreateAsync(TenantState.Running,
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", SubscriptionId = "sub-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.RevokeAsync(Context("sub-1", "job-1", tenantId: "a-removed-tenant"));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Running, site.State);
    }

    [Fact]
    public async Task ApplyAsync_ASiteAnOperatorDisabled_StaysDisabled()
    {
        var host = await Host.CreateAsync(TenantState.Disabled,
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", SubscriptionId = "sub-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.ApplyAsync(Context("sub-1", "job-1", tenantId: null));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Disabled, site.State);
    }

    [Fact]
    public async Task ApplyAsync_ASiteBillingDisabled_RunsAgain()
    {
        var host = await Host.CreateAsync(TenantState.Running,
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", SubscriptionId = "sub-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.RevokeAsync(Context("sub-1", "job-1", tenantId: null));
        Assert.True(host.Shells.TryGetSettings("Shop1", out var suspended));
        Assert.Equal(TenantState.Disabled, suspended.State);

        await host.Applier.ApplyAsync(Context("sub-1", "job-1", tenantId: null));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Running, site.State);
    }

    private static SubscriptionEntitlementContext Context(string subscriptionId, string jobId, string tenantId)
    {
        var data = new Dictionary<string, string> { [SubscriptionConstants.EntitlementData.ProvisioningJobId] = jobId };

        if (tenantId is not null)
        {
            data[SubscriptionConstants.EntitlementData.TenantId] = tenantId;
        }

        return new SubscriptionEntitlementContext(
            new Subscription { ItemId = subscriptionId, Status = SubscriptionStatus.Canceled },
            new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Tenant, Value = "Shop1", Data = data });
    }

    private sealed class Host
    {
        public ShellHost Shells { get; private init; }

        public TenantSubscriptionEntitlementApplier Applier { get; private init; }

        public static async Task<Host> CreateAsync(TenantState siteState, params TenantProvisioningJob[] jobs)
        {
            var settingsManager = new InMemoryShellSettingsManager();
            var shells = new ShellHost(settingsManager, Mock.Of<IShellContextFactory>(), new RunningShellTable(), Mock.Of<IExtensionManager>(), NullLogger<ShellHost>.Instance);

            var site = new ShellSettings { Name = "Shop1", RequestUrlHost = "shop1.example.net" };
            site.State = siteState;
            await shells.UpdateShellSettingsAsync(site);

            var applier = new TenantSubscriptionEntitlementApplier(
                shells,
                new ShellSettings { Name = ShellSettings.DefaultShellName },
                new InMemoryTenantProvisioningJobStore(jobs),
                NullLogger<TenantSubscriptionEntitlementApplier>.Instance);

            return new Host { Shells = shells, Applier = applier };
        }
    }
}
