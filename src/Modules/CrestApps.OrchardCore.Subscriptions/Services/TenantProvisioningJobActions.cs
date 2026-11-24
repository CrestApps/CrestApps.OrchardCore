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

    /// <summary>
    /// Makes a job due again at once. Retrying means an operator believes the cause is fixed, so the attempt counter
    /// and the back-off are reset; without that reset the job would be abandoned again on the next attempt. Call it
    /// only when <see cref="CanRetry"/> is <see langword="true"/>, then save the job.
    /// </summary>
    /// <param name="job">The job.</param>
    public static void PrepareRetry(TenantProvisioningJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.Status = TenantProvisioningStatus.Pending;
        job.AttemptCount = 0;
        job.NextAttemptUtc = null;
    }

    /// <summary>
    /// Closes an abandoned job whose site an operator created by hand, so the customer sees it as ready. Call it only
    /// when <see cref="CanMarkCreated"/> is <see langword="true"/>, then save the job.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="siteUrl">The address of the site, shown to the customer, or <see langword="null"/> to keep the stored one.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public static void MarkCreated(TenantProvisioningJob job, string siteUrl, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.Status = TenantProvisioningStatus.Succeeded;
        job.CompletedUtc = utcNow;
        job.UpdatedUtc = utcNow;
        job.NextAttemptUtc = null;
        job.LastError = null;

        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            job.SiteUrl = siteUrl.Trim();
        }
    }
}
