using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;

/// <summary>
/// Stamps audit times on an <see cref="MessagingBroadcast"/>.
/// </summary>
internal sealed class MessagingBroadcastHandler : CatalogEntryHandlerBase<MessagingBroadcast>
{
    private readonly IClock _clock;

    public MessagingBroadcastHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<MessagingBroadcast> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<MessagingBroadcast> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}
