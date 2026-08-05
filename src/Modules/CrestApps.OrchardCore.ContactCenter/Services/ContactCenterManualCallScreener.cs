using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Screens manual, agent-initiated soft-phone calls at the shared telephony boundary so they cannot
/// bypass the outbound compliance rules that protect campaign dialing. Manual dialing is screened
/// separately from automated campaign dialing — there is no dialer profile, CRM activity, retry
/// cool-down, or abandonment cap — but the do-not-call and calling-window rules still apply. Every
/// suppression is recorded as an auditable Contact Center event.
/// </summary>
public sealed class ContactCenterManualCallScreener : IOutboundCallScreener
{
    private readonly IOptions<ManualDialingComplianceOptions> _options;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IEnumerable<INationalDoNotCallRegistry> _doNotCallRegistries;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IContentManager _contentManager;
    private readonly IBusinessHoursService _businessHoursService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterManualCallScreener"/> class.
    /// </summary>
    /// <param name="options">The manual-dialing compliance options that govern manual soft-phone dialing.</param>
    /// <param name="phoneNumberService">The phone number service used to canonicalize the destination to E.164.</param>
    /// <param name="doNotCallRegistries">The registered national do-not-call registries, if any.</param>
    /// <param name="contactLookup">The lookup used to resolve a contact from the destination number.</param>
    /// <param name="contentManager">The content manager used to load the resolved contact.</param>
    /// <param name="businessHoursService">The business-hours service used to evaluate the calling window.</param>
    /// <param name="publisher">The event publisher used to record an auditable suppression event.</param>
    /// <param name="clock">The clock used to evaluate the calling window.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="logger">The logger used to record why a call could not be screened.</param>
    public ContactCenterManualCallScreener(
        IOptions<ManualDialingComplianceOptions> options,
        IPhoneNumberService phoneNumberService,
        IEnumerable<INationalDoNotCallRegistry> doNotCallRegistries,
        IInboundContactLookup contactLookup,
        IContentManager contentManager,
        IBusinessHoursService businessHoursService,
        IContactCenterEventPublisher publisher,
        IClock clock,
        IStringLocalizer<ContactCenterManualCallScreener> stringLocalizer,
        ILogger<ContactCenterManualCallScreener> logger)
    {
        _options = options;
        _phoneNumberService = phoneNumberService;
        _doNotCallRegistries = doNotCallRegistries;
        _contactLookup = contactLookup;
        _contentManager = contentManager;
        _businessHoursService = businessHoursService;
        _publisher = publisher;
        _clock = clock;
        S = stringLocalizer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request?.IsExtension == true)
        {
            // An internal extension routes to a colleague on the same telephone system, not to a consumer.
            // Do-not-call and calling-window rules govern outbound consumer contact, so they do not apply,
            // and an extension cannot be canonicalized to E.164 to be screened in the first place.
            return OutboundCallScreeningResult.Allow();
        }

        var options = _options.Value;

        if (!options.RespectDoNotCall && !options.EnforceCallingWindow)
        {
            return OutboundCallScreeningResult.Allow();
        }

        var destinationInput = context.Request?.To;

        if (string.IsNullOrWhiteSpace(destinationInput))
        {
            // There is nothing to dial, so there is nothing to screen. The provider rejects an empty
            // destination on its own; this is not a compliance decision.
            return OutboundCallScreeningResult.Allow();
        }

        var parsed = _phoneNumberService.TryParse(destinationInput, options.DefaultRegionCode, out var destination);

        OmnichannelContactPart contactPart = null;

        if (parsed)
        {
            contactPart = await LoadContactPartAsync(destination.Value, cancellationToken);
        }

        if (options.RespectDoNotCall)
        {
            if (!parsed)
            {
                // A destination that cannot be canonicalized cannot be compared against a do-not-call list,
                // and a registry that answers "not listed" for a number it never compared is exactly how a
                // call reaches a number that is on the list. Fail closed instead.
                return await SuppressAsync(
                    DialerSuppressionReason.NoDestination,
                    S["The destination is not a valid phone number, so it cannot be screened for do-not-call."].Value,
                    destinationInput,
                    cancellationToken);
            }

            if (contactPart is not null && contactPart.DoNotCall)
            {
                return await SuppressAsync(
                    DialerSuppressionReason.DoNotCall,
                    S["The contact opted out of phone calls."].Value,
                    destination.Value,
                    cancellationToken);
            }

            try
            {
                if (await IsOnNationalRegistryAsync(destination, cancellationToken))
                {
                    return await SuppressAsync(
                        DialerSuppressionReason.NationalDoNotCallRegistry,
                        S["The destination is listed on a national do-not-call registry."].Value,
                        destination.Value,
                        cancellationToken);
                }
            }
            catch (DoNotCallScreeningException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Suppressing a manual call because registry {RegistryKey} could not report whether the destination is listed.",
                    ex.RegistryKey);

                return await SuppressAsync(
                    DialerSuppressionReason.ComplianceScreeningUnavailable,
                    ex.Message,
                    destination.Value,
                    cancellationToken);
            }
        }

        if (options.EnforceCallingWindow)
        {
            var isOpen = await _businessHoursService.EvaluateAsync(
                options.CallingCalendarId,
                _clock.UtcNow,
                contactPart?.TimeZoneId,
                cancellationToken);

            if (isOpen != true)
            {
                return await SuppressAsync(
                    DialerSuppressionReason.OutsideCallingWindow,
                    isOpen.HasValue
                        ? S["The destination is outside the permitted manual calling window."].Value
                        : S["The manual calling window calendar is unavailable or disabled."].Value,
                    parsed ? destination.Value : destinationInput,
                    cancellationToken);
            }
        }

        return OutboundCallScreeningResult.Allow();
    }

    private async Task<OutboundCallScreeningResult> SuppressAsync(
        DialerSuppressionReason reason,
        string description,
        string destination,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Suppressed a manual outbound call: {Reason}.",
                reason);
        }

        var suppressionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.ManualDialSuppressed,
            AggregateType = ContactCenterConstants.AggregateTypes.ManualCall,
            SourceComponent = ContactCenterConstants.Components.Dialer,
        };

        suppressionEvent.SetData(new ManualDialSuppressionEventData
        {
            Reason = reason,
            Description = description,
            Destination = destination,
        });

        await _publisher.PublishAsync(suppressionEvent, cancellationToken);

        return OutboundCallScreeningResult.Deny(reason.ToString(), description);
    }

    private async Task<OmnichannelContactPart> LoadContactPartAsync(string destination, CancellationToken cancellationToken)
    {
        var contactItemIds = await _contactLookup.FindContactItemIdsAsync(destination, cancellationToken);

        foreach (var contactItemId in contactItemIds)
        {
            var contact = await _contentManager.GetAsync(contactItemId, VersionOptions.Published);

            if (contact is not null && contact.TryGet<OmnichannelContactPart>(out var contactPart))
            {
                // A do-not-call opt-out on any matched contact is decisive, so the opted-out contact is
                // preferred over a first match that happens not to be opted out.
                if (contactPart.DoNotCall)
                {
                    return contactPart;
                }
            }
        }

        if (contactItemIds.Count == 0)
        {
            return null;
        }

        var firstContact = await _contentManager.GetAsync(contactItemIds[0], VersionOptions.Published);

        return firstContact is not null && firstContact.TryGet<OmnichannelContactPart>(out var firstContactPart)
            ? firstContactPart
            : null;
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
}
