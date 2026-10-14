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
    /// to do, and a job that has lost its administrator password can never complete.
    /// </summary>
    /// <param name="job">The job.</param>
    public static bool CanRetry(TenantProvisioningJob job)
        => job is not null &&
            job.Status is not (TenantProvisioningStatus.Succeeded or TenantProvisioningStatus.Running) &&
            !string.IsNullOrEmpty(job.ProtectedAdminPassword);

    /// <summary>
    /// Whether the job can be closed as created by hand. Only an abandoned job, which the site has stopped
    /// trying, is left for a person to finish.
    /// </summary>
    /// <param name="job">The job.</param>
    public static bool CanMarkCreated(TenantProvisioningJob job)
        => job is not null && job.Status == TenantProvisioningStatus.Abandoned;
}
