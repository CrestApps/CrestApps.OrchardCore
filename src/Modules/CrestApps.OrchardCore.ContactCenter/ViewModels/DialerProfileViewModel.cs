using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for a dialer profile.
/// </summary>
public class DialerProfileViewModel
{
    /// <summary>
    /// Gets or sets the dialer profile identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique dialer profile name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the dialing mode.
    /// </summary>
    public DialerMode Mode { get; set; } = DialerMode.Preview;

    /// <summary>
    /// Gets or sets the dialer provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the number of calls per available agent.
    /// </summary>
    public int CallsPerAgent { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of attempts per activity.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the caller identifier.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
