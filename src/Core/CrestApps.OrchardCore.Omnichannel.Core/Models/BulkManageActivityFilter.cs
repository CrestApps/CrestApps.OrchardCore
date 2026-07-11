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
    /// Gets or sets the activity source to filter by.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the interaction type to filter by.
    /// </summary>
    public ActivityInteractionType? InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the activity status to filter by.
    /// </summary>
    public ActivityStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the assignment lifecycle status to filter by.
    /// </summary>
    public ActivityAssignmentStatus? AssignmentStatus { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier to filter by.
    /// </summary>
    public string CampaignId { get; set; }

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
    /// Gets or sets the maximum number of records to retrieve.
    /// When <see langword="null"/>, no limit is applied beyond normal pagination.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the phone number to search for in contact records.
    /// A leading plus sign searches E.164 values; otherwise, the national number is searched.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the match type for the phone number filter.
    /// </summary>
    public PhoneNumberMatchType PhoneNumberMatchType { get; set; } = PhoneNumberMatchType.Contains;

    /// <summary>
    /// Gets or sets the time zone identifiers to filter contacts by.
    /// </summary>
    public string[] TimeZoneIds { get; set; }

    /// <summary>
    /// Gets or sets the earliest Do Not Call date to filter by.
    /// Matches contacts whose Do Not Call date is on or after this value.
    /// </summary>
    public DateTime? DoNotCallFrom { get; set; }

    /// <summary>
    /// Gets or sets the latest Do Not Call date to filter by.
    /// Matches contacts whose Do Not Call date is on or before this value.
    /// </summary>
    public DateTime? DoNotCallTo { get; set; }

    /// <summary>
    /// Gets or sets the route values for preserving filter state during pagination.
    /// </summary>
    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
