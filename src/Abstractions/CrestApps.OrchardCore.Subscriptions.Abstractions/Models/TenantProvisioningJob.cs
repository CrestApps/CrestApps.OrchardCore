using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A durable record of a tenant that has been paid for and must be created.
/// </summary>
/// <remarks>
/// Provisioning a tenant is slow, involves a database, and can fail for reasons that have nothing to do with
/// the customer: a name that was taken a second earlier, a recipe that errors, a database that is briefly
/// unreachable, a process that restarts mid-setup. Doing it inline in the request that completes the
/// checkout means any of those leaves a customer who has paid, has no site, and has nothing to retry.
///
/// Writing the intent down first turns that into a job the site can see, retry, and report on. The
/// checkout's part is only to record what was bought; a sweep does the work and keeps trying until it
/// either succeeds or has failed enough times that a human should look at it.
/// </remarks>
public sealed class TenantProvisioningJob : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the tenant was bought through.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the subscription the tenant belongs to, when the purchase created one. It is what ties
    /// the site's continued existence to the customer continuing to pay.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who bought the tenant.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the email address to reach the buyer at, which for a guest is the only way.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the Orchard Core tenant name.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the display title of the new site.
    /// </summary>
    public string TenantTitle { get; set; }

    /// <summary>
    /// Gets or sets the administrator user name created during setup.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the administrator email address created during setup.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the <em>data-protected</em> administrator password.
    /// </summary>
    /// <remarks>
    /// The name carries the protection state deliberately. Handing this value to the setup service unchanged
    /// creates a site whose administrator can never sign in, which cannot be recovered without a password
    /// reset. It is cleared once provisioning reaches a terminal outcome, so a secret is not retained for the
    /// life of the record.
    /// </remarks>
    public string ProtectedAdminPassword { get; set; }

    /// <summary>
    /// Gets or sets the URL prefix assigned to the tenant.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the custom domains assigned to the tenant.
    /// </summary>
    public string[] Domains { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe used to initialize the site.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the feature profile applied to the tenant.
    /// </summary>
    public string FeatureProfile { get; set; }

    /// <summary>
    /// Gets or sets the current state of the job.
    /// </summary>
    public TenantProvisioningStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how many times provisioning has been attempted.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the job may next be attempted. It backs off after each failure so a
    /// persistent fault does not spin.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason the last attempt failed.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the URL the customer reaches their new site at, once it exists.
    /// </summary>
    public string SiteUrl { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the job was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the job was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the site was created.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the job has reached an outcome it will not move from on its own.
    /// </summary>
    public bool IsTerminal
        => Status is TenantProvisioningStatus.Succeeded or TenantProvisioningStatus.Abandoned;
}

/// <summary>
/// The state of a <see cref="TenantProvisioningJob"/>.
/// </summary>
public enum TenantProvisioningStatus
{
    /// <summary>
    /// The tenant has been paid for and is waiting to be created.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// A node is creating the tenant right now.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The site exists and the customer can sign in.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The last attempt failed and another is scheduled.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Provisioning failed too many times to keep retrying. The customer has paid, so this is a state a
    /// person has to resolve rather than one the site can clear on its own.
    /// </summary>
    Abandoned = 4,
}
