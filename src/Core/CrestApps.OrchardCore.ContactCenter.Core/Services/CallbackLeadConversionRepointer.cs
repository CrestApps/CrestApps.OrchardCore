using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Moves a converted lead's open callbacks to the contact it became, so a callback that comes due after the
/// conversion calls the contact instead of creating work on the closed lead. Finished callbacks stay with the lead
/// as part of its history.
/// </summary>
public sealed class CallbackLeadConversionRepointer : ILeadConversionRepointer
{
    private static readonly CallbackRequestStatus[] _openStatuses =
    [
        CallbackRequestStatus.Pending,
        CallbackRequestStatus.Scheduled,
        CallbackRequestStatus.InProgress,
    ];

    private readonly ISession _session;
    private readonly ICallbackRequestStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackLeadConversionRepointer"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="store">The callback store.</param>
    /// <param name="logger">The logger.</param>
    public CallbackLeadConversionRepointer(
        ISession session,
        ICallbackRequestStore store,
        ILogger<CallbackLeadConversionRepointer> logger)
    {
        _session = session;
        _store = store;
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

        // The callback index has no contact column, so the open callbacks are read by status and matched here. Open
        // callbacks are few, which keeps this bounded without a migration.
        var callbacks = await _session.Query<CallbackRequest, CallbackRequestIndex>(
                index => index.Status.IsIn(_openStatuses),
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        var moved = 0;

        foreach (var callback in callbacks)
        {
            if (!string.Equals(callback.ContactContentItemId, leadId, StringComparison.Ordinal))
            {
                continue;
            }

            callback.ContactContentItemId = contactId;
            callback.ContactContentType = context.Contact.ContentType;

            await _store.UpdateAsync(callback, cancellationToken);
            moved++;
        }

        if (moved > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Moved {Count} open callback(s) of a converted lead to the contact it became.",
                moved);
        }
    }
}
