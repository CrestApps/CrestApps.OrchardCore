using CrestApps.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The default implementation of <see cref="ISmsConversationManager"/>.
/// </summary>
public sealed class SmsConversationManager : CatalogManager<SmsConversation>, ISmsConversationManager
{
    private readonly ISmsConversationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationManager"/> class.
    /// </summary>
    /// <param name="store">The underlying conversation store.</param>
    /// <param name="handlers">The catalog entry handlers for conversations.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsConversationManager(
        ISmsConversationStore store,
        IEnumerable<ICatalogEntryHandler<SmsConversation>> handlers,
        ILogger<CatalogManager<SmsConversation>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string contactAddress, CancellationToken cancellationToken = default)
    {
        var conversation = await _store.FindByAddressesAsync(serviceAddress, contactAddress, cancellationToken);

        if (conversation is not null)
        {
            await LoadAsync(conversation, cancellationToken);
        }

        return conversation;
    }
}
