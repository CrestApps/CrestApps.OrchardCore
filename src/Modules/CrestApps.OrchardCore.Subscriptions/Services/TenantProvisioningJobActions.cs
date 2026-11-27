using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// What an operator can do with a site provisioning job. The screen and the actions share these rules, so a
/// button is only offered when pressing it would work.
/// </summary>
public static class TenantProvisioningJobActions
{
    /// <summary>
    /// Whether the job can be tried again. A running job is already being tried, a finished one has nothing left
    /// to do, one waiting for its payment is built once the payment arrives, and a job that has lost its administrator
    /// password can never complete.
    /// </summary>
    /// <param name="job">The job.</param>
    public static bool CanRetry(TenantProvisioningJob job)
        => job is not null &&
            job.Status is not (TenantProvisioningStatus.Succeeded or TenantProvisioningStatus.Running or TenantProvisioningStatus.AwaitingPayment) &&
            !string.IsNullOrEmpty(job.ProtectedAdminPassword);

    /// <summary>
    /// Whether the job can be closed as created by hand. Only an abandoned job, which the site has stopped
    /// trying, is left for a person to finish.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <summary>
    /// Whether staff can ask for a job that waits for its payment to be checked again, such as after the payment was
    /// recorded somewhere the site's checkout doesn't see.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns><see langword="true"/> when the job waits for its payment and can still be created.</returns>
    public static bool CanReleasePaid(TenantProvisioningJob job)
        => job is not null &&
            job.Status == TenantProvisioningStatus.AwaitingPayment &&
            !string.IsNullOrEmpty(job.ProtectedAdminPassword);

    public static bool CanMarkCreated(TenantProvisioningJob job)
        => job is not null && job.Status == TenantProvisioningStatus.Abandoned;
}
