using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Reads the contact's time zone from the CRM contact the conversation is linked to, so quiet hours are
/// evaluated where the customer actually is. A thread with no linked contact resolves to null, and the caller
/// falls back to the calendar's own zone.
/// </summary>
public sealed class MessagingContactTimeZoneResolver : IMessagingContactTimeZoneResolver
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingContactTimeZoneResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    public MessagingContactTimeZoneResolver(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public async Task<string> ResolveAsync(MessagingConversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return contact?.Get<OmnichannelContactPart>(nameof(OmnichannelContactPart))?.TimeZoneId;
    }
}
