using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailOptOutService"/>.
/// </summary>
public sealed class EmailOptOutService : IEmailOptOutService
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOptOutService"/> class.
    /// </summary>
    /// <param name="channelResolver">The resolver of the email channel, which finds the contacts holding an address.</param>
    /// <param name="contentManager">The content manager the contacts are updated through.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public EmailOptOutService(
        IMessagingChannelResolver channelResolver,
        IContentManager contentManager,
        IClock clock,
        ILogger<EmailOptOutService> logger)
    {
        _channelResolver = channelResolver;
        _contentManager = contentManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> OptOutAsync(string address, CancellationToken cancellationToken = default)
    {
        var normalized = OmnichannelEmailAddress.Normalize(address);
        var channel = _channelResolver.Get(OmnichannelConstants.Channels.Email);

        if (string.IsNullOrEmpty(normalized) || channel is null)
        {
            return 0;
        }

        var marked = 0;

        foreach (var contactId in await channel.FindContactIdsAsync(normalized, cancellationToken))
        {
            var contact = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

            if (contact is null || !contact.TryGet<OmnichannelContactPart>(out var part) || part.DoNotEmail)
            {
                continue;
            }

            contact.Alter<OmnichannelContactPart>(contactPart => contactPart.SetDoNotEmail(true, _clock.UtcNow));

            await _contentManager.UpdateAsync(contact);

            marked++;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("An email address was opted out of email; {Count} contact record(s) were marked Do not email.", marked);
        }

        return marked;
    }
}
