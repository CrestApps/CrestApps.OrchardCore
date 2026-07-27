namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Configures the Contact Center preview maintenance tooling. Reset is destructive, so it is disabled by
/// default and guarded against the Production environment: an operator must opt in deliberately.
/// </summary>
public sealed class ContactCenterPreviewMaintenanceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether resetting Contact Center data is permitted on this tenant.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool AllowReset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reset is refused when the host runs in the Production
    /// environment, regardless of <see cref="AllowReset"/>. Defaults to <see langword="true"/>.
    /// </summary>
    public bool RefuseResetInProduction { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of seconds to wait for in-flight Contact Center work to drain when quiescing.
    /// Defaults to 30 seconds.
    /// </summary>
    public int DrainTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of documents read per page while exporting or deleting a data set. Defaults
    /// to 200.
    /// </summary>
    public int PageSize { get; set; } = 200;
}
