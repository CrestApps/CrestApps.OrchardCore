using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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
    /// Gets or sets a value indicating whether the Contact Center Automated Dialer feature is enabled, which
    /// determines whether the Power and Progressive automated pacing modes are offered in the editor.
    /// </summary>
    public bool AutomatedDialerEnabled { get; set; }

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
    [Range(1, PowerDialerStrategy.MaxCallsPerAgent)]
    public int CallsPerAgent { get; set; } = 1;

    /// <summary>
    /// Gets the maximum number of calls per agent allowed for Power dialing.
    /// </summary>
    public int MaxCallsPerAgent { get; } = PowerDialerStrategy.MaxCallsPerAgent;

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
    /// Gets or sets a value indicating whether calls are restricted to the configured calling window.
    /// </summary>
    public bool EnforceCallingWindow { get; set; }

    /// <summary>
    /// Gets or sets the default business-hours calendar used to evaluate outbound calls.
    /// </summary>
    public string CallingCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the available business-hours calendars.
    /// </summary>
    public IList<SelectListItem> CallingCalendarOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether outbound dialing is gated by a rolling abandonment-rate cap.
    /// </summary>
    public bool EnforceAbandonmentCap { get; set; }

    /// <summary>
    /// Gets or sets the maximum tolerated rolling abandonment rate as a percentage of live-answered calls.
    /// </summary>
    [Range(0, 100)]
    public double MaxAbandonmentRatePercent { get; set; } = 3;

    /// <summary>
    /// Gets or sets the minimum number of live-answered calls required before the abandonment rate is enforced.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int AbandonmentSampleFloor { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether an abandoned automated call plays a safe-harbor announcement.
    /// </summary>
    public bool SafeHarborEnabled { get; set; }

    /// <summary>
    /// Gets or sets the safe-harbor announcement played to a live party when no agent connects in time.
    /// </summary>
    public string SafeHarborMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
