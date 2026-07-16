using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IDialerEligibilityService"/> implementation. It enforces the outbound compliance
/// rules that protect the dialer: a valid destination, the attempt limit, the retry cool-down, the
/// contact's do-not-call communication preference, national do-not-call registries, the calling window
/// evaluated in the contact's local time zone, and the rolling abandonment-rate cap.
/// </summary>
public sealed class DefaultDialerEligibilityService : IDialerEligibilityService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IContentManager _contentManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IBusinessHoursService _businessHoursService;
    private readonly IDialerAbandonmentPolicyService _abandonmentPolicyService;
    private readonly IEnumerable<INationalDoNotCallRegistry> _doNotCallRegistries;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDialerEligibilityService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to evaluate the retry cool-down.</param>
    /// <param name="contentManager">The content manager used to load the contact's communication preferences.</param>
    /// <param name="phoneNumberService">The phone number service used to normalize destinations to E.164.</param>
    /// <param name="businessHoursService">The business-hours service used to evaluate calling calendars.</param>
    /// <param name="abandonmentPolicyService">The policy service used to evaluate the rolling abandonment-rate cap.</param>
    /// <param name="doNotCallRegistries">The registered national do-not-call registries, if any.</param>
    /// <param name="clock">The clock used to evaluate cool-down and calling-window timing.</param>
    public DefaultDialerEligibilityService(
        IInteractionManager interactionManager,
        IContentManager contentManager,
        IPhoneNumberService phoneNumberService,
        IBusinessHoursService businessHoursService,
        IDialerAbandonmentPolicyService abandonmentPolicyService,
        IEnumerable<INationalDoNotCallRegistry> doNotCallRegistries,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _contentManager = contentManager;
        _phoneNumberService = phoneNumberService;
        _businessHoursService = businessHoursService;
        _abandonmentPolicyService = abandonmentPolicyService;
        _doNotCallRegistries = doNotCallRegistries;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialerEligibilityResult> EvaluateAsync(DialerEligibilityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Profile);
        ArgumentNullException.ThrowIfNull(context.Activity);

        var profile = context.Profile;
        var activity = context.Activity;

        if (string.IsNullOrEmpty(activity.PreferredDestination))
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.NoDestination,
                "The activity has no destination to dial.");
        }

        if (activity.Attempts >= profile.MaxAttempts)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.MaxAttemptsReached,
                $"The activity reached the maximum of {profile.MaxAttempts} attempts.");
        }

        var coolDown = await EvaluateRetryCoolDownAsync(profile, activity, cancellationToken);

        if (!coolDown.IsEligible)
        {
            return coolDown;
        }

        var contactPart = await LoadContactPartAsync(activity, cancellationToken);

        if (profile.RespectDoNotCall && contactPart is not null && contactPart.DoNotCall)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.DoNotCall,
                "The contact opted out of phone calls.");
        }

        if (profile.EnforceCallingWindow)
        {
            var normalizedDestination = Normalize(activity.PreferredDestination);
            var regionCode = string.IsNullOrEmpty(normalizedDestination)
                ? null
                : _phoneNumberService.GetRegionCode(normalizedDestination);
            var calendarId = ResolveCallingCalendarId(profile, regionCode);
            var isOpen = await _businessHoursService.EvaluateAsync(
                calendarId,
                _clock.UtcNow,
                contactPart?.TimeZoneId,
                cancellationToken);

            if (isOpen != true)
            {
                return DialerEligibilityResult.Suppressed(
                    DialerSuppressionReason.OutsideCallingWindow,
                    isOpen.HasValue
                        ? "The destination is outside its configured regional calling calendar."
                        : "The required outbound calling calendar is unavailable or disabled.");
            }
        }

        var abandonment = await _abandonmentPolicyService.EvaluateAsync(profile, cancellationToken);

        if (!abandonment.IsPermitted)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.AbandonmentRateExceeded,
                abandonment.Description);
        }

        if (profile.RespectDoNotCall && await IsOnNationalRegistryAsync(activity.PreferredDestination, cancellationToken))
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.NationalDoNotCallRegistry,
                "The destination is listed on a national do-not-call registry.");
        }

        return DialerEligibilityResult.Eligible();
    }

    private async Task<DialerEligibilityResult> EvaluateRetryCoolDownAsync(
        DialerProfile profile,
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        if (profile.RetryDelayMinutes <= 0)
        {
            return DialerEligibilityResult.Eligible();
        }

        var lastInteraction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (lastInteraction?.EndedUtc is null)
        {
            return DialerEligibilityResult.Eligible();
        }

        var nextEligibleUtc = lastInteraction.EndedUtc.Value.AddMinutes(profile.RetryDelayMinutes);

        if (nextEligibleUtc > _clock.UtcNow)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.RetryCoolDown,
                $"The previous attempt is still within the {profile.RetryDelayMinutes}-minute retry cool-down.");
        }

        return DialerEligibilityResult.Eligible();
    }

    private async Task<OmnichannelContactPart> LoadContactPartAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Published);

        if (contact is null || !contact.TryGet<OmnichannelContactPart>(out var contactPart))
        {
            return null;
        }

        return contactPart;
    }

    private static string ResolveCallingCalendarId(DialerProfile profile, string regionCode)
    {
        if (!string.IsNullOrWhiteSpace(regionCode) &&
            profile.RegionalCallingCalendarIds.TryGetValue(regionCode, out var regionalCalendarId) &&
            !string.IsNullOrWhiteSpace(regionalCalendarId))
        {
            return regionalCalendarId;
        }

        return profile.CallingCalendarId;
    }

    private async Task<bool> IsOnNationalRegistryAsync(string destination, CancellationToken cancellationToken)
    {
        if (!_doNotCallRegistries.Any())
        {
            return false;
        }

        var normalized = Normalize(destination);

        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        var numbers = new[] { normalized };

        foreach (var registry in _doNotCallRegistries)
        {
            var registered = await registry.GetRegisteredNumbersAsync(numbers, cancellationToken);

            if (registered is not null && registered.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private string Normalize(string phoneNumber)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, null, out var e164) && !string.IsNullOrEmpty(e164))
        {
            return e164;
        }

        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }
}
