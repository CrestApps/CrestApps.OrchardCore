using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    /// Gets or sets the dialing mode.
    /// </summary>
    public DialerMode Mode { get; set; } = DialerMode.Preview;

    /// <summary>
    /// Gets or sets a value indicating whether the Contact Center Paced Dialing feature is enabled, which
    /// determines whether the Power, Progressive and Predictive automated pacing modes are offered in the editor.
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
    /// Gets or sets whether automated dialing screens out answering machines before connecting an agent.
    /// </summary>
    public DialerAnsweringMachineDetection AnsweringMachineDetection { get; set; }

    /// <summary>
    /// Gets or sets how many seconds an automated call rings before it is given up as unanswered. Validated by the
    /// profile handler, and only for the automated modes the field is shown for.
    /// </summary>
    public int RingTimeoutSeconds { get; set; } = DialerAbandonment.DefaultRingTimeoutSeconds;

    /// <summary>
    /// Gets or sets the caller identifier.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the caller ID is presented even for agents who have their own line.
    /// </summary>
    public bool AlwaysUseCallerId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 region a destination without a country calling code is read in.
    /// </summary>
    public string DefaultRegionCode { get; set; }

    /// <summary>
    /// Gets or sets the country options presented for <see cref="DefaultRegionCode"/>.
    /// </summary>
    public IList<SelectListItem> DefaultRegionOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the numbers used for calls, offered as the caller ID.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CallerIdOptions { get; set; } = [];

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
    /// Gets or sets a value indicating whether an abandoned automated call plays the abandoned-call message.
    /// </summary>
    public bool SafeHarborEnabled { get; set; }

    /// <summary>
    /// Gets or sets the abandoned-call message spoken to a person who answered when no agent can be connected.
    /// </summary>
    public string SafeHarborMessage { get; set; }

    /// <summary>
    /// Gets or sets the message suggested when none has been written yet.
    /// </summary>
    [BindNever]
    public string DefaultSafeHarborMessage { get; set; } = DialerAbandonment.DefaultMessage;

    /// <summary>
    /// Gets or sets the profile's measured abandonment over the rolling window the cap is enforced on, or
    /// <see langword="null"/> when it is not measured (a new profile, or no statistics provider).
    /// </summary>
    [BindNever]
    public DialerAbandonmentStatistics RollingAbandonment { get; set; }

    /// <summary>
    /// Gets or sets the length, in minutes, of the rolling window <see cref="RollingAbandonment"/> covers.
    /// </summary>
    [BindNever]
    public int AbandonmentWindowMinutes { get; set; }

    /// <summary>
    /// Gets or sets the profile's measured abandonment over the last thirty days, the period the common abandoned-call
    /// rules measure over, or <see langword="null"/> when it is not measured.
    /// </summary>
    [BindNever]
    public DialerAbandonmentStatistics MonthlyAbandonment { get; set; }

    /// <summary>
    /// Gets or sets how a Predictive profile paces its calls.
    /// </summary>
    public PredictivePacingModel PredictivePacingModel { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate over-dialing steers toward, in percent. Validated by the profile handler.
    /// </summary>
    public double TargetAbandonmentRatePercent { get; set; } = PredictiveDialingDefaults.TargetAbandonmentRatePercent;

    /// <summary>
    /// Gets or sets the most calls over-dialing may have ringing per available agent. Validated by the profile handler.
    /// </summary>
    public double MaxLinesPerAgent { get; set; } = PredictiveDialingDefaults.LinesPerAgent;

    /// <summary>
    /// Gets or sets the most calls a Predictive profile may have in flight per campaign. Validated by the profile handler.
    /// </summary>
    public int MaxCallsInFlight { get; set; } = PredictiveDialingDefaults.CallsInFlight;

    /// <summary>
    /// Gets or sets the fewest settled calls the answer rate is measured over before over-dialing trusts it.
    /// </summary>
    public int AnswerRateSampleFloor { get; set; } = PredictiveDialingDefaults.AnswerRateSampleFloor;

    /// <summary>
    /// Gets or sets the minutes of history the answer rate is measured over.
    /// </summary>
    public int AnswerRateWindowMinutes { get; set; } = PredictiveDialingDefaults.AnswerRateWindowMinutes;

    /// <summary>
    /// Gets or sets a value indicating whether agents expected to free up are counted.
    /// </summary>
    public bool CreditAgentsFreeingUp { get; set; }

    /// <summary>
    /// Gets or sets the percentage of the agents expected to free up that is counted.
    /// </summary>
    public int FreeUpCreditPercent { get; set; } = PredictiveDialingDefaults.FreeUpCreditPercent;

    /// <summary>
    /// Gets or sets how long, in milliseconds, a person who answered may wait for an agent to free up.
    /// </summary>
    public int ConnectWaitMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an abandoned call is retried only with an agent reserved for it.
    /// </summary>
    public bool AbandonedRetryRequiresAgent { get; set; } = true;

    /// <summary>
    /// Gets or sets what the profile's recent calls measured over its answer-rate window, or <see langword="null"/> when
    /// it is not measured (a new profile, or the Paced Dialing feature is off).
    /// </summary>
    [BindNever]
    public DialerPacingStatistics PacingStatistics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
