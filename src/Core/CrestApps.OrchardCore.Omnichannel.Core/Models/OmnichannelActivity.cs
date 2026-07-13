using CrestApps.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel activity.
/// </summary>
public sealed class OmnichannelActivity : CatalogItem
{
    /// <summary>
    /// The primary key in the database.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the kind of work the activity represents.
    /// </summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Task;

    /// <summary>
    /// Gets or sets the source that created or is currently driving the activity.
    /// Workflow processing must not depend on this value.
    /// </summary>
    public string Source { get; set; } = ActivitySources.Manual;

    /// <summary>
    /// 'SMS', 'Chat', 'Email', etc.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// When the channel is SMS and the interaction type is Automatic, we specify which phone number to use to outreach to the Contact.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// The type of interaction.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// When the interaction is automated, we store the AI session Id.
    /// </summary>
    public string AISessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier used to drive this automated activity.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment name used by this automated phone activity.
    /// When empty, execution falls back to the subject flow and then the site default.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment name used by this automated phone activity.
    /// When empty, execution falls back to the subject flow and then the site default.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice identifier used by this automated phone activity.
    /// When empty, execution falls back to the subject flow and then the site default.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// When the interaction type is Automatic, we specify the preferred destination (Customer's Phone number or Email) to reach the Contact.
    /// </summary>
    public string PreferredDestination { get; set; }

    /// <summary>
    /// Gets or sets the contact content item id.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign id.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the scheduled utc.
    /// </summary>
    public DateTime ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// The attempt number of this activity. Default is 1 which indicate the very first attempt.
    /// </summary>
    public int Attempts { get; set; } = 1;

    /// <summary>
    /// Gets or sets the assigned to id.
    /// </summary>
    public string AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the assigned to username.
    /// </summary>
    public string AssignedToUsername { get; set; }

    /// <summary>
    /// Gets or sets the assigned to utc.
    /// </summary>
    public DateTime? AssignedToUtc { get; set; }

    /// <summary>
    /// Gets or sets the assignment lifecycle status used by queues, dialers, and reservations.
    /// </summary>
    public ActivityAssignmentStatus AssignmentStatus { get; set; }

    /// <summary>
    /// Gets or sets the active reservation identifier when a queue or dialer has reserved the activity.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the user or system actor that reserved the activity.
    /// </summary>
    public string ReservedById { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user or system actor that reserved the activity.
    /// </summary>
    public string ReservedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was reserved.
    /// </summary>
    public DateTime? ReservedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity reservation expires.
    /// </summary>
    public DateTime? ReservationExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the created by id.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the created by username.
    /// </summary>
    public string CreatedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the completed utc.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the completed by id.
    /// </summary>
    public string CompletedById { get; set; }

    /// <summary>
    /// Gets or sets the completed by username.
    /// </summary>
    public string CompletedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was purged.
    /// </summary>
    public DateTime? PurgedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who purged the activity.
    /// </summary>
    public string PurgedById { get; set; }

    /// <summary>
    /// Gets or sets the username of the user who purged the activity.
    /// </summary>
    public string PurgedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the disposition id.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public ContentItem Subject { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public ActivityStatus Status { get; set; }
}

/// <summary>
/// Specifies the activity interaction type options.
/// </summary>
public enum ActivityInteractionType
{
    Manual,
    Automated,
}

/// <summary>
/// Specifies the activity status options.
/// </summary>
public enum ActivityStatus
{
    NotStated,
    AwaitingAgentResponse,
    AwaitingCustomerAnswer,
    Completed,
    Pending,
    Scheduled,
    Reserved,
    Dialing,
    InProgress,
    Failed,
    Cancelled,
    Purged,
}

/// <summary>
/// Specifies the activity urgency level options.
/// </summary>
public enum ActivityUrgencyLevel
{
    Normal = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
}
