using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Moves a converted lead's message threads to the contact it became, so a customer who texts again lands in the
/// same thread, now linked to the contact.
/// </summary>
public sealed class MessagingLeadConversionRepointer : ILeadConversionRepointer
{
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingLeadConversionRepointer"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="logger">The logger.</param>
    public MessagingLeadConversionRepointer(
        ISession session,
        ILogger<MessagingLeadConversionRepointer> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RepointAsync(LeadConversionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var leadId = context.Lead?.ContentItemId;
        var contactId = context.Contact?.ContentItemId;

        if (string.IsNullOrEmpty(leadId) || string.IsNullOrEmpty(contactId))
        {
            return;
        }

        var conversations = await _session.Query<MessagingConversation, MessagingConversationIndex>(
                index => index.ContactContentItemId == leadId,
                collection: MessagingStorage.CollectionName)
            .ListAsync(cancellationToken);

        var moved = 0;

        foreach (var conversation in conversations)
        {
            conversation.ContactContentItemId = contactId;

            await _session.SaveAsync(conversation, collection: MessagingStorage.CollectionName, cancellationToken: cancellationToken);
            moved++;
        }

        if (moved > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Moved {Count} message thread(s) of a converted lead to the contact it became.",
                moved);
        }
    }
}
