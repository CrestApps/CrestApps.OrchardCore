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

    /// <summary>
    /// Selling-sites Mild (fix verification): the newest job for the name was looked up with the name as the entitlement
    /// spelled it, which SQLite and PostgreSQL compare exactly, while Orchard's shell host finds a tenant without case. An
    /// old subscription for "shop1" then disabled the site sold since as "Shop1".
    /// </summary>
    [Fact]
    public async Task RevokeAsync_AfterTheNameWasSoldAgainInAnotherCase_LeavesTheNewSiteRunning()
    {
        var host = await Host.CreateAsync(TenantState.Running,
            new TenantProvisioningJob { ItemId = "job-old", TenantName = "shop1", SubscriptionId = "sub-old", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now.AddYears(-1) },
            new TenantProvisioningJob { ItemId = "job-new", TenantName = "Shop1", SubscriptionId = "sub-new", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.RevokeAsync(Context("sub-old", "job-old", tenantId: null, tenantName: "shop1"));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Running, site.State);
    }

    /// <summary>
    /// The same, for an entitlement recorded before it carried its job.
    /// </summary>
    [Fact]
    public async Task RevokeAsync_ALegacyEntitlement_AfterTheNameWasSoldAgainInAnotherCase_LeavesTheNewSiteRunning()
    {
        var host = await Host.CreateAsync(TenantState.Running,
            new TenantProvisioningJob { ItemId = "job-old", TenantName = "shop1", SubscriptionId = "sub-old", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now.AddYears(-1) },
            new TenantProvisioningJob { ItemId = "job-new", TenantName = "Shop1", SubscriptionId = "sub-new", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.RevokeAsync(Context("sub-old", jobId: null, tenantId: null, tenantName: "shop1"));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Running, site.State);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): billing disabled the site and marked it; the operator switched it back on by
    /// hand, and the mark stayed. When the operator later disabled it for abuse, the buyer's cancel at period end found the
    /// old mark and switched the site on again. The mark is cleared whenever the applier finds the site running.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ASiteRunningWithAStaleBillingMark_ClearsIt_SoAnOperatorsLaterDisableHolds()
    {
        var host = await Host.CreateAsync(TenantState.Running, suspendedByBilling: true,
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", SubscriptionId = "sub-1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        await host.Applier.ApplyAsync(Context("sub-1", "job-1", tenantId: null));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var running));
        Assert.Null(running[SubscriptionConstants.SuspendedBySubscriptionSetting]);

        // The operator disables the site for abuse.
        running.State = TenantState.Disabled;
        await host.Shells.UpdateShellSettingsAsync(running);

        await host.Applier.ApplyAsync(Context("sub-1", "job-1", tenantId: null));

        Assert.True(host.Shells.TryGetSettings("Shop1", out var site));
        Assert.Equal(TenantState.Disabled, site.State);
    }

    private static SubscriptionEntitlementContext Context(string subscriptionId, string jobId, string tenantId, string tenantName = "Shop1")
    {
        var data = new Dictionary<string, string>();

        if (jobId is not null)
        {
            data[SubscriptionConstants.EntitlementData.ProvisioningJobId] = jobId;
        }

        if (tenantId is not null)
        {
            data[SubscriptionConstants.EntitlementData.TenantId] = tenantId;
        }

        return new SubscriptionEntitlementContext(
            new Subscription { ItemId = subscriptionId, Status = SubscriptionStatus.Canceled },
            new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Tenant, Value = tenantName, Data = data });
    }

    private sealed class Host
    {
        public ShellHost Shells { get; private init; }

        public TenantSubscriptionEntitlementApplier Applier { get; private init; }

        public static Task<Host> CreateAsync(TenantState siteState, params TenantProvisioningJob[] jobs)
            => CreateAsync(siteState, suspendedByBilling: false, jobs);

        public static async Task<Host> CreateAsync(TenantState siteState, bool suspendedByBilling, params TenantProvisioningJob[] jobs)
        {
            var settingsManager = new InMemoryShellSettingsManager();
            var shells = new ShellHost(settingsManager, Mock.Of<IShellContextFactory>(), new RunningShellTable(), Mock.Of<IExtensionManager>(), NullLogger<ShellHost>.Instance);

            var site = new ShellSettings { Name = "Shop1", RequestUrlHost = "shop1.example.net" };
            site.State = siteState;

            if (suspendedByBilling)
            {
                site[SubscriptionConstants.SuspendedBySubscriptionSetting] = "true";
            }
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
