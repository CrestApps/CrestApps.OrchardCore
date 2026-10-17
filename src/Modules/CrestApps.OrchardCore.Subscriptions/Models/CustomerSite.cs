namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// A site a customer bought, as one row of their own sites (My Sites).
/// </summary>
/// <remarks>
/// The administration provisioning list shows the same jobs, but a customer is told where their site stands in
/// their own words and offered only to open it, so their rows are built from a type of their own.
/// </remarks>
/// <param name="Job">The provisioning job that builds the site.</param>
public sealed record CustomerSite(TenantProvisioningJob Job);
