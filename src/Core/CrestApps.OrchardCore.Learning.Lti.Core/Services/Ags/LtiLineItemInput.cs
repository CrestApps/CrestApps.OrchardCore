namespace CrestApps.OrchardCore.Learning.Lti.Core.Services.Ags;

/// <summary>
/// A line item a tool posted or put (Assignment and Grade Services 2.0, the line item service), once read and checked.
/// </summary>
public sealed class LtiLineItemInput
{
    /// <summary>
    /// Gets or sets the maximum score, more than 0.
    /// </summary>
    public decimal ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the tool's resource id, if any.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the tool's tag, if any.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the resource link the line item belongs to, if any.
    /// </summary>
    public string ResourceLinkId { get; set; }

    /// <summary>
    /// Gets or sets when submissions start, in UTC, if given.
    /// </summary>
    public DateTime? StartDateTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets when submissions end, in UTC, if given.
    /// </summary>
    public DateTime? EndDateTimeUtc { get; set; }
}
