using CrestApps.OrchardCore.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelActivity : CatalogItem
{
    /// <summary>
    /// The primary key in the database.
    /// </summary>
    public long Id { get; set; }

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
    /// When the interaction type is Automatic, we specify the preferred destination (Customer's Phone number or Email) to reach the Contact.
    /// </summary>
    public string PreferredDestination { get; set; }

    /// <summary>
    /// Used when the interaction type is Automatic to specify which AI Profile to use to handle the interaction.
    /// </summary>
    public string AIProfileName { get; set; }

    public string ContactContentItemId { get; set; }

    public string ContactContentType { get; set; }

    public string CampaignId { get; set; }

    public DateTime ScheduledUtc { get; set; }

    public string Instructions { get; set; }

    /// <summary>
    /// The attempt number of this activity. Default is 1 which indicate the very first attempt.
    /// </summary>
    public int Attempts { get; set; } = 1;

    public string AssignedToId { get; set; }

    public string AssignedToUsername { get; set; }

    public DateTime? AssignedToUtc { get; set; }

    public string CreatedById { get; set; }

    public string CreatedByUsername { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string CompletedById { get; set; }

    public string CompletedByUsername { get; set; }

    public string DispositionId { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string SubjectContentType { get; set; }

    public ContentItem Subject { get; set; }

    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    public ActivityStatus Status { get; set; }
}

public enum ActivityInteractionType
{
    Manual,
    Automated,
}

public enum ActivityStatus
{
    NotStated,
    AwaitingAgentResponse,
    AwaitingCustomerAnswer,
    Completed,
    Purged,
}

public enum ActivityUrgencyLevel
{
    Normal = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
}
