namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// One reason a learner has access to a course: a manual enrollment, an order line, a subscription, a licence seat. An
/// enrollment keeps its access while any grant remains, as <c>IRoleGrantService</c> keeps a role.
/// </summary>
public sealed class EnrollmentGrant
{
    /// <summary>
    /// Gets or sets the enrollment source, such as <c>Manual</c>, <c>Self</c> or <c>Order</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the record that grants access in its source, such as the order line id. Together with
    /// <see cref="Source"/> it makes a grant idempotent.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the grant was given, in UTC.
    /// </summary>
    public DateTime GrantedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the access this grant gives ends, in UTC, or <see langword="null"/> for no end.
    /// </summary>
    public DateTime? AccessEndsUtc { get; set; }
}
