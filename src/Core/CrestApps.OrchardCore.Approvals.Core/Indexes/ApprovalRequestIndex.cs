using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Approvals.Core.Indexes;

/// <summary>
/// Finds approval requests by status, subject, policy, requester and dates. It keeps no amount.
/// </summary>
public sealed class ApprovalRequestIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the subject's kind.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the subject's id.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the subject kind and access scope, so lists read only the requests a person may see.
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the policy's id.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the requester's user id.
    /// </summary>
    public string RequesterUserId { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it is due.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
