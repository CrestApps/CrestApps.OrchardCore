using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// What a page of approval requests shows.
/// </summary>
public sealed class ApprovalRequestQuery
{
    /// <summary>
    /// Gets or sets the status, or <see langword="null"/> for every status.
    /// </summary>
    public ApprovalStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the policy, or <see langword="null"/>.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the approval type (the requests' <c>SubjectKind</c>), or <see langword="null"/> for every type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the requester's user id, for the requests a person made, or <see langword="null"/>.
    /// </summary>
    public string RequesterUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the oldest come first; the newest do by default.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Gets or sets the requests the reader may see.
    /// </summary>
    public SourceAccessFilter Access { get; set; } = SourceAccessFilter.Unrestricted;
}
