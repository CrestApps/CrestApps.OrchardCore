using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The default <see cref="ISmsDispatcher"/>: resolves the provider that owns the sending number and sends
/// through it, so a portal whose numbers span multiple carriers routes each send to the correct provider.
/// </summary>
public sealed class SmsDispatcher : ISmsDispatcher
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ISmsProviderResolver _providerResolver;
    private readonly ISiteService _siteService;
    private readonly ISmsContactResolver _contactResolver;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly Redactor _addressRedactor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsDispatcher"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint manager used to look up the number's pinned provider.</param>
    /// <param name="providerResolver">The SMS provider resolver used to obtain a provider by technical name.</param>
    /// <param name="siteService">The site service used to read the portal and built-in SMS settings.</param>
    /// <param name="contactResolver">The resolver that finds the contact a refused recipient number belongs to.</param>
    /// <param name="contentManager">The content manager used to record a contact's SMS opt-out.</param>
    /// <param name="clock">The clock the opt-out is stamped with.</param>
    /// <param name="redactorProvider">The redactor provider used to keep phone numbers out of the log.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsDispatcher(
        IOmnichannelChannelEndpointManager endpointManager,
        ISmsProviderResolver providerResolver,
        ISiteService siteService,
        ISmsContactResolver contactResolver,
        IContentManager contentManager,
        IClock clock,
        IRedactorProvider redactorProvider,
        ILogger<SmsDispatcher> logger)
    {
        _endpointManager = endpointManager;
        _providerResolver = providerResolver;
        _siteService = siteService;
        _contactResolver = contactResolver;
        _contentManager = contentManager;
        _clock = clock;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SmsDispatchResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.From))
        {
            return SmsDispatchResult.Failed("The sending number (From) is required to resolve a provider.");
        }

        var providerName = await ResolveProviderNameAsync(message.From, cancellationToken);

        if (string.IsNullOrEmpty(providerName))
        {
            return SmsDispatchResult.Failed("No SMS provider could be resolved for the sending number, the portal default, or the tenant default.");
        }

        var provider = await _providerResolver.GetAsync(providerName);

        if (provider is null)
        {
            _logger.LogWarning("The resolved SMS provider '{ProviderName}' is not registered or enabled.", providerName);

            return SmsDispatchResult.Failed($"The SMS provider '{providerName}' is not registered or enabled.");
        }

        // A provider that only answers "not sent" can still say why through the refusal scope: the provider-specific
        // code that reads its response reports the reason into it.
        using var refusal = SmsProviderRefusalScope.Begin();

        SmsDispatchResult dispatch;

        // A provider that can report its own message id does so, because that identifier is what makes a later
        // delivery receipt match this exact message instead of the newest one without an id.
        if (provider is ISmsDispatchProvider dispatchProvider)
        {
            dispatch = await dispatchProvider.DispatchAsync(message, cancellationToken);
        }
        else
        {
            var result = await provider.SendAsync(message, cancellationToken);

            dispatch = result.Succeeded
                ? SmsDispatchResult.Success()
                : SmsDispatchResult.Failed((result.Errors ?? []).Select(error => error.Message).ToArray());
        }

        if (!dispatch.Succeeded)
        {
            dispatch.ErrorCode ??= refusal.ErrorCode;

            if (string.Equals(dispatch.ErrorCode, OmnichannelConstants.SmsErrorCodes.RecipientOptedOut, StringComparison.Ordinal))
            {
                await RecordOptOutAsync(message.To, cancellationToken);
            }
        }

        return dispatch;
    }

    // The provider refuses a recipient who opted out with it or the carrier — a STOP it handled itself — and will
    // refuse every later send the same way. Recording the opt-out on the contact, exactly as an inbound STOP does,
    // is what stops the portal, broadcasts and the outbox from trying again.
    private async Task RecordOptOutAsync(string recipient, CancellationToken cancellationToken)
    {
        var contactContentItemId = string.IsNullOrWhiteSpace(recipient)
            ? null
            : await _contactResolver.ResolveContactContentItemIdAsync(recipient.GetCleanedPhoneNumber(), cancellationToken);

        var contact = string.IsNullOrEmpty(contactContentItemId)
            ? null
            : await _contentManager.GetAsync(contactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The SMS provider refused a message because the recipient {Recipient} has opted out, and no contact record matches the number to record it on.",
                    _addressRedactor.Redact(recipient));
            }

            return;
        }

        if (contact.TryGet<OmnichannelContactPart>(out var part) && part.DoNotSms)
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(contactPart => contactPart.SetDoNotSms(true, _clock.UtcNow));

        await _contentManager.UpdateAsync(contact);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The SMS provider refused a message because the recipient has opted out; contact {ContactContentItemId} is now marked Do not SMS.",
                contact.ContentItemId.SanitizeLogValue());
        }
    }

    /// <inheritdoc/>
    public async ValueTask<string> ResolveProviderNameAsync(string fromNumber, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(fromNumber))
        {
            var endpoint = await _endpointManager.GetByServiceAddressAsync(
                OmnichannelConstants.Channels.Sms,
                fromNumber.GetCleanedPhoneNumber(),
                cancellationToken);

            if (endpoint is not null && !string.IsNullOrEmpty(endpoint.ProviderName))
            {
                return endpoint.ProviderName;
            }
        }

        // Fall back to OrchardCore's tenant-default SMS provider (Configuration -> Settings -> SMS).
        var smsSettings = await _siteService.GetSettingsAsync<SmsSettings>();

        return smsSettings.DefaultProviderName;
    }

    private static Result Failed(string message)
        => Result.Failed(new LocalizedString(message, message));
}
