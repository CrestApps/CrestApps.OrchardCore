using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C16: a tenant entitlement used to name the tenant the buyer typed, even when provisioning failed because the
/// name was taken, so revoking it disabled whichever tenant had that name, possibly another customer's. The entitlement
/// now carries the provisioning job that created the tenant, and the applier acts only on a tenant that job created.
/// </summary>
public sealed class TenantEntitlementJobBindingTests
{
    [Fact]
    public async Task RevokeAsync_WhenTheJobFailed_LeavesTheTenantAlone()
    {
        var (applier, shellHost) = CreateApplier(new TenantProvisioningJob { ItemId = "job-1", TenantName = "Victim", Status = TenantProvisioningStatus.Failed, SubscriptionId = "sub-1" });

        await applier.RevokeAsync(Context("Victim", jobId: "job-1"));

        shellHost.Verify(host => host.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_WhenTheJobCreatedAnotherTenant_LeavesTheTenantAlone()
    {
        // The tenant named in the entitlement exists and runs, so only the job binding keeps it alone.
        var (applier, shellHost) = CreateApplier(new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", Status = TenantProvisioningStatus.Succeeded, SubscriptionId = "sub-1" }, existingTenant: "Victim");

        await applier.RevokeAsync(Context("Victim", jobId: "job-1"));

        shellHost.Verify(host => host.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_WhenTheJobCreatedTheTenant_DisablesIt()
    {
        var (applier, shellHost) = CreateApplier(new TenantProvisioningJob { ItemId = "job-1", TenantName = "Victim", Status = TenantProvisioningStatus.Succeeded, SubscriptionId = "sub-1" });

        await applier.RevokeAsync(Context("Victim", jobId: "job-1"));

        shellHost.Verify(host => host.UpdateShellSettingsAsync(It.Is<ShellSettings>(settings => settings.State == TenantState.Disabled)), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_ALegacyEntitlementWithoutAJob_ActsOnlyWhenThisSubscriptionsJobCreatedTheTenant()
    {
        var (applier, shellHost) = CreateApplier(new TenantProvisioningJob { ItemId = "job-9", TenantName = "Victim", Status = TenantProvisioningStatus.Succeeded, SubscriptionId = "someone-else" });

        await applier.RevokeAsync(Context("Victim", jobId: null));

        shellHost.Verify(host => host.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()), Times.Never);
    }

    private static SubscriptionEntitlementContext Context(string tenantName, string jobId)
    {
        var entitlement = new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Tenant, Value = tenantName };

        if (jobId is not null)
        {
            entitlement.Data = new Dictionary<string, string> { [SubscriptionConstants.EntitlementData.ProvisioningJobId] = jobId };
        }

        return new SubscriptionEntitlementContext(new Subscription { ItemId = "sub-1", Status = SubscriptionStatus.Canceled }, entitlement);
    }

    private static (TenantSubscriptionEntitlementApplier Applier, Mock<IShellHost> ShellHost) CreateApplier(TenantProvisioningJob job, string existingTenant = null)
    {
        existingTenant ??= job.TenantName;

        var running = new ShellSettings { Name = existingTenant };
        running.AsRunning();

        var shellHost = new Mock<IShellHost>();
        var found = running;
        shellHost.Setup(host => host.TryGetSettings(existingTenant, out found)).Returns(true);

        var store = new Mock<ITenantProvisioningJobStore>();
        store.Setup(s => s.FindByIdAsync(job.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        store.Setup(s => s.GetByTenantNameAsync(job.TenantName, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        var applier = new TenantSubscriptionEntitlementApplier(
            shellHost.Object,
            new ShellSettings { Name = ShellSettings.DefaultShellName },
            store.Object,
            NullLogger<TenantSubscriptionEntitlementApplier>.Instance);

        return (applier, shellHost);
    }
}
