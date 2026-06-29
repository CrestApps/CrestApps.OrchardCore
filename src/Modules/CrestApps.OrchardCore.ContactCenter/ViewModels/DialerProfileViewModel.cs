using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

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
    /// Gets or sets the dialer profile description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the available campaigns.
    /// </summary>
    public IList<SelectListItem> CampaignOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the available queues.
    /// </summary>
    public IList<SelectListItem> QueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialing mode.
    /// </summary>
    public DialerMode Mode { get; set; } = DialerMode.Preview;

    /// <summary>
    /// Gets or sets the Contact Center voice provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the available voice call providers.
    /// </summary>
    public IList<SelectListItem> ProviderOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of calls per available agent.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int CallsPerAgent { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of attempts per activity.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the retry delay, in minutes.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int RetryDelayMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the caller identifier.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether do-not-call and communication preferences are honored.
    /// </summary>
    public bool RespectDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
