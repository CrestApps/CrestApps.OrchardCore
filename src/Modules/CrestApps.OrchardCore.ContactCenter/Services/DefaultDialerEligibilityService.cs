using CrestApps.Core.Support;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
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
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContentManager _contentManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IBusinessHoursService _businessHoursService;
    private readonly IDialerAbandonmentPolicyService _abandonmentPolicyService;
    private readonly IEnumerable<INationalDoNotCallRegistry> _doNotCallRegistries;
    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly IConsentService _consentService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IContactMethodsAccessor _contactMethods;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDialerEligibilityService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to evaluate the retry cool-down.</param>
    /// <param name="workStateService">The routing-owned work state service that holds the authoritative attempt count.</param>
    /// <param name="contentManager">The content manager used to load the contact's communication preferences.</param>
    /// <param name="phoneNumberService">The phone number service used to normalize destinations to E.164.</param>
    /// <param name="businessHoursService">The business-hours service used to evaluate calling calendars.</param>
    /// <param name="abandonmentPolicyService">The policy service used to evaluate the rolling abandonment-rate cap.</param>
    /// <param name="doNotCallRegistries">The registered national do-not-call registries, if any.</param>
    /// <param name="notInServiceNumbers">The list of numbers known not to be in service.</param>
    /// <param name="optOutResolver">The resolver that decides whether the contact, anybody at their numbers, or the number itself has opted out of calls.</param>
    /// <param name="clock">The clock used to evaluate cool-down and calling-window timing.</param>
    /// <param name="logger">The logger used to record why an attempt could not be screened.</param>
    public DefaultDialerEligibilityService(
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IContentManager contentManager,
        IPhoneNumberService phoneNumberService,
        IBusinessHoursService businessHoursService,
        IDialerAbandonmentPolicyService abandonmentPolicyService,
        IEnumerable<INationalDoNotCallRegistry> doNotCallRegistries,
        INotInServiceNumberService notInServiceNumbers,
        IConsentService consentService,
        IClock clock,
        ILogger<DefaultDialerEligibilityService> logger,
        IContactMethodsAccessor contactMethods = null)
    {
        _contactMethods = contactMethods;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _contentManager = contentManager;
        _phoneNumberService = phoneNumberService;
        _businessHoursService = businessHoursService;
        _abandonmentPolicyService = abandonmentPolicyService;
        _doNotCallRegistries = doNotCallRegistries;
        _notInServiceNumbers = notInServiceNumbers;
        _consentService = consentService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DialerEligibilityResult> EvaluateAsync(DialerEligibilityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Profile);
        ArgumentNullException.ThrowIfNull(context.Activity);

        var profile = context.Profile;
        var activity = context.Activity;

        // The activity's number is copied from the contact when the activity is created, and nothing refreshes it. A
        // record loaded before its contact had a number -- or whose number was added or corrected since -- kept an empty
        // destination, and agents were told "The activity has no destination to dial" on a contact that plainly has one.
        // The contact is asked for its number now, before the record is given up as undialable.
        if (string.IsNullOrEmpty(activity.PreferredDestination))
        {
            activity.PreferredDestination = await FindContactPhoneNumberAsync(activity, cancellationToken);
        }

        if (string.IsNullOrEmpty(activity.PreferredDestination))
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.NoDestination,
                "The activity has no destination to dial.");
        }

        // The destination is canonicalized once, here, and every rule below works from the canonical value.
        // A destination that cannot be canonicalized suppresses the attempt instead of being repaired into a
        // digits-only approximation: that approximation could not be matched against a do-not-call registry,
        // and the registry answering "not listed" for a number it never compared is how a call reaches a
        // number that is on the list.
        if (!_phoneNumberService.TryParse(activity.PreferredDestination, profile.DefaultRegionCode, out var destination))
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.NoDestination,
                "The activity destination is not a valid phone number, so it cannot be checked for compliance.");
        }

        // A number already found dead is not dialed again, whatever else would allow it: the answer cannot change.
        if (await _notInServiceNumbers.IsNotInServiceAsync(destination.Value, cancellationToken))
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.NumberNotInService,
                "The destination is known not to be in service.");
        }

        var workState = await _workStateService.GetAsync(activity.ItemId, cancellationToken);

        // The pre-attempt check looks at the attempt about to be made: the activity's own attempt number on its first
        // call, the one after it on a further call. The dispatch re-validation runs after that attempt was recorded,
        // so it checks the recorded attempt itself, or a single-attempt profile would suppress its only call.
        var attemptNumber = context.AttemptAlreadyCounted
            ? Math.Max(1, workState?.Attempts ?? activity.Attempts)
            : ContactCenterWorkState.NextAttemptNumber(workState, activity.Attempts);
        var maxAttempts = DialerAttemptLimit.Resolve(activity, profile);
        var attemptsExceedLimit = attemptNumber > maxAttempts;

        if (attemptsExceedLimit)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.MaxAttemptsReached,
                $"The activity reached the maximum of {maxAttempts} attempts.");
        }

        var coolDown = await EvaluateRetryCoolDownAsync(profile, activity, cancellationToken);

        if (!coolDown.IsEligible)
        {
            return coolDown;
        }

        var contact = await LoadContactAsync(activity, cancellationToken);
        var contactPart = contact is not null && contact.TryGet<OmnichannelContactPart>(out var part)
            ? part
            : null;

        // Asked of the consent service, not of the contact's own flag: a stop recorded on another record that holds the
        // same number, or against the number while no record held it, is the same person asking. The do-not-call lists
        // are screened below with the national registries, under their own reason.
        if (profile.RespectDoNotCall &&
            (await _consentService.GetAsync(new ConsentQuery
            {
                Channel = ConsentChannels.Phone,
                Address = destination.Value,
                ContactKind = ConsentContactKinds.Contact,
                ContactId = contact?.ContentItemId,
                Contact = contact,
                ExcludedSources = [ConsentSourceNames.DoNotCallLists],
            }, cancellationToken)).HasOptedOut)
        {
            return DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.DoNotCall,
                "The contact opted out of phone calls.");
        }

        if (profile.EnforceCallingWindow)
        {
            var regionCode = _phoneNumberService.GetRegionCode(destination);
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

        if (profile.RespectDoNotCall)
        {
            bool isRegistered;

            try
            {
                isRegistered = await IsOnNationalRegistryAsync(destination, cancellationToken);
            }
            catch (DoNotCallScreeningException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Suppressing the dial attempt for activity {ActivityId} because registry {RegistryKey} could not report whether the destination is listed.",
                    activity.ItemId.SanitizeLogValue(),
                    ex.RegistryKey);

                // The registry reported nothing, not "not listed". Suppressing here is not terminal: the
                // destination has not been shown to be off limits, only unverified, so the activity stays
                // available for a later cycle once the registry can answer.
                return DialerEligibilityResult.Suppressed(
                    DialerSuppressionReason.ComplianceScreeningUnavailable,
                    ex.Message);
            }

            if (isRegistered)
            {
                return DialerEligibilityResult.Suppressed(
                    DialerSuppressionReason.NationalDoNotCallRegistry,
                    "The destination is listed on a national do-not-call registry.");
            }
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

    private async Task<ContentItem> LoadContactAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Published);
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

    private async Task<bool> IsOnNationalRegistryAsync(PhoneNumber destination, CancellationToken cancellationToken)
    {
        if (!_doNotCallRegistries.Any())
        {
            return false;
        }

        var numbers = new[] { destination };

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

    /// <summary>
    /// Reads the contact's preferred phone number as it is now, for an activity that was created without one.
    /// </summary>
    private async Task<string> FindContactPhoneNumberAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (_contactMethods is null || string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Published);

        if (contact is null)
        {
            return null;
        }

        var phoneNumber = _contactMethods.Get(contact).GetPreferredPhoneNumber()?.PhoneNumber;

        if (!string.IsNullOrEmpty(phoneNumber) && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Activity '{ActivityItemId}' had no number to dial; using its contact's current number instead.",
                activity.ItemId.SanitizeLogValue());
        }

        return phoneNumber;
    }
}
