using CrestApps.Core.Support;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailOptOutService"/>. The opt-out goes through <see cref="IConsentService"/>, which every
/// send path asks, so it holds for the address itself and for every contact found holding it.
/// </summary>
public sealed class EmailOptOutService : IEmailOptOutService
{
    /// <summary>
    /// The source recorded with an opt-out made by an unsubscribe link, a provider's unsubscribe event or a complaint.
    /// </summary>
    public const string ConsentSource = "email-unsubscribe";

    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IContentManager _contentManager;
    private readonly IConsentService _consentService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOptOutService"/> class.
    /// </summary>
    /// <param name="channelResolver">The resolver of the email channel, which finds the contacts holding an address.</param>
    /// <param name="contentManager">The content manager the contacts are read through.</param>
    /// <param name="consentService">The consent service the opt-out is recorded through.</param>
    /// <param name="logger">The logger.</param>
    public EmailOptOutService(
        IMessagingChannelResolver channelResolver,
        IContentManager contentManager,
        IConsentService consentService,
        ILogger<EmailOptOutService> logger)
    {
        _channelResolver = channelResolver;
        _contentManager = contentManager;
        _consentService = consentService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> OptOutAsync(string address, CancellationToken cancellationToken = default)
    {
        var normalized = OmnichannelEmailAddress.Normalize(address);

        if (string.IsNullOrEmpty(normalized))
        {
            return 0;
        }

        // The address itself is opted out first, whoever holds it. A contact's second address, or an address no contact
        // holds (a recipient typed into a broadcast), is honored as surely as a contact's primary one: every send path
        // asks the consent service about the address it sends to.
        await _consentService.RecordAsync(ConsentChannels.Email, normalized, new ConsentChange
        {
            OptedOut = true,
            Source = ConsentSource,
        }, cancellationToken);

        var marked = 0;
        var channel = _channelResolver.Get(OmnichannelConstants.Channels.Email);

        if (channel is not null)
        {
            foreach (var contactId in await channel.FindContactIdsAsync(normalized, cancellationToken))
            {
                var contact = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

                if (contact is null || !contact.TryGet<OmnichannelContactPart>(out var part) || part.DoNotEmail)
                {
                    continue;
                }

                // Recorded through the consent service as well, so the contact's own Do not email is set by the source
                // that owns it and the change is seen wherever consent is read.
                await _consentService.RecordAsync(ConsentChannels.Email, normalized, new ConsentChange
                {
                    OptedOut = true,
                    Source = ConsentSource,
                    ContactKind = ConsentContactKinds.Contact,
                    ContactId = contactId,
                }, cancellationToken);

                marked++;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("An email address was opted out of email; {Count} contact record(s) were also marked Do not email.", marked);
        }

        return marked;
    }
}
