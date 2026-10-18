using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The fields of a <c>DialSuppressed</c> event the dialer's activity is counted from.
/// </summary>
/// <remarks>
/// Like a routing decision, a suppression event may stand for several identical suppressions folded into one, carrying
/// how many as <see cref="Count"/>; without it, it stands for one.
/// </remarks>
internal sealed class DialerSuppressionPayload
{
    /// <summary>
    /// Gets or sets the dialer profile the suppressed attempt was made under.
    /// </summary>
    public string ProfileItemId { get; set; }

    /// <summary>
    /// Gets or sets the record (CRM activity) whose attempt was suppressed.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets why the compliance gate suppressed the attempt.
    /// </summary>
    public DialerSuppressionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets how many identical suppressions the event stands for, when it stands for more than one.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets how many suppressions the event stands for: its <see cref="Count"/> when it carries one, otherwise one.
    /// </summary>
    public int Occurrences => Count is > 0 ? Count.Value : 1;
}
