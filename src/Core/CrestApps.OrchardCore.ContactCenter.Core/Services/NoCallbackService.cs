using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="ICallbackService"/> for a tenant that has not enabled callbacks. Nothing is stored, so
/// nothing can be promoted. A caller is told the callback was not taken rather than being handed one that will
/// never be called back, which is the failure this default exists to make visible.
/// </summary>
public sealed class NoCallbackService : ICallbackService
{
    /// <inheritdoc/>
    public Task<CallbackRequest> ScheduleAsync(CallbackRequest callback, CancellationToken cancellationToken = default)
        => Task.FromResult<CallbackRequest>(null);

    /// <inheritdoc/>
    public Task<int> PromoteDueAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
