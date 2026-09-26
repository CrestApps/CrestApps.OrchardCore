using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Invalidates the cached enabled-configuration snapshot for <typeparamref name="T"/> whenever an entry of that type is
/// created, updated, or deleted. Invalidation is deferred until after the ambient scope commits so that a concurrent
/// read cannot repopulate the cache with pre-commit data.
/// </summary>
/// <typeparam name="T">The catalog entry type whose cached snapshot is kept in sync.</typeparam>
public sealed class ContactCenterConfigurationCacheInvalidationHandler<T> : CatalogEntryHandlerBase<T>
    where T : class
{
    /// <inheritdoc/>
    public override Task CreatedAsync(CreatedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    /// <inheritdoc/>
    public override Task DeletedAsync(DeletedContext<T> context, CancellationToken cancellationToken = default)
        => InvalidateAsync();

    private static Task InvalidateAsync()
    {
        if (ShellScope.Current is null)
        {
            return Task.CompletedTask;
        }

        ShellScope.AddDeferredTask(scope =>
            scope.ServiceProvider.GetRequiredService<IContactCenterConfigurationCache>().InvalidateEnabledAsync<T>());

        return Task.CompletedTask;
    }
}
