using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class DialerProfileHandler : CatalogEntryHandlerBase<DialerProfile>
{
    private readonly IClock _clock;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IPhoneNumberService _phoneNumberService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="shellFeaturesManager">The shell features manager used to detect the Paced Dialing feature.</param>
    /// <param name="phoneNumberService">The phone number service used to validate the outbound caller id.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileHandler(
        IClock clock,
        IShellFeaturesManager shellFeaturesManager,
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<DialerProfileHandler> stringLocalizer)
    {
        _clock = clock;
        _shellFeaturesManager = shellFeaturesManager;
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        var profile = context.Model;

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(DialerProfile.Name)]));
        }

        if (profile.Mode.RequiresPacedDialerFeature() &&
            !await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.DialerPaced))
        {
            context.Result.Fail(new ValidationResult(S["Enable the Contact Center Paced Dialing feature before using Power, Progressive or Predictive dialing."], [nameof(DialerProfile.Mode)]));
        }

        if (profile.Mode == DialerMode.Predictive)
        {
            ValidatePredictivePacing(context, profile);
        }

        // The caller id becomes the outbound "from" the voice provider dials with, and a provider rejects a
        // value that is not a real phone number: a name like "CrestApps" fails instantly with no call placed and
        // no obvious cause. Validating here rather than in the editor means a recipe import cannot install a
        // profile that silently fails every dial. An empty value is allowed -- the provider then falls back to
        // its own configured default caller id.
        if (!string.IsNullOrWhiteSpace(profile.CallerId) &&
            !_phoneNumberService.TryParse(profile.CallerId, profile.DefaultRegionCode, out _))
        {
            context.Result.Fail(new ValidationResult(S["Enter the caller ID as a valid phone number in international format, for example +15551234567."], [nameof(DialerProfile.CallerId)]));
        }

        if (profile.CallsPerAgent < 1 || profile.CallsPerAgent > PowerDialerStrategy.MaxCallsPerAgent)
        {
            context.Result.Fail(new ValidationResult(S["The calls per agent must be between 1 and {0}.", PowerDialerStrategy.MaxCallsPerAgent], [nameof(DialerProfile.CallsPerAgent)]));
        }

        if (profile.EnforceCallingWindow && string.IsNullOrWhiteSpace(profile.CallingCalendarId))
        {
            context.Result.Fail(new ValidationResult(S["Select an outbound calling calendar when calling-window enforcement is enabled."], [nameof(DialerProfile.CallingCalendarId)]));
        }

        if (profile.MaxAbandonmentRatePercent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["The maximum abandonment rate must be between 0 and 100 percent."], [nameof(DialerProfile.MaxAbandonmentRatePercent)]));
        }

        if (profile.AbandonmentSampleFloor < 0)
        {
            context.Result.Fail(new ValidationResult(S["The abandonment sample floor cannot be negative."], [nameof(DialerProfile.AbandonmentSampleFloor)]));
        }

        // An unanswered automated call rings for at least the fifteen seconds the common abandoned-call rules expect. The
        // dialer never rings for less whatever is stored, but a profile that asks for less is refused rather than
        // silently overruled. Zero, as a recipe may write it, means the default.
        if (profile.Mode.IsAutomated() &&
            profile.RingTimeoutSeconds is not 0 and (< DialerAbandonment.MinimumRingTimeoutSeconds or > DialerAbandonment.MaximumRingTimeoutSeconds))
        {
            context.Result.Fail(new ValidationResult(S["The ring time must be between {0} and {1} seconds. An unanswered automated call rings for at least {0} seconds.", DialerAbandonment.MinimumRingTimeoutSeconds, DialerAbandonment.MaximumRingTimeoutSeconds], [nameof(DialerProfile.RingTimeoutSeconds)]));
        }

        if (profile.EnforceAbandonmentCap && profile.Mode.IsAutomated() && !profile.SafeHarborEnabled)
        {
            context.Result.Fail(new ValidationResult(S["Enable the abandoned-call message when an automated dialing mode enforces an abandonment cap."], [nameof(DialerProfile.SafeHarborEnabled)]));
        }

        if (profile.SafeHarborEnabled && string.IsNullOrWhiteSpace(profile.SafeHarborMessage))
        {
            context.Result.Fail(new ValidationResult(S["Provide the abandoned-call message when it is enabled."], [nameof(DialerProfile.SafeHarborMessage)]));
        }
    }

    // The predictive settings only govern Predictive profiles, so only they are held to them: a Power profile imported with
    // a stray value is not refused for a setting it never uses.
    private void ValidatePredictivePacing(ValidatingContext<DialerProfile> context, DialerProfile profile)
    {
        if (!Enum.IsDefined(profile.PredictivePacingModel))
        {
            context.Result.Fail(new ValidationResult(S["Select a valid pacing model."], [nameof(DialerProfile.PredictivePacingModel)]));
        }

        if (profile.TargetAbandonmentRatePercent is <= 0 or > 100 || double.IsNaN(profile.TargetAbandonmentRatePercent))
        {
            context.Result.Fail(new ValidationResult(S["The target abandonment rate must be greater than 0 and at most 100 percent."], [nameof(DialerProfile.TargetAbandonmentRatePercent)]));
        }

        if (profile.MaxLinesPerAgent is < PredictiveDialingDefaults.MinLinesPerAgent or > PredictiveDialingDefaults.MaxLinesPerAgent || double.IsNaN(profile.MaxLinesPerAgent))
        {
            context.Result.Fail(new ValidationResult(S["The lines per agent must be between {0} and {1}.", PredictiveDialingDefaults.MinLinesPerAgent, PredictiveDialingDefaults.MaxLinesPerAgent], [nameof(DialerProfile.MaxLinesPerAgent)]));
        }

        if (profile.MaxCallsInFlight is < 1 or > PredictiveDialingDefaults.MaxCallsInFlight)
        {
            context.Result.Fail(new ValidationResult(S["The calls in flight must be between 1 and {0}.", PredictiveDialingDefaults.MaxCallsInFlight], [nameof(DialerProfile.MaxCallsInFlight)]));
        }

        if (profile.AnswerRateSampleFloor is < PredictiveDialingDefaults.MinAnswerRateSampleFloor or > PredictiveDialingDefaults.MaxAnswerRateSampleFloor)
        {
            context.Result.Fail(new ValidationResult(S["The answer rate sample floor must be between {0} and {1} calls.", PredictiveDialingDefaults.MinAnswerRateSampleFloor, PredictiveDialingDefaults.MaxAnswerRateSampleFloor], [nameof(DialerProfile.AnswerRateSampleFloor)]));
        }

        if (profile.AnswerRateWindowMinutes is < PredictiveDialingDefaults.MinAnswerRateWindowMinutes or > PredictiveDialingDefaults.MaxAnswerRateWindowMinutes)
        {
            context.Result.Fail(new ValidationResult(S["The answer rate window must be between {0} and {1} minutes.", PredictiveDialingDefaults.MinAnswerRateWindowMinutes, PredictiveDialingDefaults.MaxAnswerRateWindowMinutes], [nameof(DialerProfile.AnswerRateWindowMinutes)]));
        }

        if (profile.FreeUpCreditPercent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["The share of agents freeing up that is counted must be between 0 and 100 percent."], [nameof(DialerProfile.FreeUpCreditPercent)]));
        }

        if (profile.ConnectWaitMilliseconds is < 0 or > PredictiveDialingDefaults.MaxConnectWaitMilliseconds)
        {
            context.Result.Fail(new ValidationResult(S["The connect wait must be between 0 and {0} milliseconds.", PredictiveDialingDefaults.MaxConnectWaitMilliseconds], [nameof(DialerProfile.ConnectWaitMilliseconds)]));
        }

        if (profile.PredictivePacingModel != PredictivePacingModel.OverDial)
        {
            return;
        }

        // Over-dialing is the one pacing that abandons calls by design, so it is only allowed with every safeguard on: a cap
        // it is held to, a target below that cap for it to steer by, and the message a person hears when no agent is free.
        if (!profile.EnforceAbandonmentCap)
        {
            context.Result.Fail(new ValidationResult(S["Over-dialing requires an enforced abandonment-rate cap."], [nameof(DialerProfile.EnforceAbandonmentCap)]));

            if (!profile.SafeHarborEnabled)
            {
                context.Result.Fail(new ValidationResult(S["Over-dialing requires the abandoned-call message."], [nameof(DialerProfile.SafeHarborEnabled)]));
            }
        }

        if (profile.MaxAbandonmentRatePercent <= 0)
        {
            context.Result.Fail(new ValidationResult(S["Over-dialing requires a maximum abandonment rate greater than 0 percent."], [nameof(DialerProfile.MaxAbandonmentRatePercent)]));
        }
        else if (profile.TargetAbandonmentRatePercent >= profile.MaxAbandonmentRatePercent)
        {
            context.Result.Fail(new ValidationResult(S["The target abandonment rate must be lower than the maximum abandonment rate."], [nameof(DialerProfile.TargetAbandonmentRatePercent)]));
        }
    }
}
