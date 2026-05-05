using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the filter criteria for bulk-managing omnichannel activities.
/// </summary>
public sealed class BulkManageActivityFilter : Entity
{
    /// <summary>
    /// Gets or sets whether to filter by published contacts.
    /// When <see langword="true"/>, only activities for published contacts are returned.
    /// When <see langword="false"/>, only activities for unpublished contacts are returned.
    /// When <see langword="null"/>, no contact publication filter is applied.
    /// </summary>
    public bool? ContactIsPublished { get; set; }

    /// <summary>
    /// Gets or sets the attempt number filter expression.
    /// Supports exact values (e.g., "3") and range expressions (e.g., "3+", "3-").
    /// </summary>
    public string AttemptFilter { get; set; }

    /// <summary>
    /// Gets or sets the subject content type to filter by.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the channel to filter by.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the user IDs to filter activities assigned to.
    /// </summary>
    public string[] AssignedToUserIds { get; set; }

    /// <summary>
    /// Gets or sets the earliest scheduled date to filter by.
    /// </summary>
    public DateTime? ScheduledFrom { get; set; }

    /// <summary>
    /// Gets or sets the latest scheduled date to filter by.
    /// </summary>
    public DateTime? ScheduledTo { get; set; }

    /// <summary>
    /// Gets or sets the earliest created date to filter by.
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the latest created date to filter by.
    /// </summary>
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// Gets or sets the urgency level to filter by.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the route values for preserving filter state during pagination.
    /// </summary>
    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
