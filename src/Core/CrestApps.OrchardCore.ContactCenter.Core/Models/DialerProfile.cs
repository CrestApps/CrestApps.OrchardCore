using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an outbound dialing configuration that ties a campaign and queue to a dialing mode and provider.
/// </summary>
public sealed class DialerProfile : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the dialer profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the dialer profile.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the CRM campaign whose activities are dialed.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the queue eligible agents sign in to for the campaign.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the dialing mode that controls pacing and agent reservation behavior.
    /// </summary>
    public DialerMode Mode { get; set; } = DialerMode.Preview;

    /// <summary>
    /// Gets or sets the technical name of the dialer provider that places calls, or null for the default.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the number of calls placed per available agent for power dialing.
    /// </summary>
    public int CallsPerAgent { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of dialing attempts allowed per activity.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay, in minutes, before a no-answer activity is retried.
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the caller identifier presented to the customer when supported.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether do-not-call and communication preferences suppress activities.
    /// </summary>
    public bool RespectDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the dialer profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the dialer profile was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
