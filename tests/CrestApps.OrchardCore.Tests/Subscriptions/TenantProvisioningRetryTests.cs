using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Review 11 C4: "Retry now" on the Site provisioning page built a site still waiting for its payment. A Pay Later buyer
/// got a running site before paying, because the job was neither running, succeeded nor short of its password.
/// </summary>
public sealed class TenantProvisioningRetryTests
{
    [Fact]
    public async Task Retry_AJobWaitingForPayment_LeavesItWaitingAndBuildsNothing()
    {
        var job = new TenantProvisioningJob
        {
            ItemId = "job-1",
            TenantName = "qasite1",
            ProtectedAdminPassword = "protected",
            Status = TenantProvisioningStatus.AwaitingPayment,
        };

        var jobs = new InMemoryTenantProvisioningJobStore(job);
        var provisioning = new Mock<ITenantProvisioningService>(MockBehavior.Strict);
        var notifier = new RecordingNotifier();
        var controller = CreateController(jobs, provisioning.Object, notifier);

        var result = await controller.Retry("job-1");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, (await jobs.FindByIdAsync("job-1", TestContext.Current.CancellationToken)).Status);
        provisioning.Verify(service => service.ProvisionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public void CanRetry_IsFalseForAJobWaitingForPayment()
    {
        var job = new TenantProvisioningJob { ProtectedAdminPassword = "protected", Status = TenantProvisioningStatus.AwaitingPayment };

        Assert.False(TenantProvisioningJobActions.CanRetry(job));
    }

    private static TenantProvisioningAdminController CreateController(InMemoryTenantProvisioningJobStore jobs, ITenantProvisioningService provisioning, INotifier notifier)
        => new(
            jobs,
            provisioning,
            TestSecurity.Granting(SubscriptionPermissions.ManageSubscriptions),
            notifier,
            new EchoHtmlLocalizer<TenantProvisioningAdminController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = TestSecurity.User("operator") },
            },
        };
}
