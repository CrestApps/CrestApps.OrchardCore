using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// One row per grant of an enrollment, so a refunded order line or an expired seat finds its enrollments.
/// </summary>
public sealed class EnrollmentGrantIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the source of the grant.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the record in its source.
    /// </summary>
    public string SourceId { get; set; }
}
