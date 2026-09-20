using CrestApps.Core.Handlers;
using CrestApps.Core.Hosting;
using CrestApps.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Invalidates the cached enabled-configuration snapshot for <typeparamref name="T"/> whenever an entry of that type is
/// created, updated, or deleted. Invalidation is deferred until after the ambient scope commits so that a concurrent
/// read cannot repopulate the cache with pre-commit data.
/// </summary>
/// <typeparam name="T">The catalog entry type whose cached snapshot is kept in sync.</typeparam>
public sealed class ContactCenterConfigurationCacheInvalidationHandler<T> : CatalogEntryHandlerBase<T>
    where T : class
{
    private readonly IAfterCommitTaskQueue _afterCommitTaskQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationCacheInvalidationHandler{T}"/> class.
    /// </summary>
    /// <param name="afterCommitTaskQueue">The queue that defers the invalidation until the ambient scope commits.</param>
    public ContactCenterConfigurationCacheInvalidationHandler(IAfterCommitTaskQueue afterCommitTaskQueue)
    {
        _afterCommitTaskQueue = afterCommitTaskQueue;
    }

    /// <inheritdoc/>
    public override Task CreatedAsync(CreatedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    /// <inheritdoc/>
    public override Task DeletedAsync(DeletedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    private Task InvalidateAsync()
    {
        _afterCommitTaskQueue.Enqueue(serviceProvider =>
            serviceProvider.GetRequiredService<IContactCenterConfigurationCache>().InvalidateEnabledAsync<T>());

        return Task.CompletedTask;
    }
}
