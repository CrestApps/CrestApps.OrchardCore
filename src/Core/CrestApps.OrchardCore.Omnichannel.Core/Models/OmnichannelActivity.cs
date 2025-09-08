using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelActivity : Entity
{
    public string Id { get; set; }

    /// <summary>
    /// 'SMS', 'Chat', 'Email', etc.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// When the channel is SMS and the owner is AI, we specify which phone number to use to outreach to the Contact.
    /// </summary>
    public string ChannelEndpoint { get; set; }

    /// <summary>
    /// When the owner is AI, we specify the preferred destination (Customer's Phone number or Email) to reach the Contact.
    /// </summary>
    public string PreferredDestination { get; set; }

    public string AIProfileId { get; set; }

    public string AIUserId { get; set; }

    public string ContactContentItemId { get; set; }

    public string ContactContentType { get; set; }

    public string CampaignId { get; set; }

    public DateTime ScheduledAt { get; set; }

    /// <summary>
    /// The attempt number of this activity. Default is 1 which indicate the very first attempt.
    /// </summary>
    public int Attempts { get; set; } = 1;

    public string AssignedToId { get; set; }

    public string AssignedToUsername { get; set; }

    public DateTime? AssignedToUtc { get; set; }

    public string Instructions { get; set; }

    public string CreatedById { get; set; }

    public string CreatedByUsername { get; set; }

    public string DispositionId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string SubjectId { get; set; }

    public UrgencyLevel UrgencyLevel { get; set; }

    public ActivityStatus Status { get; set; }
}

public enum ActivityStatus
{
    NotStated,
    AwaitingAgentResponse,
    AwaitingCustomerAnswer,
    Completed,
}

public enum UrgencyLevel
{
    Normal = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
}
