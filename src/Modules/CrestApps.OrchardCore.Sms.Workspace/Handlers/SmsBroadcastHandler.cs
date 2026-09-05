using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Handlers;

/// <summary>
/// Stamps audit times on an <see cref="SmsBroadcast"/>.
/// </summary>
internal sealed class SmsBroadcastHandler : CatalogEntryHandlerBase<SmsBroadcast>
{
    private readonly IClock _clock;

    public SmsBroadcastHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<SmsBroadcast> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<SmsBroadcast> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}
